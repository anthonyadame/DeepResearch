using System.ComponentModel;
using DeepResearchAgent.Services;
using DeepResearchAgent.Models;

namespace DeepResearchAgent.Tools;

/// <summary>
/// Tool definitions for the research agent system.
/// These replace Python's @tool decorators with C# attributes and AIFunctionFactory pattern.
/// </summary>
public static class ResearchTools
{
    /// <summary>
    /// A tool for delegating a specific research task to a specialized sub-agent.
    /// The Supervisor uses this to fan out work.
    /// </summary>
    [Description("Conduct research on a specific topic")]
    public static string ConductResearch(
        [Description("The topic to research. Should be a single, self-contained topic described in high detail.")]
        string researchTopic)
    {
        // This will be implemented to invoke the researcher sub-workflow
        return $"Research delegated for topic: {researchTopic}";
    }

    /// <summary>
    /// A tool for the Supervisor to signal that the research process is complete 
    /// and the final report can be generated.
    /// </summary>
    [Description("Signal that research is complete")]
    public static string ResearchComplete()
    {
        return "Research marked as complete";
    }

    /// <summary>
    /// Tool for strategic reflection on research progress and decision-making.
    /// Creates a deliberate pause in the research workflow for quality decision-making.
    /// </summary>
    [Description("Think strategically about research progress and plan next steps")]
    public static string ThinkTool(
        [Description("Your detailed reflection on research progress, findings, gaps, and next steps.")]
        string reflection)
    {
        return $"Reflection recorded: {reflection}";
    }

    /// <summary>
    /// Refine draft report by synthesizing findings.
    /// This is the core denoising step in the diffusion process.
    /// </summary>
    [Description("Refine the draft report with new research findings")]
    public static string RefineDraftReport(
        string researchBrief,
        string findings,
        string draftReport)
    {
        // This will be implemented to call the LLM with the refinement prompt
        return "Draft report refined";
    }
}
