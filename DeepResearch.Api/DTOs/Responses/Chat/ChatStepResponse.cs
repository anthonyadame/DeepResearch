using DeepResearchAgent.Models;

namespace DeepResearch.Api.DTOs.Responses.Chat;

/// <summary>
/// Response from executing a single step of the research workflow via chat.
/// Extends AgentState with additional UI metadata for step-by-step display.
/// UI can display all properties or filter to show only the current step's updates.
/// </summary>
public record ChatStepResponse
{
    // ===== Core AgentState Properties =====

    /// <summary>
    /// User-facing messages (conversation history).
    /// </summary>
    public List<ChatMessage> Messages { get; init; } = new();

    /// <summary>
    /// The research brief generated from user input (Step 2 output).
    /// </summary>
    public string? ResearchBrief { get; init; }

    /// <summary>
    /// Supervisor-specific message history.
    /// </summary>
    public List<ChatMessage> SupervisorMessages { get; init; } = new();

    /// <summary>
    /// Raw, unprocessed notes from all research activities.
    /// </summary>
    public List<string> RawNotes { get; init; } = new();

    /// <summary>
    /// The final, curated notes for the writer (post-processing).
    /// </summary>
    public List<string> Notes { get; init; } = new();

    /// <summary>
    /// The initial draft report (Step 3 output).
    /// </summary>
    public string? DraftReport { get; init; }

    /// <summary>
    /// The final polished report (Step 5 output).
    /// </summary>
    public string? FinalReport { get; init; }

    /// <summary>
    /// Supervisor workflow state (Step 4).
    /// </summary>
    public SupervisorState? SupervisorState { get; init; }

    /// <summary>
    /// True if clarification is needed before proceeding.
    /// Set during Step 1 if query needs more detail.
    /// </summary>
    public bool NeedsQualityRepair { get; init; }

    // ===== UI Metadata =====

    /// <summary>
    /// Current step in the 5-step workflow:
    /// 1 = Clarification, 2 = Research Brief, 3 = Draft Report, 4 = Supervisor Refinement, 5 = Final Report
    /// 0 = Not applicable/error state
    /// </summary>
    public int CurrentStep { get; init; }

    /// <summary>
    /// If set, the user must answer this clarification question before continuing.
    /// Only populated when NeedsQualityRepair=true and Step 1 needs clarification.
    /// </summary>
    public string? ClarificationQuestion { get; init; }

    /// <summary>
    /// True if the entire workflow is complete (Step 5 finished).
    /// </summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// Human-readable status message for UI display.
    /// Examples: "Generating research brief...", "Refining findings...", "Research complete"
    /// </summary>
    public required string StatusMessage { get; init; }

    /// <summary>
    /// The formatted content to display for THIS step.
    /// UI can show this as a preview/summary of what just completed.
    /// Full details are available in the specific property (ResearchBrief, DraftReport, etc.)
    /// </summary>
    public string DisplayContent { get; init; } = string.Empty;

    /// <summary>
    /// Optional metrics about this step execution (duration, content length, etc.).
    /// </summary>
    public Dictionary<string, object>? Metrics { get; init; }
}
