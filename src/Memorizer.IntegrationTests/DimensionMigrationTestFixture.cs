using Memorizer.Services;
using Testcontainers.PostgreSql;

namespace Memorizer.IntegrationTests;

/// <summary>
/// Collection definition for dimension migration tests.
/// Uses its own dedicated PostgreSQL container to avoid interfering with other tests.
/// Does NOT use Ollama - uses fake embedding services instead for fast, deterministic testing.
/// </summary>
[CollectionDefinition(nameof(DimensionMigrationTestCollection))]
public class DimensionMigrationTestCollection : ICollectionFixture<DimensionMigrationTestFixture> { }

/// <summary>
/// Test fixture for dimension migration integration tests.
/// Provides an isolated PostgreSQL container WITHOUT Ollama.
/// Uses FakeEmbeddingService for fast, deterministic dimension migration testing.
/// </summary>
public class DimensionMigrationTestFixture : IAsyncLifetime
{
    public readonly PostgreSqlContainer PostgresContainer;

    // Simulated model dimensions for testing
    public const string StartingModel = "fake-model-384d";
    public const int StartingModelDimensions = 384;
    public const string TargetModel = "fake-model-768d";
    public const int TargetModelDimensions = 768;

    // Limit pool size to prevent connection exhaustion.
    // Test classes must implement IDisposable to properly release connections.
    public string PostgresConnectionString =>
        PostgresContainer.GetConnectionString() + ";Maximum Pool Size=10;Minimum Pool Size=0;";

    public DimensionMigrationTestFixture()
    {
        // Create dedicated PostgreSQL container for dimension migration tests
        PostgresContainer = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg17")
            .WithCleanUp(true)
            .WithPortBinding(5432, true) // Random host port
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithDatabase("dimension_migration_test")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await PostgresContainer.StartAsync();

        // Run database migrations
        await SchemaMigrator.MigrateAsync(PostgresConnectionString);
    }

    /// <summary>
    /// Reset the database to a clean state for a new test.
    /// Clears all memories and resets embedding_config to specified dimensions.
    /// </summary>
    public async Task ResetDatabaseAsync(string model, int dimensions)
    {
        await using var conn = new Npgsql.NpgsqlConnection(PostgresConnectionString);
        await conn.OpenAsync();

        // Clear all memories
        await using (var cmd = new Npgsql.NpgsqlCommand("DELETE FROM memories", conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // Clear memory versions
        await using (var cmd = new Npgsql.NpgsqlCommand("DELETE FROM memory_versions", conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // Clear migration history
        await using (var cmd = new Npgsql.NpgsqlCommand("DELETE FROM embedding_dimension_migrations", conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // Reset embedding_config - deactivate any existing active config and insert new one
        await using (var cmd = new Npgsql.NpgsqlCommand(
            "UPDATE embedding_config SET is_active = false WHERE is_active = true", conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = new Npgsql.NpgsqlCommand(@"
            INSERT INTO embedding_config (model_name, dimensions, is_active, detected_at)
            VALUES (@model, @dimensions, true, NOW())", conn))
        {
            cmd.Parameters.AddWithValue("model", model);
            cmd.Parameters.AddWithValue("dimensions", dimensions);
            await cmd.ExecuteNonQueryAsync();
        }

        // Ensure schema matches the specified dimensions
        try
        {
            // Drop indexes first (required for pgvector ALTER)
            await using (var cmd = new Npgsql.NpgsqlCommand(
                "DROP INDEX IF EXISTS idx_memories_embedding_cosine", conn))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new Npgsql.NpgsqlCommand(
                "DROP INDEX IF EXISTS idx_memories_embedding_metadata_cosine", conn))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // Alter columns to match specified dimensions
            await using (var cmd = new Npgsql.NpgsqlCommand(
                $"ALTER TABLE memories ALTER COLUMN embedding TYPE VECTOR({dimensions})", conn))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new Npgsql.NpgsqlCommand(
                $"ALTER TABLE memories ALTER COLUMN embedding_metadata TYPE VECTOR({dimensions})", conn))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // Recreate indexes
            await using (var cmd = new Npgsql.NpgsqlCommand(
                $"CREATE INDEX IF NOT EXISTS idx_memories_embedding_cosine ON memories USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100)", conn))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new Npgsql.NpgsqlCommand(
                $"CREATE INDEX IF NOT EXISTS idx_memories_embedding_metadata_cosine ON memories USING ivfflat (embedding_metadata vector_cosine_ops) WITH (lists = 100)", conn))
            {
                await cmd.ExecuteNonQueryAsync();
            }
        }
        catch
        {
            // Ignore schema errors - table might already be at the right dimensions
        }
    }

    public async Task DisposeAsync()
    {
        await PostgresContainer.DisposeAsync();
    }
}
