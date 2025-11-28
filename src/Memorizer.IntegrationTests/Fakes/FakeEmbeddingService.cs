using System.Text.Json;
using Memorizer.Services;

namespace Memorizer.IntegrationTests.Fakes;

/// <summary>
/// Fake embedding service that returns deterministic embeddings of configurable dimensions.
/// Used for testing dimension migration without requiring actual embedding models.
/// </summary>
public class FakeEmbeddingService : IEmbeddingService
{
    private int _dimensions;
    private int _callCount = 0;

    public FakeEmbeddingService(int dimensions = 384)
    {
        _dimensions = dimensions;
    }

    /// <summary>
    /// Change the dimensions returned by this fake service.
    /// Used to simulate switching to a different embedding model.
    /// </summary>
    public void SetDimensions(int dimensions)
    {
        _dimensions = dimensions;
    }

    /// <summary>
    /// Current dimensions being returned.
    /// </summary>
    public int CurrentDimensions => _dimensions;

    /// <summary>
    /// Number of times Generate has been called.
    /// </summary>
    public int CallCount => _callCount;

    /// <summary>
    /// Reset the call counter.
    /// </summary>
    public void ResetCallCount() => _callCount = 0;

    public Task<float[]> Generate(string text, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _callCount);

        // Generate deterministic embedding based on text hash
        // This ensures same text always gets same embedding
        var hash = text.GetHashCode();
        var random = new Random(hash);

        var embedding = new float[_dimensions];
        for (int i = 0; i < _dimensions; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1); // Values between -1 and 1
        }

        // Normalize to unit vector (common for embeddings)
        var magnitude = MathF.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < _dimensions; i++)
        {
            embedding[i] /= magnitude;
        }

        return Task.FromResult(embedding);
    }

    public Task<float[]> Generate(JsonDocument document, CancellationToken cancellationToken = default)
    {
        // Convert document to string and use the string-based method
        var text = document.RootElement.ToString();
        return Generate(text, cancellationToken);
    }
}
