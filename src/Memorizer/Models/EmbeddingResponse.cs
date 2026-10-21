using System.Text.Json.Serialization;

namespace Memorizer.Models;

/// <summary>
/// Response body from Ollama's <c>/api/embed</c> endpoint: one vector per input.
/// </summary>
public class EmbeddingResponse
{
    [JsonPropertyName("embeddings")]
    public float[][] Embeddings { get; init; } = [];
}
