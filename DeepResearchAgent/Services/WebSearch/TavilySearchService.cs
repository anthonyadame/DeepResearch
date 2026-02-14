using System.Text;
using System.Text.Json;
using DeepResearchAgent.Models;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services.WebSearch;

/// <summary>
/// Tavily web search provider implementation.
/// Uses Tavily API for intelligent web search with configurable depth and focus.
/// https://tavily.com/
/// </summary>
public class TavilySearchService : IWebSearchProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly ILogger<TavilySearchService>? _logger;
    private readonly TimeSpan _requestTimeout;

    public string ProviderName => "tavily";

    public TavilySearchService(
        HttpClient httpClient,
        string apiKey,
        string baseUrl = "https://api.tavily.com",
        ILogger<TavilySearchService>? logger = null,
        TimeSpan? requestTimeout = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _baseUrl = baseUrl.TrimEnd('/');
        _logger = logger;
        _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(30);
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
                "Tavily: Searching for '{Query}' with max {MaxResults} results. Topics: {Topics}",
                query,
                maxResults,
                topics != null ? string.Join(", ", topics) : "none");

            var request = new TavilySearchRequest
            {
                ApiKey = _apiKey,
                Query = query,
                MaxResults = maxResults,
                IncludeAnswer = true,
                IncludeDomains = topics ?? new List<string>()
            };

            var requestJson = JsonSerializer.Serialize(request);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                cts.CancelAfter(_requestTimeout);

                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/search",
                    content,
                    cts.Token);

                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync(cts.Token);
                var searchResponse = JsonSerializer.Deserialize<TavilySearchResponse>(
                    jsonResponse,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (searchResponse?.Results == null)
                {
                    _logger?.LogWarning("Tavily: No results returned for query '{Query}'", query);
                    return new List<WebSearchResult>();
                }

                var results = searchResponse.Results
                    .Take(maxResults)
                    .Select(r => new WebSearchResult
                    {
                        Title = r.Title ?? string.Empty,
                        Url = r.Url ?? string.Empty,
                        Content = r.Content ?? string.Empty,
                        Engine = ProviderName,
                        Score = r.Score
                    })
                    .ToList();

                _logger?.LogInformation(
                    "Tavily: Found {ResultCount} results for query '{Query}'",
                    results.Count,
                    query);

                return results;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(
                ex,
                "Tavily: HTTP error occurred during search for query '{Query}'",
                query);
            throw new InvalidOperationException(
                $"Tavily web search failed for query: {query}",
                ex);
        }
        catch (OperationCanceledException ex)
        {
            _logger?.LogError(
                ex,
                "Tavily: Request timeout for query '{Query}'",
                query);
            throw new InvalidOperationException(
                $"Tavily web search timed out for query: {query}",
                ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Tavily: Unexpected error during search for query '{Query}'",
                query);
            throw new InvalidOperationException(
                $"Tavily web search failed for query: {query}",
                ex);
        }
    }

    /// <summary>
    /// Tavily search request model
    /// </summary>
    private class TavilySearchRequest
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
        public int MaxResults { get; set; } = 10;
        public bool IncludeAnswer { get; set; } = true;
        public List<string> IncludeDomains { get; set; } = new();
    }

    /// <summary>
    /// Tavily search response model
    /// </summary>
    private class TavilySearchResponse
    {
        public List<TavilyResult> Results { get; set; } = new();
    }

    /// <summary>
    /// Individual result from Tavily
    /// </summary>
    private class TavilyResult
    {
        public string? Title { get; set; }
        public string? Url { get; set; }
        public string? Content { get; set; }
        public double? Score { get; set; }
    }
}
