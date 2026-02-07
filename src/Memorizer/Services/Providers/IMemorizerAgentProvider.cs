namespace Memorizer.Services.Providers;

/// <summary>
/// Interface for the Memorizer Agent - the reasoning/analysis LLM provider.
/// This is used for tasks like title generation, content analysis, and future
/// "What's Next?" workflow recommendations.
///
/// Separate from IEmbeddingService which handles vector embedding generation.
/// </summary>
public interface IMemorizerAgentProvider : IDisposable
{
    /// <summary>
    /// Gets the provider name (e.g., "ollama", "anthropic", "openai")
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the display name for UI purposes
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Tests connectivity to the LLM service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating if the service is available and configured</returns>
    Task<MemorizerAgentHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a descriptive title for content that doesn't have one
    /// </summary>
    /// <param name="content">The text content to generate a title for</param>
    /// <param name="contentType">Type of content (e.g., "reference", "how-to")</param>
    /// <param name="existingTags">Existing tags for context</param>
    /// <param name="maxTitleLength">Maximum length for the generated title</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated title</returns>
    Task<string> GenerateTitleAsync(
        string content,
        string contentType,
        string[]? existingTags = null,
        int maxTitleLength = 80,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Result of Memorizer Agent health check
/// </summary>
public class MemorizerAgentHealthResult
{
    public bool IsHealthy { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? ModelName { get; init; }
    public TimeSpan? ResponseTime { get; init; }
    public string? ErrorDetails { get; init; }
}
