namespace DeepResearchAgent.Services;

/// <summary>
/// Configuration options for SearCrawl4AI service
/// </summary>
public class SearCrawl4AIConfig
{
    public string SearXNGBaseUrl { get; set; } = "http://localhost:8080";
    public string Crawl4AIBaseUrl { get; set; } = "http://localhost:11235";
    public int DefaultMaxResults { get; set; } = 5;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public bool EnableLogging { get; set; } = true;
}
