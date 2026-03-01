namespace DeepResearchAgent.Models;

/// <summary>
/// Represents scraped content from Crawl4AI
/// </summary>
public class ScrapedContent
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Html { get; set; } = string.Empty;
    public string Markdown { get; set; } = string.Empty;
    public string CleanedHtml { get; set; } = string.Empty;
    public string Media { get; set; } = string.Empty;
    public string Links { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Request for Crawl4AI scraping
/// </summary>
public class Crawl4AIRequest
{
    public List<string> Urls { get; set; } = new();
    public string WordCountThreshold { get; set; } = "10";
    public string ExtractionStrategy { get; set; } = "NoExtractionStrategy";
    public string ChunkingStrategy { get; set; } = "RegexChunking";
    public bool Bypass_cache { get; set; } = false;
    public string CssSelector { get; set; } = string.Empty;
    public bool Screenshot { get; set; } = false;
    public string UserAgent { get; set; } = string.Empty;
    public bool Verbose { get; set; } = true;
}

/// <summary>
/// Response from Crawl4AI service
/// </summary>
public class Crawl4AIResponse
{
    public List<ScrapedContent> Results { get; set; } = new();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
