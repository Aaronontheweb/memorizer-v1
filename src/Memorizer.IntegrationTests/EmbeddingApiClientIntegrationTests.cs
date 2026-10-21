using Memorizer.Extensions;
using Memorizer.IntegrationTests.Logging;
using Memorizer.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Memorizer.IntegrationTests;

/// <summary>
/// Runs the Ollama embedding client against a real Ollama server, to check that input
/// longer than the model's context window is truncated rather than rejected.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class EmbeddingApiClientIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public EmbeddingApiClientIntegrationTests(IntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private ServiceProvider CreateServices(string model = "all-minilm")
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Storage"] = _fixture.PostgresConnectionString,
                ["Embeddings:ApiUrl"] = _fixture.OllamaApiUrl,
                ["Embeddings:Model"] = model,
                ["Embeddings:Timeout"] = TimeSpan.FromMinutes(1).ToString()
            })
            .Build());

        services.AddMemorizer();
        services.AddLogging(builder => builder.AddXUnit(_output, LogLevel.Debug));

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task GenerateAsync_InputLongerThanContextWindow_IsTruncatedNotRejected()
    {
        // all-minilm has a 256-token context window. This is several thousand tokens,
        // which the legacy /api/embeddings endpoint rejects with "the input length
        // exceeds the context length".
        var longText = string.Concat(Enumerable.Repeat(
            "fastfetch was stuck at 2.66.0 for weeks because the hourly build kept failing. ", 200));

        using var services = CreateServices();
        using var scope = services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IEmbeddingApiClient>();

        var embedding = await client.GenerateAsync("all-minilm", longText);

        Assert.Equal(384, embedding.Length);
    }

    [Fact]
    public async Task GenerateAsync_ShortInput_ReturnsUnitLengthVector()
    {
        using var services = CreateServices();
        using var scope = services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IEmbeddingApiClient>();

        var embedding = await client.GenerateAsync("all-minilm", "hello world");

        // /api/embed returns L2-normalised vectors; cosine similarity is unaffected.
        var norm = Math.Sqrt(embedding.Sum(x => (double)x * x));
        Assert.Equal(384, embedding.Length);
        Assert.InRange(norm, 0.999, 1.001);
    }
}
