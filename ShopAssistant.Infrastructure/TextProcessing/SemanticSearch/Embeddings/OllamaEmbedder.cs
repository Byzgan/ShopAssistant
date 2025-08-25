namespace ShopAssistant.Infrastructure.TextProcessing.SemanticSearch.Embeddings;

using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ShopAssistant.Contracts.Interfaces.TextProcessing;
using Contracts.Config;

/// <summary>
/// Provides neural embedding generation by calling a local Ollama LLM API.
/// </summary>
public class OllamaEmbedder : ITextEmbedder
{
    private readonly HttpClient _client;
    private readonly OllamaOptions _options;

    /// <summary>
    /// Initializes the embedder with the specified API options and HttpClient.
    /// </summary>
    /// <param name="options">Ollama model/API options.</param>
    /// <param name="client">HTTP client for sending requests.</param>
    public OllamaEmbedder(IOptions<OllamaOptions> options, HttpClient client)
    {
        _options = options.Value;
        _client = client;
    }

    /// <summary>
    /// Generates a neural embedding vector for the given text using Ollama API.
    /// </summary>
    /// <param name="text">The text to embed.</param>
    /// <returns>Float array with the embedding vector.</returns>
    /// <exception cref="InvalidOperationException">If API response does not contain the expected 'embedding' property.</exception>
    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var payload = new { model = _options.ModelName, prompt = text };
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(_options.ApiUrl, content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(result);

        if (!doc.RootElement.TryGetProperty("embedding", out var embArray))
            throw new InvalidOperationException("The API response does not contain an 'embedding' property.");

        var embedding = new float[embArray.GetArrayLength()];
        int idx = 0;
        foreach (var elem in embArray.EnumerateArray())
            embedding[idx++] = elem.GetSingle();

        return embedding;
    }
}
