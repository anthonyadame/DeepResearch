using DeepResearchAgent.Models;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services.WebSearch;

/// <summary>
/// Adapter to make SearCrawl4AIService implement IWebSearchProvider.
/// Bridges legacy SearCrawl4AIService with the new provider abstraction.
/// </summary>
public class SearCrawl4AIAdapter : IWebSearchProvider
{
    private readonly SearCrawl4AIService _searCrawl4AIService;
    private readonly ILogger<SearCrawl4AIAdapter>? _logger;

    public string ProviderName => "searxng";

    public SearCrawl4AIAdapter(
        SearCrawl4AIService searCrawl4AIService,
        ILogger<SearCrawl4AIAdapter>? logger = null)
    {
        _searCrawl4AIService = searCrawl4AIService ?? throw new ArgumentNullException(nameof(searCrawl4AIService));
        _logger = logger;
    }

    public async Task<List<WebSearchResult>> SearchAsync(
        string query,
        int maxResults = 10,
        List<string>? topics = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation(
                "SearXNG: Searching for '{Query}' with max {MaxResults} results. Topics: {Topics}",
                query,
                maxResults,
                topics != null ? string.Join(", ", topics) : "none");

            var response = await _searCrawl4AIService.SearchAsync(query, maxResults, cancellationToken);

            var results = (response?.Results ?? new List<SearchResult>())
                .Select(r => new WebSearchResult
                {
                    Title = r.Title,
                    Url = r.Url,
                    Content = r.Content ?? string.Empty,
                    Engine = ProviderName,
                    Score = r.Score
                })
                .ToList();

            _logger?.LogInformation(
                "SearXNG: Found {ResultCount} results for query '{Query}'",
                results.Count,
                query);

            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "SearXNG: Error during search for query '{Query}'",
                query);
            throw;
        }
    }
}
