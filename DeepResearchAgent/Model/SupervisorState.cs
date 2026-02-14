namespace DeepResearchAgent.Models;

/// <summary>
/// The advanced, hierarchical state for the main Supervisor agent, 
/// the central workbench for the diffusion process.
/// </summary>
public class SupervisorState
{
    /// <summary>
    /// A standard field for accumulating the conversational history with the Supervisor.
    /// </summary>
    public List<ChatMessage> SupervisorMessages { get; set; } = new();

    /// <summary>
    /// The core artifacts of the research process that the Supervisor manages.
    /// </summary>
    public string ResearchBrief { get; set; } = string.Empty;

    /// <summary>
    /// The current draft report being refined through the diffusion process.
    /// </summary>
    public string DraftReport { get; set; } = string.Empty;

    /// <summary>
    /// This is a key memory management design. 'RawNotes' is a temporary, high-volume buffer
    /// for unprocessed search results.
    /// </summary>
    public List<string> RawNotes { get; set; } = new();

    /// <summary>
    /// 'KnowledgeBase' is the permanent, structured, and pruned storage.
    /// </summary>
    public List<FactState> KnowledgeBase { get; set; } = new();

    /// <summary>
    /// A simple counter to prevent infinite loops in our iterative process.
    /// </summary>
    public int ResearchIterations { get; set; } = 0;

    /// <summary>
    /// These fields manage the self-correction and adversarial feedback loops.
    /// </summary>
    public List<CritiqueState> ActiveCritiques { get; set; } = new();

    /// <summary>
    /// Quality history tracking for self-evolution.
    /// </summary>
    public List<QualityMetric> QualityHistory { get; set; } = new();

    /// <summary>
    /// A boolean flag that the Evaluator can set to signal to the Supervisor 
    /// that the draft quality is unacceptably low.
    /// </summary>
    public bool NeedsQualityRepair { get; set; } = false;
}
