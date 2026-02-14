using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using DeepResearchAgent.Models;

namespace DeepResearchAgent.Services;

/// <summary>
/// Service that combines SearXNG for web searching with Crawl4AI for web scraping.
/// Provides unified interface for research agent to gather information from the web.
/// </summary>
public class SearCrawl4AIService : ISearCrawl4AIService
{
    private readonly HttpClient _httpClient;
    private readonly string _searxngBaseUrl;
    private readonly string _crawl4aiBaseUrl;
    private readonly ILogger? _logger;
    private readonly bool _enableCaching;
    private readonly TimeSpan _cacheDuration;
    private readonly ConcurrentDictionary<string, CacheEntry<SearXNGResponse>> _searchCache = new();
    private readonly ConcurrentDictionary<string, CacheEntry<Crawl4AIResponse>> _scrapeCache = new();

    public SearCrawl4AIService(
        HttpClient httpClient,
        string searxngBaseUrl = "http://localhost:8080",
        string crawl4aiBaseUrl = "http://localhost:11235",
        ILogger? logger = null,
        bool enableCaching = true,
        TimeSpan? cacheDuration = null)
    {
        _httpClient = httpClient;
        _searxngBaseUrl = searxngBaseUrl.TrimEnd('/');
        _crawl4aiBaseUrl = crawl4aiBaseUrl.TrimEnd('/');
        _logger = logger;
        _enableCaching = enableCaching;
        _cacheDuration = cacheDuration ?? TimeSpan.FromMinutes(10);
    }

