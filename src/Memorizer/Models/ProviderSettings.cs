using System.Text.Json;

namespace Memorizer.Models;

/// <summary>
/// Represents configuration settings for an LLM provider.
/// Supports bifurcated architecture with separate embedding and memorizer agent providers.
/// </summary>
public class ProviderSettings
{
    public ProviderSettingsId Id { get; init; }

    /// <summary>
    /// Type of provider: 'embedding' or 'memorizer_agent'
    /// </summary>
    public string ProviderType { get; init; } = string.Empty;

    /// <summary>
    /// Provider identifier: 'ollama', 'anthropic', 'openai', etc.
    /// </summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable display name for UI
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Provider-specific configuration (apiUrl, model, timeout, etc.)
    /// </summary>
    public JsonDocument Config { get; init; } = JsonDocument.Parse("{}");

    /// <summary>
    /// Whether this provider is currently active for its type.
    /// Only one provider per type can be active.
    /// </summary>
    public bool IsActive { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Constants for provider types used in the bifurcated architecture.
/// </summary>
public static class ProviderTypes
{
    /// <summary>
    /// Provider type for embedding/vector generation (e.g., all-minilm)
    /// </summary>
    public const string Embedding = "embedding";

    /// <summary>
    /// Provider type for the Memorizer Agent (reasoning/analysis LLM)
    /// </summary>
    public const string MemorizerAgent = "memorizer_agent";
}

/// <summary>
/// Constants for known provider names.
/// </summary>
public static class ProviderNames
{
    public const string Ollama = "ollama";
    public const string Anthropic = "anthropic";
    public const string OpenAI = "openai";
}
