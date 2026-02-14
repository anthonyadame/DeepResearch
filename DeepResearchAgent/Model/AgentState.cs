using System.Text.Json.Serialization;

namespace DeepResearchAgent.Models;

/// <summary>
/// The main state for the full multi-agent system, which accumulates all final artifacts.
/// </summary>
public class AgentState
{
    /// <summary>
    /// User-facing messages (conversation history).
    /// </summary>
    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = new();

    /// <summary>
    /// The research brief generated from user input.
    /// </summary>
    [JsonPropertyName("research_brief")]
    public string? ResearchBrief { get; set; }

    /// <summary>
    /// Supervisor-specific message history.
    /// </summary>
    [JsonPropertyName("supervisor_messages")]
    public List<ChatMessage> SupervisorMessages { get; set; } = new();

    /// <summary>
    /// Raw, unprocessed notes from all research activities.
    /// </summary>
    [JsonPropertyName("raw_notes")]
    public List<string> RawNotes { get; set; } = new();

    /// <summary>
    /// The final, curated notes for the writer (post-processing).
    /// </summary>
    [JsonPropertyName("notes")]
    public List<string> Notes { get; set; } = new();

    /// <summary>
    /// The initial draft report (pre-diffusion).
    /// </summary>
    [JsonPropertyName("draft_report")]
    public string DraftReport { get; set; } = string.Empty;

    /// <summary>
    /// The final polished report (post-diffusion).
    /// </summary>
    [JsonPropertyName("final_report")]
    public string FinalReport { get; set; } = string.Empty;

    [JsonPropertyName("supervisor_state")]
    public SupervisorState SupervisorState { get; set; } = new();

    [JsonPropertyName("needs_quality_repair")]
    public bool NeedsQualityRepair { get; set; } = true;
}

/// <summary>
/// The initial input state, which only contains the user's messages.
/// </summary>
public class AgentInputState
{
    public required List<ChatMessage> Messages { get; init; }
}
