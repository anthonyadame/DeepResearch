namespace DeepResearch.Api.DTOs.Requests.Services;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Request for web scraping - Extract content from URL using Crawl4AI.
/// </summary>
public class ScrapeRequest
{
    /// <summary>
    /// URL to scrape (required).
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// Optional session context.
    /// </summary>
    public SessionContextDto? Session { get; set; }

    /// <summary>
    /// Optional configuration overrides.
    /// </summary>
    public ConfigurationDto? Configuration { get; set; }

    /// <summary>
    /// Extract CSS selectors from page.
    /// </summary>
    public List<string>? CssSelectors { get; set; }

    /// <summary>
    /// Extract XPath elements from page.
    /// </summary>
    public List<string>? XPaths { get; set; }

    /// <summary>
    /// Wait for element selector before scraping.
    /// </summary>
    public string? WaitForSelector { get; set; }

    /// <summary>
    /// Javascript to execute before scraping.
    /// </summary>
    public string? ExecuteJavaScript { get; set; }

    /// <summary>
    /// Include images in scrape.
    /// </summary>
    public bool IncludeImages { get; set; } = false;

    /// <summary>
    /// Include links in scrape.
    /// </summary>
    public bool IncludeLinks { get; set; } = true;

    /// <summary>
    /// Timeout in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Enable detailed logging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}
