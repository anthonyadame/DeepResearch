using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DeepResearchAgent.Models;

namespace DeepResearchAgent.Services.Workflows;

/// <summary>
/// Research execution configuration.
/// </summary>
public class ResearchConfiguration
{
    public int MaxIterations { get; set; } = 5;
    public double TargetQualityScore { get; set; } = 0.8;
    public int TimeoutSeconds { get; set; } = 600;
    public List<string> FocusTopics { get; set; } = new();
}

/// <summary>
/// Single research iteration result.
/// </summary>
public class ResearchIteration
{
    public int IterationNumber { get; set; }
    public List<ExtractedFact> FoundFacts { get; set; } = new();
    public double QualityScore { get; set; }
    public DateTime CompletedAt { get; set; }
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Current research progress.
/// </summary>
public class ResearchProgress
{
    public string ResearchId { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public int CurrentIteration { get; set; }
    public int MaxIterations { get; set; }
    public double CurrentQualityScore { get; set; }
    public double TargetQualityScore { get; set; }
    public bool IsComplete { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "Running";
}

/// <summary>
/// Final research findings.
/// </summary>
public class ResearchFindings
{
    public string ResearchId { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public List<ResearchIteration> Iterations { get; set; } = new();
    public List<ExtractedFact> AllFacts { get; set; } = new();
    public double FinalQualityScore { get; set; }
    public int TotalIterations { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public bool ReachedTargetQuality { get; set; }
    public DateTime CompletedAt { get; set; }
}

/// <summary>
/// Researcher Workflow Service for intelligent research iteration loops.
/// </summary>
public interface IResearcherWorkflowService
{
    /// <summary>Execute research with iteration loop until target quality or max iterations.</summary>
    Task<ResearchFindings> ExecuteResearchAsync(
        string topic,
        ResearchConfiguration config,
        CancellationToken ct = default);

    /// <summary>Get all iterations for a research execution.</summary>
    Task<IReadOnlyList<ResearchIteration>> GetIterationHistoryAsync(
        string researchId,
        CancellationToken ct = default);

    /// <summary>Get current progress of ongoing research.</summary>
    Task<ResearchProgress> GetProgressAsync(
        string researchId,
        CancellationToken ct = default);

    /// <summary>Stop research early and return current findings.</summary>
    Task<ResearchFindings> StopResearchAsync(
        string researchId,
        string reason,
        CancellationToken ct = default);
}
