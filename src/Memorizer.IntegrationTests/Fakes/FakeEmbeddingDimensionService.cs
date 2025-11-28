using Memorizer.Models;
using Memorizer.Services;
using Npgsql;

namespace Memorizer.IntegrationTests.Fakes;

/// <summary>
/// Fake embedding dimension service that uses FakeEmbeddingService for dimension detection
/// but performs real database operations for schema/config validation.
/// </summary>
public class FakeEmbeddingDimensionService : IEmbeddingDimensionService
{
    private readonly FakeEmbeddingService _fakeEmbeddingService;
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _configuredModel;

    public FakeEmbeddingDimensionService(
        FakeEmbeddingService fakeEmbeddingService,
        NpgsqlDataSource dataSource,
        string configuredModel = "fake-model")
    {
        _fakeEmbeddingService = fakeEmbeddingService;
        _dataSource = dataSource;
        _configuredModel = configuredModel;
    }

    public Task<int?> ProbeModelDimensionsAsync(CancellationToken ct = default)
    {
        // Return the fake service's current dimensions
        return Task.FromResult<int?>(_fakeEmbeddingService.CurrentDimensions);
    }

    public async Task<EmbeddingConfigRecord?> GetActiveConfigAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT model_name, dimensions, detected_at, is_active
            FROM embedding_config
            WHERE is_active = true
            ORDER BY detected_at DESC
            LIMIT 1";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (await reader.ReadAsync(ct))
        {
            return new EmbeddingConfigRecord
            {
                ModelName = reader.GetString(0),
                Dimensions = reader.GetInt32(1),
                DetectedAt = reader.GetDateTime(2),
                IsActive = reader.GetBoolean(3)
            };
        }

        return null;
    }

    public async Task<int?> GetDatabaseSchemaDimensionsAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT atttypmod
            FROM pg_attribute
            WHERE attrelid = 'memories'::regclass
            AND attname = 'embedding'";

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result != null && result != DBNull.Value)
        {
            return Convert.ToInt32(result);
        }

        return null;
    }

    public async Task<DimensionValidationResult> ValidateAsync(CancellationToken ct = default)
    {
        var detectedDimensions = await ProbeModelDimensionsAsync(ct);
        var storedConfig = await GetActiveConfigAsync(ct);
        var schemaDimensions = await GetDatabaseSchemaDimensionsAsync(ct);

        var requiresMigration = false;
        string? mismatchDescription = null;

        // Check if detected dimensions differ from stored config
        if (detectedDimensions.HasValue && storedConfig != null)
        {
            if (detectedDimensions.Value != storedConfig.Dimensions)
            {
                requiresMigration = true;
                mismatchDescription = $"Model outputs {detectedDimensions.Value} dimensions but config has {storedConfig.Dimensions}";
            }
        }
        // Check if detected dimensions differ from schema
        else if (detectedDimensions.HasValue && schemaDimensions.HasValue)
        {
            if (detectedDimensions.Value != schemaDimensions.Value)
            {
                requiresMigration = true;
                mismatchDescription = $"Model outputs {detectedDimensions.Value} dimensions but schema has {schemaDimensions.Value}";
            }
        }

        return new DimensionValidationResult(
            ConfiguredModel: _configuredModel,
            DetectedModelDimensions: detectedDimensions,
            StoredDimensions: storedConfig?.Dimensions,
            StoredModel: storedConfig?.ModelName,
            DatabaseSchemaDimensions: schemaDimensions,
            HasMismatch: requiresMigration,
            MismatchDescription: mismatchDescription,
            RequiresMigration: requiresMigration,
            EmbeddingApiAvailable: true
        );
    }

    public async Task UpdateActiveConfigAsync(string modelName, int dimensions, CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        // Deactivate existing
        await using (var deactivateCmd = new NpgsqlCommand(
            "UPDATE embedding_config SET is_active = false WHERE is_active = true", conn))
        {
            await deactivateCmd.ExecuteNonQueryAsync(ct);
        }

        // Insert new active config
        await using var insertCmd = new NpgsqlCommand(@"
            INSERT INTO embedding_config (model_name, dimensions, is_active, detected_at)
            VALUES (@model, @dimensions, true, NOW())", conn);
        insertCmd.Parameters.AddWithValue("model", modelName);
        insertCmd.Parameters.AddWithValue("dimensions", dimensions);
        await insertCmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> GetEffectiveDimensionsAsync(CancellationToken ct = default)
    {
        var detected = await ProbeModelDimensionsAsync(ct);
        if (detected.HasValue) return detected.Value;

        var stored = await GetActiveConfigAsync(ct);
        if (stored != null) return stored.Dimensions;

        var schema = await GetDatabaseSchemaDimensionsAsync(ct);
        if (schema.HasValue) return schema.Value;

        return 384; // Default fallback
    }
}
