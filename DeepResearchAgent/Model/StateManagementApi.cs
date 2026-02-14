namespace DeepResearchAgent.Models;

/// <summary>
/// Public API for the state management system. This file re-exports all state-related types
/// for easy consumption throughout the application.
/// 
/// The state management layer provides:
/// - Hierarchical state objects matching LangGraph's TypedDict patterns
/// - Accumulator semantics for list merging (raw_notes, messages, etc.)
/// - State validation and health checking
/// - State snapshots for debugging and analysis
/// - Factory methods for consistent state initialization
/// - Transition routing for workflow orchestration
/// </summary>

// Core State Models
public partial class DeepResearchStateApi
{
    // Agent States
    public static AgentState CreateAgentState() => StateFactory.CreateAgentState();
    public static AgentState CreateAgentState(List<ChatMessage> messages) => StateFactory.CreateAgentState(messages);
    public static AgentState CloneAgentState(AgentState original) => StateFactory.CloneAgentState(original);

    // Supervisor States
    public static SupervisorState CreateSupervisorState() => StateFactory.CreateSupervisorState();
    public static SupervisorState CreateSupervisorState(string brief, string draft, List<ChatMessage> messages)
        => StateFactory.CreateSupervisorState(brief, draft, messages);
    public static SupervisorState CloneSupervisorState(SupervisorState original) => StateFactory.CloneSupervisorState(original);

    // Researcher States
    public static ResearcherState CreateResearcherState() => StateFactory.CreateResearcherState();
    public static ResearcherState CreateResearcherState(string topic) => StateFactory.CreateResearcherState(topic);
    public static ResearcherState CreateResearcherState(string topic, List<ChatMessage> messages)
        => StateFactory.CreateResearcherState(topic, messages);
    public static ResearcherState CloneResearcherState(ResearcherState original) => StateFactory.CloneResearcherState(original);

    // Feedback & Metrics
    public static CritiqueState CreateCritique(string author, string concern, int severity, bool addressed = false)
        => StateFactory.CreateCritique(author, concern, severity, addressed);
    public static QualityMetric CreateQualityMetric(float score, string feedback, int iteration)
        => StateFactory.CreateQualityMetric(score, feedback, iteration);
    public static FactState CreateFact(string content, string url, int confidence, bool disputed = false)
        => StateFactory.CreateFact(content, url, confidence, disputed);

    // Validation
    public static StateValidator.ValidationResult ValidateAgentState(AgentState state) => StateValidator.ValidateAgentState(state);
    public static StateValidator.ValidationResult ValidateSupervisorState(SupervisorState state) => StateValidator.ValidateSupervisorState(state);
    public static StateValidator.ValidationResult ValidateResearcherState(ResearcherState state) => StateValidator.ValidateResearcherState(state);
    public static StateValidator.ValidationResult ValidateFact(FactState fact) => StateValidator.ValidateFact(fact);
    public static StateValidator.ValidationResult ValidateCritique(CritiqueState critique) => StateValidator.ValidateCritique(critique);
    public static StateHealthReport GetHealthReport(SupervisorState state) => StateValidator.GetHealthReport(state);
    public static bool ShouldContinueDiffusion(SupervisorState state, int maxIterations)
        => StateValidator.ShouldContinueDiffusion(state, maxIterations);
}
