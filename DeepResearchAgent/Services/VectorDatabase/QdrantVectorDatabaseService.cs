using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services.VectorDatabase;

/// <summary>
/// Configuration options for Qdrant vector database connection.
/// </summary>
public class QdrantConfig
{
    /// <summary>Base URL of Qdrant server (e.g., http://localhost:6333)</summary>
    public string BaseUrl { get; set; } = "http://localhost:6333";

    /// <summary>API key for authentication (optional).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Collection name to use in Qdrant.</summary>
    public string CollectionName { get; set; } = "research";

    /// <summary>Vector dimension size (should match embedding model output).</summary>
    public int VectorDimension { get; set; } = 384; // Default for small models

    /// <summary>Connection timeout in milliseconds.</summary>
    public int TimeoutMs { get; set; } = 30000;
}

/// <summary>
/// Qdrant vector database implementation.
/// Qdrant is an open-source, production-grade vector similarity search engine.
/// See: https://qdrant.tech/
/// </summary>
public class QdrantVectorDatabaseService : IVectorDatabaseService
{
    private readonly HttpClient _httpClient;
    private readonly QdrantConfig _config;
    private readonly Microsoft.Extensions.Logging.ILogger? _logger;
    private readonly IEmbeddingService _embeddingService;

    public string Name => "Qdrant";

    public QdrantVectorDatabaseService(
        HttpClient httpClient,
        QdrantConfig config,
        IEmbeddingService embeddingService,
        Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _logger = logger;
    }

    public async Task<string> UpsertAsync(
        string id,
        string content,
        float[] embedding,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Upserting document {id} to Qdrant collection {collection}", 
                id, _config.CollectionName);

            if (embedding.Length != _config.VectorDimension)
            {
                _logger?.LogWarning("Embedding dimension mismatch. Expected {expected}, got {actual}",
                    _config.VectorDimension, embedding.Length);
            }

