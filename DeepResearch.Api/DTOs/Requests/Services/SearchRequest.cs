namespace DeepResearch.Api.DTOs.Requests.Services;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Request for web search - Search for information using SearXNG.
/// </summary>
public class SearchRequest
{
    /// <summary>
    /// Search query (required).
    /// </summary>
    public required string Query { get; set; }

    /// <summary>
    /// Optional session context.
    /// </summary>
    public SessionContextDto? Session { get; set; }

    /// <summary>
    /// Optional configuration overrides.
    /// </summary>
    public ConfigurationDto? Configuration { get; set; }

    /// <summary>
    /// Maximum number of search results to return.
    /// </summary>
    public int MaxResults { get; set; } = 10;

    /// <summary>
    /// Search language.
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Search category: general, images, news, social, music.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Optional search filters.
    /// </summary>
    public Dictionary<string, string>? Filters { get; set; }

    /// <summary>
    /// Whether to use cached results if available.
    /// </summary>
    public bool? AllowCached { get; set; }

    /// <summary>
    /// Timeout in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Enable detailed logging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}
