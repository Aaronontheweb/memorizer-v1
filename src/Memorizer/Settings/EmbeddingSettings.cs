namespace Memorizer.Settings;

/// <summary>
/// Settings for the embedding service.
/// Note: Embedding dimensions are auto-detected from the model and stored in the database.
/// See IEmbeddingDimensionService for dimension management.
///
/// These settings can be loaded from:
/// 1. appsettings.json / environment variables (initial load)
/// 2. Database provider_settings table (applied at startup by InitializationService)
///
/// Services should inject IOptionsSnapshot&lt;EmbeddingSettings&gt; to get current values
/// that automatically update when configuration changes.
/// </summary>
public class EmbeddingSettings
{
    public Uri ApiUrl { get; set; } = new("http://localhost:11434");
    public string Model { get; set; } = "all-minilm";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}