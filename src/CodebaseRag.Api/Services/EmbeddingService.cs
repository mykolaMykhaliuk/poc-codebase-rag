using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodebaseRag.Api.Configuration;
using Microsoft.Extensions.Options;

namespace CodebaseRag.Api.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingSettings _settings;
    private readonly ILogger<EmbeddingService> _logger;

    public int Dimensions => _settings.Dimensions;
    public string ModelName => _settings.Model;

    public EmbeddingService(
        HttpClient httpClient,
        IOptions<RagSettings> settings,
        ILogger<EmbeddingService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value.Embedding;
        _logger = logger;

        // Configure base URL
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

        // Add authorization header if API key is provided
        if (!string.IsNullOrEmpty(_settings.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var results = await EmbedBatchAsync(new[] { text }, cancellationToken);
        return results.First();
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        if (textList.Count == 0)
            return Array.Empty<float[]>();

        var results = new List<float[]>();
        var batches = textList
            .Select((text, index) => new { text, index })
            .GroupBy(x => x.index / _settings.BatchSize)
            .Select(g => g.Select(x => x.text).ToList())
            .ToList();

        foreach (var batch in batches)
        {
            var batchResults = await EmbedBatchInternalAsync(batch, cancellationToken);
            results.AddRange(batchResults);
        }

        return results;
    }

    private async Task<IReadOnlyList<float[]>> EmbedBatchInternalAsync(
        List<string> texts,
        CancellationToken cancellationToken)
    {
        var request = new EmbeddingRequest
        {
            Model = _settings.Model,
            Input = texts
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "embeddings",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(
                cancellationToken: cancellationToken);

            if (result?.Data == null || result.Data.Count != texts.Count)
            {
                throw new InvalidOperationException(
                    $"Expected {texts.Count} embeddings but got {result?.Data?.Count ?? 0}");
            }

            return result.Data
                .OrderBy(d => d.Index)
                .Select(d => d.Embedding)
                .ToList();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get embeddings from {Provider}", _settings.Provider);
            throw new InvalidOperationException($"Embedding API error: {ex.Message}", ex);
        }
    }

    private class EmbeddingRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("input")]
        public required List<string> Input { get; set; }
    }

    private class EmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingData> Data { get; set; } = new();
    }

    private class EmbeddingData
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
