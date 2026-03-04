namespace DeepResearch.Api.DTOs.Responses.Services;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Response from metrics queries.
/// </summary>
public class MetricsResponse
{
    /// <summary>
    /// Operation performed.
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Metrics data.
    /// </summary>
    public PaginatedResponse<MetricEntryDto>? Metrics { get; set; }

    /// <summary>
    /// Aggregated statistics.
    /// </summary>
    public MetricsStatisticsDto? Statistics { get; set; }

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
/// Individual metric entry.
/// </summary>
public class MetricEntryDto
{
    public string Agent { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ResearchId { get; set; }
    public DateTime RecordedAt { get; set; }
    public long DurationMilliseconds { get; set; }
    public Dictionary<string, object>? AdditionalData { get; set; }
}

/// <summary>
/// Aggregated metrics statistics.
/// </summary>
public class MetricsStatisticsDto
{
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public double AverageDurationMs { get; set; }
    public double MinDurationMs { get; set; }
    public double MaxDurationMs { get; set; }
    public Dictionary<string, int>? RequestsByAgent { get; set; }
    public Dictionary<string, int>? RequestsByStatus { get; set; }
}
