namespace DeepResearch.Api.DTOs.Responses.Services;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Response from vector search operations.
/// </summary>
public class VectorSearchResponse
{
    /// <summary>
    /// Search results.
    /// </summary>
    public List<VectorResultDto> Results { get; set; } = new();

    /// <summary>
    /// Total results found.
    /// </summary>
    public int TotalResults { get; set; }

    /// <summary>
    /// Query vector used.
    /// </summary>
    public List<float>? QueryVector { get; set; }

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
/// Individual vector search result.
/// </summary>
public class VectorResultDto
{
    public string VectorId { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
    public Dictionary<string, object>? Payload { get; set; }
}

/// <summary>
/// Response from add vector operation.
/// </summary>
public class VectorAddResponse
{
    public string VectorId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Message { get; set; }
    public long DurationMilliseconds { get; set; }
}
