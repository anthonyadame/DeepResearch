using DeepResearchAgent.Models;

namespace DeepResearchAgent.Services.WebSearch;

/// <summary>
/// Abstraction for web search providers.
/// Enables switching between different search implementations (SearXNG, Tavily, etc.).
/// </summary>
public interface IWebSearchProvider
{
    /// <summary>
    /// Gets the provider name (e.g., "searxng", "tavily").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Search the web for information on a query.
    /// </summary>
    /// <param name="query">The search query</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <param name="topics">Optional list of topics to constrain the search context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of web search results</returns>
    Task<List<WebSearchResult>> SearchAsync(
        string query,
        int maxResults = 10,
        List<string>? topics = null,
        CancellationToken cancellationToken = default);
}
