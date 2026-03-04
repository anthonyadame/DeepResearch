namespace DeepResearch.Api.DTOs.Requests.Workflows;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Request for MasterWorkflow - Complete 5-step research pipeline.
/// </summary>
public class MasterWorkflowRequest
{
    /// <summary>
    /// The research query from the user.
    /// </summary>
    public required string UserQuery { get; set; }

    /// <summary>
    /// Optional session context for stateful operations.
    /// </summary>
    public SessionContextDto? Session { get; set; }

    /// <summary>
    /// Optional configuration overrides.
    /// </summary>
    public ConfigurationDto? Configuration { get; set; }

    /// <summary>
    /// Whether to run asynchronously.
    /// </summary>
    public bool RunAsync { get; set; } = false;

    /// <summary>
    /// Timeout in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; set; }
}

/// <summary>
/// Request for SupervisorWorkflow - Iterative refinement loop.
/// </summary>
public class SupervisorWorkflowRequest
{
    /// <summary>
    /// The research brief to refine.
    /// </summary>
    public required string ResearchBrief { get; set; }

    /// <summary>
    /// Optional session context.
    /// </summary>
    public SessionContextDto? Session { get; set; }

    /// <summary>
    /// Maximum iterations for refinement.
    /// </summary>
    public int MaxIterations { get; set; } = 3;

    /// <summary>
    /// Target quality score (0.0-1.0) to stop early if reached.
    /// </summary>
    public double? TargetQualityScore { get; set; }

    /// <summary>
    /// Optional draft report for refinement.
    /// </summary>
    public string? DraftReport { get; set; }
}

/// <summary>
/// Request for ResearcherWorkflow - Focused research phase.
/// </summary>
public class ResearcherWorkflowRequest
{
    /// <summary>
    /// The topic to research.
    /// </summary>
    public required string Topic { get; set; }

    /// <summary>
    /// Optional research ID for tracking.
    /// </summary>
    public string? ResearchId { get; set; }

    /// <summary>
    /// Optional session context.
    /// </summary>
    public SessionContextDto? Session { get; set; }

    /// <summary>
    /// Optional configuration overrides.
    /// </summary>
    public ConfigurationDto? Configuration { get; set; }
}
