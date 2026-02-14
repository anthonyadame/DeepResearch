namespace DeepResearchAgent.Models;

/// <summary>
/// The state for the "worker" research agent sub-graph.
/// </summary>
public class ResearcherState
{
    /// <summary>
    /// Message history for the researcher agent.
    /// </summary>
    public List<ChatMessage> ResearcherMessages { get; set; } = new();

    /// <summary>
    /// Tool call iterations counter to prevent infinite loops.
    /// </summary>
    public int ToolCallIterations { get; set; } = 0;

    /// <summary>
    /// The specific research topic for this worker.
    /// </summary>
    public string ResearchTopic { get; set; } = string.Empty;

    /// <summary>
    /// The final, cleaned-up output of a research run.
    /// </summary>
    public string CompressedResearch { get; set; } = string.Empty;

    /// <summary>
    /// The temporary buffer of raw search results for this specific worker.
    /// </summary>
    public List<string> RawNotes { get; set; } = new();
}

/// <summary>
/// A specialized state defining the output of the research agent sub-graph.
/// </summary>
public record ResearcherOutputState
{
    public required string CompressedResearch { get; init; }
    public required List<string> RawNotes { get; init; }
    public required List<ChatMessage> ResearcherMessages { get; init; }
}
