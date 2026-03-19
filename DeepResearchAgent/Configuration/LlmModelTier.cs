namespace DeepResearchAgent.Configuration;

/// <summary>
/// Defines LLM model tiers for workload-appropriate model selection.
/// Enables intelligent trade-off between latency and capability.
/// </summary>
public enum LlmModelTier
{
    /// <summary>
    /// Fast tier: 7B models for simple tasks (validation, format conversion, classification).
    /// Target latency: 3-5 seconds
    /// Use cases: ClarifyAgent, simple validation, format checks
    /// </summary>
    Fast = 0,

    /// <summary>
    /// Balanced tier: 14B models for medium complexity (summarization, brief generation).
    /// Target latency: 8-12 seconds
    /// Use cases: ResearchBriefAgent, DraftReportAgent, content summarization
    /// </summary>
    Balanced = 1,

    /// <summary>
    /// Power tier: 32B+ models for complex reasoning (analysis, synthesis, critique).
    /// Target latency: 20-30 seconds
    /// Use cases: SupervisorWorkflow (brain/redteam), ReportAgent, deep analysis
    /// </summary>
    Power = 2
}
