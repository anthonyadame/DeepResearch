namespace DeepResearch.Api.DTOs.Responses.Services;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Response from web scraping.
/// </summary>
public class ScrapeResponse
{
    /// <summary>
    /// URL that was scraped.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Main content extracted from page.
    /// </summary>
    public string? MainContent { get; set; }

    /// <summary>
    /// Page title.
    /// </summary>
    public string? PageTitle { get; set; }

    /// <summary>
    /// Meta description.
    /// </summary>
    public string? MetaDescription { get; set; }

    /// <summary>
    /// HTML content (if requested).
    /// </summary>
    public string? HtmlContent { get; set; }

    /// <summary>
    /// Structured data extracted (JSON-LD, etc).
    /// </summary>
    public object? StructuredData { get; set; }

    /// <summary>
    /// Links found on page.
    /// </summary>
    public List<string> Links { get; set; } = new();

    /// <summary>
    /// Images found on page.
    /// </summary>
    public List<string> Images { get; set; } = new();

    /// <summary>
    /// CSS selectors extracted.
    /// </summary>
    public Dictionary<string, object>? CssSelectorData { get; set; }

    /// <summary>
    /// XPath extracted data.
    /// </summary>
    public Dictionary<string, object>? XPathData { get; set; }

    /// <summary>
    /// HTTP status code.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Response headers.
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long DurationMilliseconds { get; set; }

    /// <summary>
    /// Metadata about the operation.
    /// </summary>
    public ApiMetadata? Metadata { get; set; }
}