            var payload = new Dictionary<string, object>
            {
                ["content"] = content
            };

            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    payload[kvp.Key] = kvp.Value;
                }
            }

            var pointRequest = new QdrantPointRequest
            {
                Points = new[]
                {
                    new QdrantPoint
                    {
                        Id = id,
                        Vector = embedding,
                        Payload = payload
                    }
                }
            };

            var url = $"{_config.BaseUrl}/collections/{_config.CollectionName}/points";
            var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = JsonContent.Create(pointRequest)
            };

            if (!string.IsNullOrEmpty(_config.ApiKey))
            {
                request.Headers.Add("api-key", _config.ApiKey);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger?.LogDebug("Successfully upserted document {id}", id);
            return id;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to upsert document {id} to Qdrant", id);
            throw;
        }
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        float[] embedding,
        int topK = 5,
        double? scoreThreshold = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Searching Qdrant with embedding vector (topK: {topK})", topK);

            var searchRequest = new QdrantSearchRequest
            {
                Vector = embedding,
                Limit = topK,
                ScoreThreshold = scoreThreshold ?? 0.0
            };

            var url = $"{_config.BaseUrl}/collections/{_config.CollectionName}/points/search";
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(searchRequest)
            };

            if (!string.IsNullOrEmpty(_config.ApiKey))
            {
                request.Headers.Add("api-key", _config.ApiKey);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var searchResponse = JsonSerializer.Deserialize<QdrantSearchResponse>(json, options);
            
            var results = (searchResponse?.Result ?? new List<QdrantSearchHit>())
                .Select(hit => new VectorSearchResult
                {
                    Id = hit.Id,
                    Content = hit.Payload?.GetValueOrDefault("content")?.ToString() ?? string.Empty,
                    Score = hit.Score,
                    Metadata = hit.Payload
                })
                .ToList();

            _logger?.LogDebug("Search returned {count} results", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to search Qdrant");
            throw;
        }
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchByContentAsync(
        string content,
        int topK = 5,
        double? scoreThreshold = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Searching Qdrant by content");

            // Generate embedding for the content
            var embedding = await _embeddingService.EmbedAsync(content, cancellationToken);

            // Use vector search
            return await SearchAsync(embedding, topK, scoreThreshold, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to search Qdrant by content");
            throw;
        }
    }

    public async Task DeleteAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Deleting document {id} from Qdrant", id);

            var deleteRequest = new QdrantDeleteRequest
            {
                PointsSelector = new { PointIdsList = new[] { id } }
            };

            var url = $"{_config.BaseUrl}/collections/{_config.CollectionName}/points/delete";
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(deleteRequest)
            };

            if (!string.IsNullOrEmpty(_config.ApiKey))
            {
                request.Headers.Add("api-key", _config.ApiKey);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger?.LogDebug("Successfully deleted document {id}", id);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete document {id} from Qdrant", id);
            throw;
        }
    }

    public async Task ClearAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Clearing Qdrant collection {collection}", _config.CollectionName);

            // Delete all points by creating an empty delete request with match_all
            var url = $"{_config.BaseUrl}/collections/{_config.CollectionName}/points/delete";
            var deleteAllRequest = new { filter = new { @is_empty = true } };

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(deleteAllRequest)
            };

            if (!string.IsNullOrEmpty(_config.ApiKey))
            {
                request.Headers.Add("api-key", _config.ApiKey);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger?.LogInformation("Cleared Qdrant collection {collection}", _config.CollectionName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to clear Qdrant collection");
            throw;
        }
    }

    public async Task<VectorDatabaseStats> GetStatsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_config.BaseUrl}/collections/{_config.CollectionName}";
            
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(_config.ApiKey))
            {
                request.Headers.Add("api-key", _config.ApiKey);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var collectionInfo = JsonSerializer.Deserialize<QdrantCollectionInfo>(json, options);

            return new VectorDatabaseStats
            {
                DocumentCount = collectionInfo?.Result?.PointsCount ?? 0,
                SizeBytes = collectionInfo?.Result?.ConfigDiff?.VectorSize ?? 0,
                IsHealthy = true,
                Status = "Operational"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get Qdrant stats");
            return new VectorDatabaseStats
            {
                IsHealthy = false,
                Status = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<bool> HealthCheckAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_config.BaseUrl}/health";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    #region Qdrant API DTOs

    private class QdrantPointRequest
    {
        [JsonPropertyName("points")]
        public QdrantPoint[] Points { get; set; } = Array.Empty<QdrantPoint>();
    }

    private class QdrantPoint
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("vector")]
        public float[] Vector { get; set; } = Array.Empty<float>();

        [JsonPropertyName("payload")]
        public Dictionary<string, object> Payload { get; set; } = new();
    }

    private class QdrantSearchRequest
    {
        [JsonPropertyName("vector")]
        public float[] Vector { get; set; } = Array.Empty<float>();

        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 10;

        [JsonPropertyName("score_threshold")]
        public double ScoreThreshold { get; set; } = 0.0;
    }

    private class QdrantSearchResponse
    {
        [JsonPropertyName("result")]
        public List<QdrantSearchHit> Result { get; set; } = new();
    }

    private class QdrantSearchHit
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("payload")]
        public Dictionary<string, object>? Payload { get; set; }
    }

    private class QdrantDeleteRequest
    {
        [JsonPropertyName("points_selector")]
        public object PointsSelector { get; set; } = new();
    }

    private class QdrantCollectionInfo
    {
        [JsonPropertyName("result")]
        public QdrantCollectionResult? Result { get; set; }
    }

    private class QdrantCollectionResult
    {
        [JsonPropertyName("points_count")]
        public long PointsCount { get; set; }

        [JsonPropertyName("config")]
        public QdrantConfig Config { get; set; } = new();

        [JsonPropertyName("config_diff")]
        public QdrantConfigDiff? ConfigDiff { get; set; }
    }

    private class QdrantConfigDiff
    {
        [JsonPropertyName("vector_size")]
        public long VectorSize { get; set; }
    }

    #endregion
}
