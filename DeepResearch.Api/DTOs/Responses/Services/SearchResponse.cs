namespace DeepResearch.Api.DTOs.Responses.Services;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Response from web search.
/// </summary>
public class SearchResponse
{
    /// <summary>
    /// Search query performed.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Search results found.
    /// </summary>
    public List<SearchResultDto> Results { get; set; } = new();

    /// <summary>
    /// Total number of results available.
    /// </summary>
    public int TotalResults { get; set; }

    /// <summary>
    /// Search engine(s) used.
    /// </summary>
    public List<string> SearchEngines { get; set; } = new();

    /// <summary>
    /// Whether results are cached.
    /// </summary>
    public bool IsCached { get; set; }

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long DurationMilliseconds { get; set; }

    /// <summary>
    /// Metadata about the operation.
    /// </summary>
    public ApiMetadata? Metadata { get; set; }
}

/// <summary>
/// Individual search result.
/// </summary>
public class SearchResultDto
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? PublishedDate { get; set; }
    public double? Relevance { get; set; }
}
