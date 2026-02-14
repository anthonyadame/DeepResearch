using DeepResearchAgent.Models;

namespace DeepResearchAgent.Services;

/// <summary>
/// Interface for the SearCrawl4AI service combining SearXNG search with Crawl4AI scraping
/// </summary>
public interface ISearCrawl4AIService
{
    /// <summary>
    /// Search using SearXNG
    /// </summary>
    Task<SearXNGResponse> SearchAsync(string query, int maxResults = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scrape URLs using Crawl4AI
    /// </summary>
    Task<Crawl4AIResponse> ScrapeAsync(List<string> urls, Crawl4AIRequest? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Combined search and scrape: searches with SearXNG then scrapes top results with Crawl4AI
    /// </summary>
    Task<List<ScrapedContent>> SearchAndScrapeAsync(
        string query, 
        int maxResults = 5, 
        Crawl4AIRequest? scrapeOptions = null,
        CancellationToken cancellationToken = default);
}
