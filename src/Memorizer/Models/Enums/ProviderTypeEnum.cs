using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Memorizer.Models.Enums;

/// <summary>
/// Provider type for the bifurcated LLM architecture.
/// Replaces the string constants in ProviderTypes static class.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProviderTypeEnum
{
    /// <summary>
    /// Provider type for embedding/vector generation (e.g., all-minilm).
    /// </summary>
    [EnumMember(Value = "embedding")]
    Embedding,

    /// <summary>
    /// Provider type for the Memorizer Agent (reasoning/analysis LLM).
    /// </summary>
    [EnumMember(Value = "memorizer_agent")]
    MemorizerAgent
}

/// <summary>
/// Extension methods for ProviderTypeEnum.
/// </summary>
public static class ProviderTypeEnumExtensions
{
    /// <summary>
    /// Gets the string value for the enum (as stored in database).
    /// </summary>
    public static string ToStringValue(this ProviderTypeEnum type)
    {
        return type switch
        {
            ProviderTypeEnum.Embedding => "embedding",
            ProviderTypeEnum.MemorizerAgent => "memorizer_agent",
            _ => "embedding"
        };
    }

    /// <summary>
    /// Parses a string value to ProviderTypeEnum.
    /// </summary>
    public static ProviderTypeEnum ParseProviderType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ProviderTypeEnum.Embedding;

        return value.ToLowerInvariant() switch
        {
            "embedding" => ProviderTypeEnum.Embedding,
            "memorizer_agent" or "memorizeragent" or "agent" => ProviderTypeEnum.MemorizerAgent,
            _ => ProviderTypeEnum.Embedding
        };
    }
}
