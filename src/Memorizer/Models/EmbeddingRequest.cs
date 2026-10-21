using System.Text.Json.Serialization;

namespace Memorizer.Models;

/// <summary>
/// Request body for Ollama's <c>/api/embed</c> endpoint.
/// </summary>
public class EmbeddingRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("input")]
    public string Input { get; init; } = string.Empty;

    /// <summary>
    /// Truncate input that exceeds the model's context length instead of failing the
    /// request. Ollama's default is already true; it is sent explicitly so the behaviour
    /// does not depend on the server version.
    /// </summary>
    [JsonPropertyName("truncate")]
    public bool Truncate { get; init; } = true;
}
