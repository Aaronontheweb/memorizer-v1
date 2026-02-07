namespace Memorizer.Settings;

/// <summary>
/// Settings for LLM (Large Language Model) services used by the Memorizer Agent.
///
/// These settings can be loaded from:
/// 1. appsettings.json / environment variables (initial load)
/// 2. Database provider_settings table (applied at startup by InitializationService)
///
/// Services should inject IOptionsSnapshot&lt;LlmSettings&gt; to get current values
/// that automatically update when configuration changes.
/// </summary>
public sealed class LlmSettings
{
    /// <summary>
    /// API URL for the LLM service (e.g., Ollama)
    /// </summary>
    public Uri ApiUrl { get; set; } = new("http://localhost:11434");

    /// <summary>
    /// Model name to use for LLM operations
    /// </summary>
    public string Model { get; set; } = "qwen2:0.5b";

    /// <summary>
    /// Timeout for LLM requests
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
} 