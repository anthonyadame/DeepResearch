using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services.VectorDatabase;

/// <summary>
/// Service for generating embeddings from text content.
/// Can be implemented with various embedding models (SentenceTransformers, OpenAI, etc.)
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generate an embedding vector for the given text.
    /// </summary>
    Task<float[]> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embeddings for multiple texts in batch.
    /// </summary>
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the dimension of embeddings produced by this service.
    /// </summary>
    int GetEmbeddingDimension();
}

/// <summary>
/// Simple embedding service using a local embedding model via HTTP (e.g., Ollama embeddings endpoint).
/// </summary>
public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly int _dimension;
    private readonly Microsoft.Extensions.Logging.ILogger? _logger;

    public OllamaEmbeddingService(
        HttpClient httpClient,
        string baseUrl = "http://localhost:11434",
        string model = "nomic-embed-text",
        int dimension = 384,
        Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
        _dimension = dimension;
        _logger = logger;
    }

    public async Task<float[]> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Generating embedding for text of length {length}", text.Length);

            var request = new
            {
                model = _model,
                prompt = text
            };

            var url = $"{_baseUrl}/api/embeddings";
            var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = System.Text.Json.JsonSerializer.Deserialize<OllamaEmbeddingResponse>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (result?.Embedding == null || result.Embedding.Length == 0)
            {
                throw new InvalidOperationException("No embedding returned from Ollama");
            }

            _logger?.LogDebug("Generated embedding with dimension {dim}", result.Embedding.Length);
            return result.Embedding;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate embedding");
            throw;
        }
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var embeddings = new List<float[]>();
        
        foreach (var text in texts)
        {
            var embedding = await EmbedAsync(text, cancellationToken);
            embeddings.Add(embedding);
        }

        return embeddings;
    }

    public int GetEmbeddingDimension() => _dimension;

    private class OllamaEmbeddingResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}
