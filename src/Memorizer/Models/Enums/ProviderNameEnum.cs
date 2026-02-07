using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Memorizer.Models.Enums;

/// <summary>
/// Known LLM provider names.
/// Replaces the string constants in ProviderNames static class.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProviderNameEnum
{
    /// <summary>
    /// Ollama - local LLM inference.
    /// </summary>
    [EnumMember(Value = "ollama")]
    Ollama,

    /// <summary>
    /// Anthropic - Claude models.
    /// </summary>
    [EnumMember(Value = "anthropic")]
    Anthropic,

    /// <summary>
    /// OpenAI - GPT models.
    /// </summary>
    [EnumMember(Value = "openai")]
    OpenAI,

    /// <summary>
    /// Custom provider for extensibility.
    /// </summary>
    [EnumMember(Value = "custom")]
    Custom
}

/// <summary>
/// Extension methods for ProviderNameEnum.
/// </summary>
public static class ProviderNameEnumExtensions
{
    /// <summary>
    /// Gets the string value for the enum (as stored in database).
    /// </summary>
    public static string ToStringValue(this ProviderNameEnum name)
    {
        return name switch
        {
            ProviderNameEnum.Ollama => "ollama",
            ProviderNameEnum.Anthropic => "anthropic",
            ProviderNameEnum.OpenAI => "openai",
            ProviderNameEnum.Custom => "custom",
            _ => "ollama"
        };
    }

    /// <summary>
    /// Parses a string value to ProviderNameEnum.
    /// </summary>
    public static ProviderNameEnum ParseProviderName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ProviderNameEnum.Ollama;

        return value.ToLowerInvariant() switch
        {
            "ollama" => ProviderNameEnum.Ollama,
            "anthropic" or "claude" => ProviderNameEnum.Anthropic,
            "openai" or "gpt" => ProviderNameEnum.OpenAI,
            "custom" => ProviderNameEnum.Custom,
            _ => ProviderNameEnum.Custom
        };
    }
}
