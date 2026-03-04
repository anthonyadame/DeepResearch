using DeepResearchAgent.Models;

namespace DeepResearch.Api.DTOs.Requests.Chat;

/// <summary>
/// Request for executing a single step of the research workflow via chat.
/// The AgentState is passed back and forth between UI and API, allowing
/// the workflow to progress through 5 steps with the UI controlling timing.
/// </summary>
public record ChatStepRequest
{
    /// <summary>
    /// The current state of the research workflow.
    /// Contains all accumulated results and flags from previous steps.
    /// </summary>
    public required AgentState CurrentState { get; init; }

    /// <summary>
    /// User's response to a clarification question (if NeedsQualityRepair was true).
    /// If provided, this replaces Messages.First() and NeedsQualityRepair is set to false.
    /// </summary>
    public string? UserResponse { get; init; }

    /// <summary>
    /// Optional research configuration for this step.
    /// </summary>
    public ResearchConfig? Config { get; init; }
}
