namespace DeepResearch.Api.DTOs.Responses.Workflows;

using DeepResearch.Api.DTOs.Common;

/// <summary>
/// Response from MasterWorkflow - Complete research report.
/// </summary>
public class MasterWorkflowResponse
{
    /// <summary>
    /// Unique research execution ID.
    /// </summary>
    public string ResearchId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Optional session ID from request.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Final research report content.
    /// </summary>
    public string FinalReport { get; set; } = string.Empty;

    /// <summary>
    /// Overall quality score (0.0-1.0).
    /// </summary>
    public double QualityScore { get; set; }

    /// <summary>
    /// Execution status.
    /// </summary>
    public string Status { get; set; } = "Completed";

    /// <summary>
    /// Number of iterations completed.
    /// </summary>
    public int IterationsCompleted { get; set; }

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long DurationMilliseconds { get; set; }

    /// <summary>
    /// When the execution started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When the execution completed.
    /// </summary>
    public DateTime CompletedAt { get; set; }
}

/// <summary>
/// Response from SupervisorWorkflow - Refined research report.
/// </summary>
public class SupervisorWorkflowResponse
{
    /// <summary>
    /// Unique supervision ID.
    /// </summary>
    public string SupervisionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Optional session ID from request.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Quality scores from each iteration.
    /// </summary>
    public List<double> QualityScores { get; set; } = new();

    /// <summary>
    /// Final quality score.
    /// </summary>
    public double FinalQualityScore => QualityScores.Any() ? QualityScores.Last() : 0.0;

    /// <summary>
    /// Number of iterations performed.
    /// </summary>
    public int IterationsPerformed { get; set; }

    /// <summary>
    /// Whether target quality was reached.
    /// </summary>
    public bool TargetQualityReached { get; set; }

    /// <summary>
    /// Refined report content.
    /// </summary>
    public string RefinedReport { get; set; } = string.Empty;

    /// <summary>
    /// Execution status.
    /// </summary>
    public string Status { get; set; } = "Completed";

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long DurationMilliseconds { get; set; }
}

/// <summary>
/// Response from ResearcherWorkflow - Research findings.
/// </summary>
public class ResearcherWorkflowResponse
{
    /// <summary>
    /// Unique research ID.
    /// </summary>
    public string ResearchId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Optional session ID.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// The topic researched.
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Facts and findings extracted.
    /// </summary>
    public List<FactDto> Facts { get; set; } = new();

    /// <summary>
    /// Topics covered in research.
    /// </summary>
    public List<string> TopicsCovered { get; set; } = new();

    /// <summary>
    /// Number of iterations completed.
    /// </summary>
    public int IterationsCompleted { get; set; }

    /// <summary>
    /// Number of web searches performed.
    /// </summary>
    public int SearchesPerformed { get; set; }

    /// <summary>
    /// Number of sources consulted.
    /// </summary>
    public int SourcesConsulted { get; set; }

    /// <summary>
    /// Execution status.
    /// </summary>
    public string Status { get; set; } = "Completed";

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long DurationMilliseconds { get; set; }
}
