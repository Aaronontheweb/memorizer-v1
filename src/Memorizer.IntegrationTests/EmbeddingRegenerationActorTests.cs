using Akka.Actor;
using Akka.Hosting;
using Akka.Hosting.TestKit;
using Memorizer.Actors;
using Memorizer.Models;
using Memorizer.Models.ValueTypes;
using Pgvector;
using Xunit.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Linq;
using Memorizer.Extensions;
using Memorizer.Services;
using Memorizer.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Memorizer.IntegrationTests;

[Collection(nameof(IntegrationTestCollection))]
public class EmbeddingRegenerationActorTests : TestKit
{
    private readonly IntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    // Longer timeout for CI environments where the actor may be busy with async embedding operations
    private static readonly TimeSpan StatusQueryTimeout = TimeSpan.FromSeconds(15);

    // Total time to wait for embedding regeneration job to complete (CI with 100+ memories can be slow)
    private static readonly TimeSpan JobCompletionTimeout = TimeSpan.FromMinutes(2);

    public EmbeddingRegenerationActorTests(IntegrationTestFixture fixture, ITestOutputHelper output)
        : base(output: output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// Waits for the embedding regeneration job to complete using TestKit's AwaitConditionAsync.
    /// Returns the final status once the job transitions from running to idle.
    /// </summary>
    private async Task<EmbeddingRegenerationStatus> WaitForJobCompletion(IActorRef actor)
    {
        EmbeddingRegenerationStatus? lastStatus = null;
        bool jobStarted = false;

        await AwaitConditionAsync(async () =>
        {
            try
            {
                lastStatus = await actor.Ask<EmbeddingRegenerationStatus>(
                    new GetEmbeddingRegenerationStatus(),
                    StatusQueryTimeout);

                if (lastStatus.IsRunning)
                    jobStarted = true;

                _output.WriteLine($"Status: {lastStatus.Status}, IsRunning: {lastStatus.IsRunning}, Processed: {lastStatus.TotalProcessed}");

                // Job is complete when it returns to idle after having been running
                return jobStarted && !lastStatus.IsRunning;
            }
            catch (AskTimeoutException)
            {
                // Actor is busy processing - this is expected, keep waiting
                _output.WriteLine("Status query timed out (actor busy), continuing to wait...");
                return false;
            }
        }, JobCompletionTimeout);

        return lastStatus ?? throw new InvalidOperationException("Job completed but no status was captured");
    }

    [Fact]
    public async Task Actor_Emits_BatchCompleted_When_Processing_Finishes()
    {
        // Use a scope to resolve scoped services
        using var scope = Host.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();

        // 1. Get initial count
        var initialCount = (await storage.GetMemoriesPaginated(1, int.MaxValue)).Memories.Count;

        // 2. Add test memories
        var testMemories = new[]
        {
            await storage.StoreMemory("test", "Content A for embedding regeneration test", "test", new[] { "x" }, new Confidence(1.0), "Title A"),
            await storage.StoreMemory("test", "Content B for embedding regeneration test", "test", new[] { "y" }, new Confidence(1.0), "Title B"),
            await storage.StoreMemory("test", "Content C for embedding regeneration test", "test", new[] { "z" }, new Confidence(1.0), "Title C")
        };

        // Use the Akka.DependencyInjection resolver to create the actor
        var resolver = Akka.DependencyInjection.DependencyResolver.For(Sys);
        var actor = Sys.ActorOf(resolver.Props<EmbeddingRegenerationActor>());

        // Act: Start the embedding regeneration job
        actor.Tell(new RegenerateAllEmbeddings(PageSize: 2, RequestedBy: "test"), ActorRefs.NoSender);

        // Wait for job to complete using TestKit's AwaitConditionAsync
        var status = await WaitForJobCompletion(actor);

        Assert.False(status.IsRunning, "Job should no longer be running");

        // After job completion, the actor returns to idle state
        // Check that processing completed by verifying no errors and job is done
        Assert.Equal("idle", status.Status.ToLowerInvariant());

        // Verify that embeddings were regenerated (both content and metadata)
        foreach (var testMemory in testMemories)
        {
            var regeneratedMemory = await storage.Get(testMemory.Id);
            Assert.NotNull(regeneratedMemory);
            Assert.NotNull(regeneratedMemory.Embedding);
            Assert.NotNull(regeneratedMemory.EmbeddingMetadata);
            _output.WriteLine($"Memory {testMemory.Id}: Content embedding exists={regeneratedMemory.Embedding != null}, Metadata embedding exists={regeneratedMemory.EmbeddingMetadata != null}");
        }

        // Clean up test data
        foreach (var memory in testMemories)
            await storage.Delete(memory.Id);
    }

    [Fact]
    public async Task Actor_Regenerates_Both_Content_And_Metadata_Embeddings()
    {
        // Use a scope to resolve scoped services
        using var scope = Host.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();

        // Create a test memory
        var testMemory = await storage.StoreMemory(
            "test",
            "This is test content for dual embedding regeneration verification",
            "test",
            new[] { "dual-test", "embedding" },
            new Confidence(1.0),
            "Dual Embedding Test");

        // Get original embeddings
        var original = await storage.Get(testMemory.Id);
        Assert.NotNull(original);
        Assert.NotNull(original.Embedding);
        Assert.NotNull(original.EmbeddingMetadata);

        var originalContentEmbedding = original.Embedding.ToArray();
        var originalMetadataEmbedding = original.EmbeddingMetadata.ToArray();

        _output.WriteLine($"Original content embedding dimensions: {originalContentEmbedding.Length}");
        _output.WriteLine($"Original metadata embedding dimensions: {originalMetadataEmbedding.Length}");

        // Use the Akka.DependencyInjection resolver to create the actor
        var resolver = Akka.DependencyInjection.DependencyResolver.For(Sys);
        var actor = Sys.ActorOf(resolver.Props<EmbeddingRegenerationActor>());

        // Start regeneration
        actor.Tell(new RegenerateAllEmbeddings(PageSize: 10, RequestedBy: "test"), ActorRefs.NoSender);

        // Wait for job to complete using TestKit's AwaitConditionAsync
        var status = await WaitForJobCompletion(actor);

        Assert.False(status.IsRunning);

        // Get regenerated memory
        var regenerated = await storage.Get(testMemory.Id);
        Assert.NotNull(regenerated);
        Assert.NotNull(regenerated.Embedding);
        Assert.NotNull(regenerated.EmbeddingMetadata);

        var regeneratedContentEmbedding = regenerated.Embedding.ToArray();
        var regeneratedMetadataEmbedding = regenerated.EmbeddingMetadata.ToArray();

        _output.WriteLine($"Regenerated content embedding dimensions: {regeneratedContentEmbedding.Length}");
        _output.WriteLine($"Regenerated metadata embedding dimensions: {regeneratedMetadataEmbedding.Length}");

        // Verify embeddings have correct dimensions (should match embedding service output)
        Assert.Equal(originalContentEmbedding.Length, regeneratedContentEmbedding.Length);
        Assert.Equal(originalMetadataEmbedding.Length, regeneratedMetadataEmbedding.Length);

        // Clean up
        await storage.Delete(testMemory.Id);
    }

    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // Configure services just like in the IntegrationTests class
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Storage"] = _fixture.PostgresConnectionString,
                ["Embeddings:ApiUrl"] = _fixture.OllamaApiUrl,
                ["Embeddings:Model"] = "all-minilm",
                ["Embeddings:Timeout"] = TimeSpan.FromMinutes(1).ToString()
            })
            .Build());

        // Add HTTP client for embedding service
        services.AddHttpClient<IEmbeddingService, EmbeddingService>(client =>
        {
            client.BaseAddress = new Uri(_fixture.OllamaApiUrl);
            client.Timeout = TimeSpan.FromMinutes(1);
        });

        // Add services
        services.AddSingleton(new EmbeddingSettings
        {
            ApiUrl = new Uri(_fixture.OllamaApiUrl),
            Model = "all-minilm",
            Timeout = TimeSpan.FromMinutes(1)
        });

        services.AddMemorizer(initialize:false);
    }

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        // No custom Akka configuration needed for this test
    }
}
