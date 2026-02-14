namespace DeepResearchAgent.Models;

/// <summary>
/// Represents a search result from SearXNG
/// </summary>
public class SearchResult
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Engine { get; set; } = string.Empty;
    public double? Score { get; set; }
}

/// <summary>
/// Represents the response from SearXNG search
/// </summary>
public class SearXNGResponse
{
    public string Query { get; set; } = string.Empty;
    public int NumberOfResults { get; set; }
    public List<SearchResult> Results { get; set; } = new();
}
