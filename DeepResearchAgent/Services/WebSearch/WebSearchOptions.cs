namespace DeepResearchAgent.Services.WebSearch;

/// <summary>
/// Configuration options for web search providers
/// </summary>
public class WebSearchOptions
{
    /// <summary>
    /// Gets or sets the default web search provider to use (e.g., "searxng", "tavily")
    /// </summary>
    public string Provider { get; set; } = "searxng";

    /// <summary>
    /// Gets or sets the Tavily API key
    /// </summary>
    public string? TavilyApiKey { get; set; }

    /// <summary>
    /// Gets or sets the Tavily API base URL
    /// </summary>
    public string TavilyBaseUrl { get; set; } = "https://api.tavily.com";

    /// <summary>
    /// Gets or sets the request timeout in seconds
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the default maximum number of results
    /// </summary>
    public int DefaultMaxResults { get; set; } = 10;

    /// <summary>
    /// Gets or sets optional topics for constraining web search results
    /// </summary>
    public List<string>? DefaultTopics { get; set; }
}
