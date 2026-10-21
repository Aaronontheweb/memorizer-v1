using System.Net.Http.Headers;
using System.Net.Http.Json;
using Memorizer.Models;
using Memorizer.Settings;
using Microsoft.Extensions.Options;

namespace Memorizer.Services;

/// <summary>
/// Sends embedding requests to the configured provider and normalises the response
/// to a <c>float[]</c>. Supports Ollama's native <c>/api/embed</c> shape and the
/// OpenAI-compatible <c>/v1/embeddings</c> shape (also exposed by LiteLLM, vLLM, Azure
/// OpenAI, LocalAI, etc.).
/// </summary>
public interface IEmbeddingApiClient
{
    Task<float[]> GenerateAsync(string model, string text, CancellationToken cancellationToken = default);
}

public sealed class EmbeddingApiClient : IEmbeddingApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsSnapshot<EmbeddingSettings> _settingsSnapshot;
    private readonly ILogger<EmbeddingApiClient> _logger;

    private EmbeddingSettings Settings => _settingsSnapshot.Value;

    public EmbeddingApiClient(
        HttpClient httpClient,
        IOptionsSnapshot<EmbeddingSettings> settingsSnapshot,
        ILogger<EmbeddingApiClient> logger)
    {
        _httpClient = httpClient;
        _settingsSnapshot = settingsSnapshot;
        _logger = logger;

        _httpClient.BaseAddress = Settings.ApiUrl;
        _httpClient.Timeout = Settings.Timeout;
    }

    public async Task<float[]> GenerateAsync(string model, string text, CancellationToken cancellationToken = default)
    {
        var provider = Settings.Provider;

        if (string.Equals(provider, ProviderNames.OpenAI, StringComparison.OrdinalIgnoreCase))
        {
            return await GenerateOpenAIAsync(model, text, cancellationToken);
        }

        return await GenerateOllamaAsync(model, text, cancellationToken);
    }

    private async Task<float[]> GenerateOllamaAsync(string model, string text, CancellationToken cancellationToken)
    {
        // /api/embed rather than the legacy /api/embeddings: the legacy endpoint rejects
        // input longer than the model's context window ("the input length exceeds the
        // context length"), which made storing any long memory fail with small-context
        // models such as all-minilm (256 tokens). /api/embed truncates instead. Both
        // return the same direction (it is L2-normalised), and similarity search uses
        // cosine distance, so existing stored embeddings stay compatible.
        var request = new EmbeddingRequest { Model = model, Input = text };

        _logger.LogDebug("Sending Ollama embedding request to {ApiUrl}", Settings.ApiUrl);
        var response = await _httpClient.PostAsJsonAsync("api/embed", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: cancellationToken);
        var first = result?.Embeddings.FirstOrDefault();
        if (first is null || first.Length == 0)
        {
            throw new InvalidOperationException("Empty embedding response from Ollama API");
        }

        return first;
    }

    private async Task<float[]> GenerateOpenAIAsync(string model, string text, CancellationToken cancellationToken)
    {
        var request = new OpenAIEmbeddingRequest { Model = model, Input = text };
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/embeddings")
        {
            Content = JsonContent.Create(request)
        };

        if (!string.IsNullOrWhiteSpace(Settings.ApiKey))
        {
            httpRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", Settings.ApiKey);
        }

        _logger.LogDebug("Sending OpenAI-compatible embedding request to {ApiUrl}", Settings.ApiUrl);
        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>(cancellationToken: cancellationToken);
        var first = result?.Data.FirstOrDefault();
        if (first?.Embedding is null || first.Embedding.Length == 0)
        {
            throw new InvalidOperationException("Empty embedding response from OpenAI-compatible API");
        }

        return first.Embedding;
    }

    /// <summary>
    /// Like <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>, but reads the
    /// response body on failure and includes it in the thrown exception. Embedding
    /// backends put the real reason there (e.g. Ollama's "the input length exceeds the
    /// context length"); the default helper discards it, leaving only a bare status code.
    /// Preserving it lets tools give the caller an honest, actionable failure.
    /// </summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        string body;
        try
        {
            body = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
        }
        catch
        {
            body = string.Empty;
        }

        if (body.Length > 500)
            body = body[..500] + "…";

        var detail = string.IsNullOrEmpty(body) ? string.Empty : $": {body}";
        throw new HttpRequestException(
            $"Embedding request failed with status {(int)response.StatusCode} ({response.StatusCode}){detail}",
            inner: null,
            statusCode: response.StatusCode);
    }
}
