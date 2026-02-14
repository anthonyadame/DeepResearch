using System.ComponentModel.DataAnnotations;

namespace DeepResearchAgent.Models;

/// <summary>
/// Research request model for API endpoints.
/// </summary>
public class ResearchRequest
{
    /// <summary>
    /// Research topic (required).
    /// </summary>
    [Required(ErrorMessage = "Topic is required")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Topic must be between 3 and 200 characters")]
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Research brief/description (required).
    /// </summary>
    [Required(ErrorMessage = "Research brief is required")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Brief must be between 10 and 1000 characters")]
    public string ResearchBrief { get; set; } = string.Empty;

    /// <summary>
    /// Optional research ID for state tracking.
    /// </summary>
    public string? ResearchId { get; set; }
}

/// <summary>
/// Research response model from API endpoints.
/// </summary>
public class ResearchResponse
{
    /// <summary>
    /// Research ID.
    /// </summary>
    public string ResearchId { get; set; } = string.Empty;

    /// <summary>
    /// Research topic.
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Generated report.
    /// </summary>
    public ReportOutput Report { get; set; } = new();

    /// <summary>
    /// Execution status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Research state response model.
/// </summary>
public class ResearchStateResponse
{
    /// <summary>
    /// Research ID.
    /// </summary>
    public string ResearchId { get; set; } = string.Empty;

    /// <summary>
    /// Research query.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Current status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Start timestamp.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Completion timestamp.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Iteration count.
    /// </summary>
    public int IterationCount { get; set; }

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Error response model.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error message.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Error details.
    /// </summary>
    public List<string> Details { get; set; } = new();
}

/// <summary>
/// Validation response model.
/// </summary>
public class ValidationResponse
{
    /// <summary>
    /// Whether validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation errors.
    /// </summary>
    public List<string> Errors { get; set; } = new();
}
