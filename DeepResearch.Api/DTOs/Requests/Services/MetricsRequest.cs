namespace DeepResearch.Api.DTOs.Requests.Services;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Request for metrics queries and operations.
/// </summary>
public class MetricsRequest
{
    /// <summary>
    /// Optional session context.
    /// </summary>
    public SessionContextDto? Session { get; set; }

    /// <summary>
    /// Operation: GetAll, GetByAgent, GetByResearchId, Clear.
    /// </summary>
    public required string Operation { get; set; }

    /// <summary>
    /// Agent filter if applicable.
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Research ID filter if applicable.
    /// </summary>
    public string? ResearchId { get; set; }

    /// <summary>
    /// Date range start for filtering.
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Date range end for filtering.
    /// </summary>
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Pagination parameters.
    /// </summary>
    public PaginationDto? Pagination { get; set; }

    /// <summary>
    /// Enable detailed logging.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}
