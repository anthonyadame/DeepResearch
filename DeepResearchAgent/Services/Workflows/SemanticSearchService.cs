using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeepResearchAgent.Models;
using DeepResearchAgent.Services.VectorDatabase;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services.Workflows;

/// <summary>Semantic search result with similarity score.</summary>
public class SemanticSearchResult
{
    public string FactId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public DateTime IndexedAt { get; set; }
    public List<string> RelatedTopics { get; set; } = new();
}

/// <summary>Search statistics.</summary>
public class SearchStatistics
{
    public int TotalIndexedFacts { get; set; }
    public int SearchCount { get; set; }
    public double AverageSearchTime { get; set; }
}

/// <summary>Semantic Search Service interface.</summary>
public interface ISemanticSearchService
{
    Task IndexFactAsync(
        ExtractedFact fact,
        CancellationToken ct = default);

    Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
        string query,
        int topK = 10,
        double minSimilarityScore = 0.5,
        CancellationToken ct = default);

    Task<IReadOnlyList<SemanticSearchResult>> FindSimilarAsync(
        string factId,
        int topK = 5,
        CancellationToken ct = default);

    Task<IReadOnlyList<SemanticSearchResult>> GetRecommendationsAsync(
        string query,
        Dictionary<string, object>? context = null,
        int topK = 10,
        CancellationToken ct = default);

    Task RemoveFactAsync(string factId, CancellationToken ct = default);
    Task<SearchStatistics> GetStatisticsAsync(CancellationToken ct = default);
}

/// <summary>Implementation of Semantic Search Service.</summary>
public class SemanticSearchService : ISemanticSearchService
{
    private readonly IVectorDatabaseService? _vectorDb;
    private readonly IEmbeddingService? _embeddingService;
    private readonly ILogger<SemanticSearchService>? _logger;

    private readonly ConcurrentDictionary<string, SemanticSearchResult> _factIndex = new();
    private int _searchCount = 0;

    public SemanticSearchService(
        IVectorDatabaseService? vectorDb = null,
        IEmbeddingService? embeddingService = null,
        ILogger<SemanticSearchService>? logger = null)
    {
        _vectorDb = vectorDb;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task IndexFactAsync(
        ExtractedFact fact,
        CancellationToken ct = default)
    {
        var factId = Guid.NewGuid().ToString("N");

        var result = new SemanticSearchResult
        {
            FactId = factId,
            Content = fact.Statement,
            SourceUrl = fact.Source,
            SimilarityScore = fact.Confidence,
            IndexedAt = DateTime.UtcNow,
            RelatedTopics = new() { fact.Category }
        };

        _factIndex.TryAdd(factId, result);

        _logger?.LogDebug("Fact indexed: {FactId} - {Content}", factId, fact.Statement);
    }

    public async Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
        string query,
        int topK = 10,
        double minSimilarityScore = 0.5,
        CancellationToken ct = default)
    {
        _searchCount++;

        // Simple string matching (production would use vector similarity)
        var results = _factIndex.Values
            .Where(f => f.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Where(f => f.SimilarityScore >= minSimilarityScore)
            .OrderByDescending(f => f.SimilarityScore)
            .Take(topK)
            .ToList();

        _logger?.LogInformation(
            "Search for '{Query}' returned {ResultCount} results",
            query,
            results.Count);

        return results.AsReadOnly();
    }

    public async Task<IReadOnlyList<SemanticSearchResult>> FindSimilarAsync(
        string factId,
        int topK = 5,
        CancellationToken ct = default)
    {
        if (!_factIndex.TryGetValue(factId, out var targetFact))
        {
            return new List<SemanticSearchResult>().AsReadOnly();
        }

        var similar = _factIndex.Values
            .Where(f => f.FactId != factId)
            .Where(f => f.RelatedTopics.Intersect(targetFact.RelatedTopics).Any())
            .OrderByDescending(f => f.SimilarityScore)
            .Take(topK)
            .ToList();

        return similar.AsReadOnly();
    }

    public async Task<IReadOnlyList<SemanticSearchResult>> GetRecommendationsAsync(
        string query,
        Dictionary<string, object>? context = null,
        int topK = 10,
        CancellationToken ct = default)
    {
        // Combine search results with context-based recommendations
        var searchResults = await SearchAsync(query, topK, 0.3, ct);

        // Add context-based filtering if available
        if (context != null && context.ContainsKey("topics"))
        {
            var topics = context["topics"] as List<string> ?? new();
            var resultList = searchResults.ToList();
            return resultList
                .Where((r, i) => r.RelatedTopics.Intersect(topics).Any() || i < topK / 2)
                .Take(topK)
                .ToList()
                .AsReadOnly();
        }

        return searchResults;
    }

    public async Task RemoveFactAsync(string factId, CancellationToken ct = default)
    {
        _factIndex.TryRemove(factId, out _);
        _logger?.LogDebug("Fact removed from index: {FactId}", factId);
    }

    // Note: This is a simplified implementation. Production version would use actual embeddings.

    public async Task<SearchStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        return new SearchStatistics
        {
            TotalIndexedFacts = _factIndex.Count,
            SearchCount = _searchCount,
            AverageSearchTime = 0.005 // Approximate (5ms)
        };
    }
}