    /// <summary>
    /// Search using SearXNG
    /// </summary>
    public async Task<SearXNGResponse> SearchAsync(
        string query, 
        int maxResults = 10, 
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"search::{query}::{maxResults}";
        if (_enableCaching && TryGetCached(_searchCache, cacheKey, out var cachedSearch))
        {
            _logger?.LogInformation("Returning cached SearXNG results for: {Query}", query);
            return cachedSearch;
        }

        try
        {
            _logger?.LogInformation("Searching SearXNG for: {Query}", query);

            var queryParams = new Dictionary<string, string>
            {
                { "q", query },
                { "format", "json" },
                { "pageno", "1" }
            };

            var queryString = string.Join("&", queryParams.Select(kvp => 
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

            var requestUrl = $"{_searxngBaseUrl}/search?{queryString}";

            var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

            var results = new List<SearchResult>();
            
            if (jsonResponse.TryGetProperty("results", out var resultsArray))
            {
                var count = 0;
                foreach (var item in resultsArray.EnumerateArray())
                {
                    if (count >= maxResults) break;

                    var result = new SearchResult
                    {
                        Title = item.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                        Url = item.TryGetProperty("url", out var url) ? url.GetString() ?? "" : "",
                        Content = item.TryGetProperty("content", out var content) ? content.GetString() ?? "" : "",
                        Engine = item.TryGetProperty("engine", out var engine) ? engine.GetString() ?? "" : "",
                        Score = item.TryGetProperty("score", out var score) && score.ValueKind == JsonValueKind.Number 
                            ? score.GetDouble() 
                            : null
                    };

                    results.Add(result);
                    count++;
                }
            }

            _logger?.LogInformation("Found {Count} results from SearXNG", results.Count);

            var responsePayload = new SearXNGResponse
            {
                Query = query,
                NumberOfResults = results.Count,
                Results = results
            };

            SetCache(_searchCache, cacheKey, responsePayload);
            return responsePayload;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error searching SearXNG for query: {Query}", query);
            throw new InvalidOperationException($"SearXNG search failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Scrape URLs using Crawl4AI
    /// </summary>
    public async Task<Crawl4AIResponse> ScrapeAsync(
        List<string> urls, 
        Crawl4AIRequest? options = null,
        CancellationToken cancellationToken = default)
    {
        
        var cacheKey = $"scrape::{string.Join('|', urls)}";
        if (_enableCaching && TryGetCached(_scrapeCache, cacheKey, out var cachedScrape))
        {
            _logger?.LogInformation("Returning cached Crawl4AI results for {Count} URLs", urls.Count);
            return cachedScrape;
        }

        try
        {
            _logger?.LogInformation("Scraping {Count} URLs with Crawl4AI", urls.Count);

            var request = options ?? new Crawl4AIRequest();
            request.Urls = urls;

            var requestUrl = $"{_crawl4aiBaseUrl}/crawl";
            var response = await _httpClient.PostAsJsonAsync(requestUrl, request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger?.LogError(null, "Crawl4AI returned error: {Error}", errorContent);
                
                return new Crawl4AIResponse
                {
                    Success = false,
                    ErrorMessage = $"HTTP {response.StatusCode}: {errorContent}"
                };
            }

            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var scrapedContents = new List<ScrapedContent>();

            if (jsonResponse.TryGetProperty("results", out var resultsArray))
            {
                foreach (var item in resultsArray.EnumerateArray())
                {
                    var scraped = new ScrapedContent
                    {
                        Url = item.TryGetProperty("url", out var url) ? url.GetString() ?? "" : "",
                        Html = item.TryGetProperty("html", out var html) ? html.GetString() ?? "" : "",
                        Markdown = item.TryGetProperty("markdown", out var markdown) ? markdown.ToString() ?? "" : "",
                        CleanedHtml = item.TryGetProperty("cleaned_html", out var cleanedHtml) ? cleanedHtml.GetString() ?? "" : "",
                        Media = item.TryGetProperty("media", out var media) ? media.ToString() : "",
                        Links = item.TryGetProperty("links", out var links) ? links.ToString() : "",
                        Success = item.TryGetProperty("success", out var success) && success.GetBoolean(),
                        ErrorMessage = item.TryGetProperty("error_message", out var error) ? error.GetString() : null
                    };

                    if (item.TryGetProperty("metadata", out var metadata))
                    {
                        scraped.Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metadata.GetRawText()) 
                            ?? new Dictionary<string, object>();
                    }

                    scrapedContents.Add(scraped);
                }
            }

            _logger?.LogInformation("Successfully scraped {Count} URLs", scrapedContents.Count(s => s.Success));

            var responsePayload = new Crawl4AIResponse
            {
                Success = true,
                Results = scrapedContents
            };

            SetCache(_scrapeCache, cacheKey, responsePayload);
            return responsePayload;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error scraping with Crawl4AI");
            return new Crawl4AIResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Combined search and scrape operation: searches with SearXNG then scrapes top results with Crawl4AI
    /// </summary>
    public async Task<List<ScrapedContent>> SearchAndScrapeAsync(
        string query,
        int maxResults = 5,
        Crawl4AIRequest? scrapeOptions = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting search and scrape for: {Query}", query);

        // Step 1: Search with SearXNG
        var searchResponse = await SearchAsync(query, maxResults, cancellationToken);

        if (!searchResponse.Results.Any())
        {
            _logger?.LogWarning("No search results found for query: {Query}", query);
            return new List<ScrapedContent>();
        }

        // Step 2: Extract URLs from search results
        var urls = searchResponse.Results
            .Where(r => !string.IsNullOrWhiteSpace(r.Url))
            .Select(r => r.Url)
            .Take(maxResults)
            .ToList();

        if (!urls.Any())
        {
            _logger?.LogWarning("No valid URLs found in search results");
            return new List<ScrapedContent>();
        }

        // Step 3: Scrape URLs with Crawl4AI
        var scrapeResponse = await ScrapeAsync(urls, scrapeOptions, cancellationToken);

        if (!scrapeResponse.Success)
        {
            _logger?.LogError(null, "Scraping failed: {Error}", scrapeResponse.ErrorMessage);
            throw new InvalidOperationException($"Scraping failed: {scrapeResponse.ErrorMessage}");
        }

        var successfulScrapes = scrapeResponse.Results.Where(r => r.Success).ToList();
        _logger?.LogInformation("Search and scrape completed. {Count}/{Total} URLs successfully scraped", 
            successfulScrapes.Count, urls.Count);

        return successfulScrapes;
    }

    private bool TryGetCached<T>(ConcurrentDictionary<string, CacheEntry<T>> cache, string key, out T value)
    {
        value = default!;
        if (!_enableCaching)
        {
            return false;
        }

        if (cache.TryGetValue(key, out var entry))
        {
            if (entry.Expiry > DateTimeOffset.UtcNow)
            {
                value = entry.Value;
                return true;
            }
            cache.TryRemove(key, out _);
        }

        return false;
    }

    private void SetCache<T>(ConcurrentDictionary<string, CacheEntry<T>> cache, string key, T value)
    {
        if (!_enableCaching)
        {
            return;
        }

        cache[key] = new CacheEntry<T>(value, DateTimeOffset.UtcNow.Add(_cacheDuration));
    }

    private record CacheEntry<T>(T Value, DateTimeOffset Expiry);
}

/// <summary>
/// Minimal logger interface to avoid dependency on Microsoft.Extensions.Logging
/// Can be replaced with proper ILogger when logging is set up
/// </summary>
public interface ILogger
{
    void LogInformation(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogError(Exception? exception, string message, params object[] args);
}
