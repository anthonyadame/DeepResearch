using System.Collections.Generic;

namespace DeepResearchAgent.Configuration;

/// <summary>
/// Configuration for LLM model selection across different workflow functions.
/// Allows optimization of price/performance ratio for each specific task.
/// </summary>
public class WorkflowModelConfiguration
{
    /// <summary>
    /// Model for supervisor brain decision-making (requires reasoning ability).
    /// Default: "gpt-oss:20b" (balanced reasoning + speed)
    /// </summary>
    public string SupervisorBrainModel { get; set; } = "gpt-oss:20b";

    /// <summary>
    /// Model for tool execution and research coordination.
    /// Default: "mistral:7b" (fast, cost-effective)
    /// </summary>
    public string SupervisorToolsModel { get; set; } = "mistral:7b";

    /// <summary>
    /// Model for quality evaluation and scoring (requires analysis).
    /// Default: "gpt-oss:20b" (analytical capability)
    /// </summary>
    public string QualityEvaluatorModel { get; set; } = "gpt-oss:20b";

    /// <summary>
    /// Model for red team adversarial critique (requires reasoning).
    /// Default: "gpt-oss:20b" (critical thinking)
    /// </summary>
    public string RedTeamModel { get; set; } = "gpt-oss:20b";

    /// <summary>
    /// Model for context pruning and fact extraction (requires understanding).
    /// Default: "mistral:7b" (good comprehension, fast)
    /// </summary>
    public string ContextPrunerModel { get; set; } = "mistral:7b";

    /// <summary>
    /// Get a model for a specific workflow function.
    /// </summary>
    public string GetModelForFunction(WorkflowFunction function)
    {
        return function switch
        {
            WorkflowFunction.SupervisorBrain => SupervisorBrainModel,
            WorkflowFunction.SupervisorTools => SupervisorToolsModel,
            WorkflowFunction.QualityEvaluator => QualityEvaluatorModel,
            WorkflowFunction.RedTeam => RedTeamModel,
            WorkflowFunction.ContextPruner => ContextPrunerModel,
            _ => SupervisorBrainModel
        };
    }
}

/// <summary>
/// Enum representing different core functions in the supervisor workflow.
/// </summary>
public enum WorkflowFunction
{
    /// <summary>Supervisor brain for decision-making</summary>
    SupervisorBrain,

    /// <summary>Supervisor tools for research coordination</summary>
    SupervisorTools,

    /// <summary>Quality evaluator for scoring</summary>
    QualityEvaluator,

    /// <summary>Red team for adversarial critique</summary>
    RedTeam,

    /// <summary>Context pruner for fact extraction</summary>
    ContextPruner
}