using Akka.Actor;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using Memorizer.Actors;
using Memorizer.IntegrationTests.Fakes;
using Memorizer.Models;
using Memorizer.Models.ValueTypes;
using Memorizer.Services;
using Memorizer.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit.Abstractions;

namespace Memorizer.IntegrationTests;

/// <summary>
/// Integration tests for the dimension migration feature.
/// Uses a dedicated PostgreSQL container and FAKE embedding services
/// for fast, deterministic testing without requiring Ollama.
/// </summary>
[Collection(nameof(DimensionMigrationTestCollection))]
public class DimensionMigrationIntegrationTests : TestKit
{
    private readonly DimensionMigrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    // Shared fake embedding service - allows us to change dimensions mid-test
    private static FakeEmbeddingService? _fakeEmbeddingService;

    public DimensionMigrationIntegrationTests(DimensionMigrationTestFixture fixture, ITestOutputHelper output)
        : base(output: output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// Test NULL embedding handling in Storage methods.
    /// This is critical during dimension migration when embeddings are temporarily NULL.
    /// </summary>
    [Fact]
    public async Task Storage_HandlesNullEmbeddings_DuringMigration()
    {
        // Reset database to starting dimensions
        await _fixture.ResetDatabaseAsync(
            DimensionMigrationTestFixture.StartingModel,
            DimensionMigrationTestFixture.StartingModelDimensions);

        // Set fake service to matching dimensions so we can store memories
        _fakeEmbeddingService!.SetDimensions(DimensionMigrationTestFixture.StartingModelDimensions);

        var storage = Host.Services.GetRequiredService<IStorage>();

        // Create a test memory
        var memory = await storage.StoreMemory(
            "test",
            "Test content for null embedding test",
            "test",
            new[] { "null-test" },
            new Confidence(1.0),
            "Null Test Memory");

        _output.WriteLine($"Created memory {memory.Id}");

        // Manually set embedding to NULL to simulate mid-migration state
        // (The migration process drops NOT NULL constraint during regeneration)
        await using var conn = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await conn.OpenAsync();

        // Drop NOT NULL constraints like migration does
        await using (var dropConstraintCmd = new NpgsqlCommand(
            "ALTER TABLE memories ALTER COLUMN embedding DROP NOT NULL", conn))
        {
            await dropConstraintCmd.ExecuteNonQueryAsync();
        }
        await using (var dropConstraintCmd2 = new NpgsqlCommand(
            "ALTER TABLE memories ALTER COLUMN embedding_metadata DROP NOT NULL", conn))
        {
            await dropConstraintCmd2.ExecuteNonQueryAsync();
        }

        await using var updateCmd = new NpgsqlCommand(
            "UPDATE memories SET embedding = NULL, embedding_metadata = NULL WHERE id = @id", conn);
        updateCmd.Parameters.AddWithValue("id", memory.Id.Value);
        await updateCmd.ExecuteNonQueryAsync();

        _output.WriteLine("Set embeddings to NULL");

        // Test Get() handles NULL embedding
        var fetched = await storage.Get(memory.Id);
        Assert.NotNull(fetched);
        Assert.Null(fetched.Embedding);
        _output.WriteLine("✓ Get() handled NULL embedding correctly");

        // Test GetMemoriesPaginated() handles NULL embedding
        var (memories, count) = await storage.GetMemoriesPaginated(1, 100);
        var nullMemory = memories.FirstOrDefault(m => m.Id == memory.Id);
        Assert.NotNull(nullMemory);
        Assert.Null(nullMemory.Embedding);
        _output.WriteLine("✓ GetMemoriesPaginated() handled NULL embedding correctly");

        // Clean up
        await storage.Delete(memory.Id);
    }

    /// <summary>
    /// Test that dimension mismatch is correctly detected.
    /// </summary>
    [Fact]
    public async Task DimensionService_DetectsMismatch_WhenDimensionsDiffer()
    {
        // Reset database to 384 dimensions
        await _fixture.ResetDatabaseAsync(
            DimensionMigrationTestFixture.StartingModel,
            DimensionMigrationTestFixture.StartingModelDimensions);

        var dimensionService = Host.Services.GetRequiredService<IEmbeddingDimensionService>();

        // The fake embedding service is configured to return 768 dimensions (target)
        // but the database is set to 384 - this should be detected as a mismatch
        var validation = await dimensionService.ValidateAsync();

        _output.WriteLine($"Validation result:");
        _output.WriteLine($"  RequiresMigration: {validation.RequiresMigration}");
        _output.WriteLine($"  DetectedModelDimensions: {validation.DetectedModelDimensions}");
        _output.WriteLine($"  StoredDimensions: {validation.StoredDimensions}");
        _output.WriteLine($"  DatabaseSchemaDimensions: {validation.DatabaseSchemaDimensions}");
        _output.WriteLine($"  MismatchReason: {validation.MismatchDescription}");

        Assert.True(validation.RequiresMigration, "Should detect migration requirement");
        Assert.Equal(DimensionMigrationTestFixture.TargetModelDimensions, validation.DetectedModelDimensions);
        Assert.Equal(DimensionMigrationTestFixture.StartingModelDimensions, validation.StoredDimensions);
    }

    /// <summary>
    /// Test that the DimensionMigrationActor rejects concurrent migration requests.
    /// </summary>
    [Fact]
    public async Task DimensionMigrationActor_RejectsConcurrentMigrations()
    {
        // Reset database
        await _fixture.ResetDatabaseAsync(
            DimensionMigrationTestFixture.StartingModel,
            DimensionMigrationTestFixture.StartingModelDimensions);

        // Set fake service to matching dimensions so we can store memories
        _fakeEmbeddingService!.SetDimensions(DimensionMigrationTestFixture.StartingModelDimensions);

        var storage = Host.Services.GetRequiredService<IStorage>();

        // Create some test memories
        for (int i = 0; i < 3; i++)
        {
            await storage.StoreMemory("test", $"Content {i}", "test", new[] { "concurrent-test" }, new Confidence(1.0), $"Memory {i}");
        }

        // Switch to target dimensions to simulate model change that triggers migration
        _fakeEmbeddingService.SetDimensions(DimensionMigrationTestFixture.TargetModelDimensions);

        // Get the migration actor
        var migrationActorRef = Host.Services.GetRequiredService<IRequiredActor<DimensionMigrationActorKey>>();
        var migrationActor = await migrationActorRef.GetAsync();

        // Start first migration
        var firstStatus = await migrationActor.Ask<DimensionMigrationStatus>(
            new StartDimensionMigration(RequestedBy: "test-1"),
            TimeSpan.FromSeconds(30));

        _output.WriteLine($"First migration: IsRunning={firstStatus.IsRunning}, Status={firstStatus.Status}");

        if (firstStatus.IsRunning)
        {
            // Try to start second migration while first is running
            var secondStatus = await migrationActor.Ask<DimensionMigrationStatus>(
                new StartDimensionMigration(RequestedBy: "test-2"),
                TimeSpan.FromSeconds(5));

            _output.WriteLine($"Second migration: IsRunning={secondStatus.IsRunning}, Status={secondStatus.Status}");

            Assert.Contains("already in progress", secondStatus.Status, StringComparison.OrdinalIgnoreCase);
            _output.WriteLine("✓ Concurrent migration correctly rejected");
        }

        // Wait for migration to complete
        DimensionMigrationStatus? finalStatus = null;
        for (int i = 0; i < 30; i++)
        {
            await Task.Delay(500);
            finalStatus = await migrationActor.Ask<DimensionMigrationStatus>(
                new GetDimensionMigrationStatus(),
                TimeSpan.FromSeconds(5));

            if (!finalStatus.IsRunning)
                break;
        }

        Assert.NotNull(finalStatus);
        Assert.False(finalStatus.IsRunning);
    }

    /// <summary>
    /// Full end-to-end dimension migration test.
    /// Tests: schema change, embedding regeneration, config update.
    /// </summary>
    [Fact]
    public async Task DimensionMigration_FullEndToEnd_WithFakeEmbeddings()
    {
        // Reset database to starting dimensions (384)
        await _fixture.ResetDatabaseAsync(
            DimensionMigrationTestFixture.StartingModel,
            DimensionMigrationTestFixture.StartingModelDimensions);

        var storage = Host.Services.GetRequiredService<IStorage>();
        var dimensionService = Host.Services.GetRequiredService<IEmbeddingDimensionService>();

        _output.WriteLine("=== Creating test memories with 384-dimension embeddings ===");

        // Create test memories (fake service returns 768d, but schema is 384d)
        // We need to temporarily set fake service to 384d to create valid memories
        _fakeEmbeddingService!.SetDimensions(DimensionMigrationTestFixture.StartingModelDimensions);

        var testMemories = new List<Memory>();
        for (int i = 0; i < 5; i++)
        {
            var mem = await storage.StoreMemory(
                "test",
                $"Test content {i} for dimension migration",
                "test",
                new[] { "migration-test" },
                new Confidence(1.0),
                $"Migration Test Memory {i}");
            testMemories.Add(mem);
            _output.WriteLine($"Created memory {mem.Id}");
        }

        // Verify memories have 384d embeddings
        foreach (var mem in testMemories)
        {
            var fetched = await storage.Get(mem.Id);
            Assert.NotNull(fetched?.Embedding);
            Assert.Equal(DimensionMigrationTestFixture.StartingModelDimensions, fetched.Embedding.ToArray().Length);
        }
        _output.WriteLine("✓ All memories created with 384-dimension embeddings");

        // Now switch fake service to target dimensions (768)
        _fakeEmbeddingService.SetDimensions(DimensionMigrationTestFixture.TargetModelDimensions);

        // Verify mismatch is detected
        var validation = await dimensionService.ValidateAsync();
        Assert.True(validation.RequiresMigration);
        _output.WriteLine($"✓ Dimension mismatch detected: {validation.MismatchDescription}");

        // Start migration
        _output.WriteLine("\n=== Starting dimension migration ===");
        var migrationActorRef = Host.Services.GetRequiredService<IRequiredActor<DimensionMigrationActorKey>>();
        var migrationActor = await migrationActorRef.GetAsync();

        var startStatus = await migrationActor.Ask<DimensionMigrationStatus>(
            new StartDimensionMigration(RequestedBy: "integration-test"),
            TimeSpan.FromSeconds(30));

        _output.WriteLine($"Migration started: IsRunning={startStatus.IsRunning}, Status={startStatus.Status}");
        _output.WriteLine($"  OldDimensions: {startStatus.OldDimensions}");
        _output.WriteLine($"  NewDimensions: {startStatus.NewDimensions}");
        _output.WriteLine($"  TotalMemories: {startStatus.TotalMemories}");

        if (!startStatus.IsRunning && startStatus.ErrorMessage != null)
        {
            Assert.Fail($"Migration failed to start: {startStatus.ErrorMessage}");
        }

        // Wait for migration to complete
        _output.WriteLine("\n=== Waiting for migration to complete ===");
        DimensionMigrationStatus? finalStatus = null;
        var maxWait = TimeSpan.FromMinutes(1);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < maxWait)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));

            finalStatus = await migrationActor.Ask<DimensionMigrationStatus>(
                new GetDimensionMigrationStatus(),
                TimeSpan.FromSeconds(5));

            _output.WriteLine($"Progress: {finalStatus.Status}, Processed={finalStatus.Processed}/{finalStatus.TotalMemories}");

            if (!finalStatus.IsRunning)
                break;
        }

        Assert.NotNull(finalStatus);
        Assert.False(finalStatus.IsRunning, "Migration should have completed");
        _output.WriteLine($"✓ Migration completed: {finalStatus.Status}");

        // Verify results
        _output.WriteLine("\n=== Verifying migration results ===");

        // Check schema dimensions
        await using var conn = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await conn.OpenAsync();

        await using var schemaCmd = new NpgsqlCommand(@"
            SELECT atttypmod
            FROM pg_attribute
            WHERE attrelid = 'memories'::regclass
            AND attname = 'embedding'", conn);

        var schemaDimensions = Convert.ToInt32(await schemaCmd.ExecuteScalarAsync());
        Assert.Equal(DimensionMigrationTestFixture.TargetModelDimensions, schemaDimensions);
        _output.WriteLine($"✓ Schema updated to {schemaDimensions} dimensions");

        // Check embedding_config
        await using var configCmd = new NpgsqlCommand(@"
            SELECT dimensions FROM embedding_config WHERE is_active = true", conn);
        var configDimensions = Convert.ToInt32(await configCmd.ExecuteScalarAsync());
        Assert.Equal(DimensionMigrationTestFixture.TargetModelDimensions, configDimensions);
        _output.WriteLine($"✓ embedding_config updated to {configDimensions} dimensions");

        // Check all embeddings were regenerated with new dimensions
        foreach (var originalMem in testMemories)
        {
            var regenerated = await storage.Get(originalMem.Id);
            Assert.NotNull(regenerated);
            Assert.NotNull(regenerated.Embedding);

            var newDims = regenerated.Embedding.ToArray().Length;
            Assert.Equal(DimensionMigrationTestFixture.TargetModelDimensions, newDims);
            _output.WriteLine($"✓ Memory {originalMem.Id}: {newDims} dimensions");
        }

        _output.WriteLine("\n=== All tests passed! ===");

        // Clean up
        foreach (var mem in testMemories)
            await storage.Delete(mem.Id);
    }

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // Create the shared fake embedding service with TARGET dimensions
        // (simulates having switched to a new model)
        _fakeEmbeddingService = new FakeEmbeddingService(DimensionMigrationTestFixture.TargetModelDimensions);

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Storage"] = _fixture.PostgresConnectionString,
                ["Versioning:MaxVersionsPerMemory"] = "50"
            })
            .Build());

        // Register the fake embedding service
        services.AddSingleton<FakeEmbeddingService>(_fakeEmbeddingService);
        services.AddSingleton<IEmbeddingService>(_fakeEmbeddingService);

        // Register NpgsqlDataSource
        services.AddSingleton(sp =>
        {
            var sourceBuilder = new NpgsqlDataSourceBuilder(_fixture.PostgresConnectionString);
            sourceBuilder.UseVector();
            return sourceBuilder.Build();
        });

        // Register fake dimension service
        services.AddSingleton<IEmbeddingDimensionService>(sp =>
            new FakeEmbeddingDimensionService(
                sp.GetRequiredService<FakeEmbeddingService>(),
                sp.GetRequiredService<NpgsqlDataSource>(),
                DimensionMigrationTestFixture.TargetModel));

        // Register settings
        services.AddSingleton(new EmbeddingSettings
        {
            ApiUrl = new Uri("http://fake"),
            Model = DimensionMigrationTestFixture.TargetModel,
            Timeout = TimeSpan.FromMinutes(1)
        });

        services.AddSingleton(new VersioningSettings { MaxVersionsPerMemory = 50 });

        // Register dimension mismatch state
        services.AddSingleton<IDimensionMismatchState, DimensionMismatchState>();

        // Register DiffService (required by Storage for versioning)
        services.AddSingleton<IDiffService, DiffService>();

        // Register Storage
        services.AddSingleton<IStorage, Storage>();
    }

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        builder.ConfigureLoggers(logger =>
        {
            logger.ClearLoggers();
            logger.LogLevel = Akka.Event.LogLevel.InfoLevel;
            logger.AddLoggerFactory();
        });

        // Register the actors we need for testing
        builder.WithActors((system, registry, resolver) =>
        {
            // EmbeddingRegenerationActor - needed by DimensionMigrationActor
            var embeddingRegenerationProps = resolver.Props<EmbeddingRegenerationActor>();
            var embeddingRegenerationActor = system.ActorOf(embeddingRegenerationProps, "embedding-regeneration");
            registry.Register<EmbeddingRegenerationActorKey>(embeddingRegenerationActor);

            // DimensionMigrationActor
            var dimensionMigrationProps = resolver.Props<DimensionMigrationActor>();
            var dimensionMigrationActor = system.ActorOf(dimensionMigrationProps, "dimension-migration");
            registry.Register<DimensionMigrationActorKey>(dimensionMigrationActor);
        });
    }
}
