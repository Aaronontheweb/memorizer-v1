using Memorizer.Settings;
using Npgsql;
using Registrator.Net;

namespace Memorizer.Services;

public interface IMemoryStatsService
{
    /// <summary>
    /// Gets statistics about the memory storage
    /// </summary>
    Task<MemoryStats> GetStatsAsync(CancellationToken cancellationToken = default);
}

public class MemoryStats
{
    /// <summary>
    /// Total count of memories in the database
    /// </summary>
    public int TotalMemories { get; set; }
    
    /// <summary>
    /// Average size of memory content in bytes
    /// </summary>
    public long AverageMemorySizeBytes { get; set; }
    
    /// <summary>
    /// Configured embedding dimensions
    /// </summary>
    public int EmbeddingDimensions { get; set; }
}

[AutoRegisterInterfaces(ServiceLifetime.Singleton)]
public class MemoryStatsService : IMemoryStatsService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly EmbeddingSettings _embeddingSettings;
    
    public MemoryStatsService(NpgsqlDataSource dataSource, EmbeddingSettings embeddingSettings)
    {
        _dataSource = dataSource;
        _embeddingSettings = embeddingSettings;
    }
    
    public async Task<MemoryStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        int totalMemories = 0;
        long avgSizeBytes = 0;
        
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        
        // Get count of memories
        await using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM memories", connection))
        {
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            totalMemories = Convert.ToInt32(result);
        }
        
        // Get average size (text field)
        await using (var cmd = new NpgsqlCommand("SELECT AVG(LENGTH(text)) FROM memories", connection))
        {
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result != DBNull.Value && result != null)
            {
                avgSizeBytes = Convert.ToInt64(result);
            }
        }
        
        return new MemoryStats
        {
            TotalMemories = totalMemories,
            AverageMemorySizeBytes = avgSizeBytes,
            EmbeddingDimensions = _embeddingSettings.Dimensions
        };
    }
} 