using System.Collections.Generic;

namespace DeepResearchAgent.Configuration;

/// <summary>
/// Configuration for LLM model selection across different workflow functions.
/// Maps to multi-model vLLM deployment with LiteLLM routing.
/// Container 1: Qwen2.5-7B (vllm-qwen:8001) - Reasoning, quality, critique, 128K context
/// Container 2: Mistral-7B (vllm-mistral:8002) - Fast tools, research, pruning
/// </summary>
public class WorkflowModelConfiguration
{
    /// <summary>
    /// Model for supervisor brain decision-making (requires reasoning ability).
    /// Default: "qwen2.5-7b" (Qwen2.5-7B on vllm-qwen:8001)
    /// Performance: ~2,500 tok/s, 128K context, excellent reasoning
    /// </summary>
    public string SupervisorBrainModel { get; set; } = "qwen2.5-7b";

    /// <summary>
    /// Model for tool execution and research coordination.
    /// Default: "mistral-7b" (Mistral-7B on vllm-mistral:8002)
    /// Performance: ~2,500 tok/s, 32K context, fast and cost-effective
    /// </summary>
    public string SupervisorToolsModel { get; set; } = "mistral-7b";

    /// <summary>
    /// Model for quality evaluation and scoring (requires analysis).
    /// Default: "qwen2.5-7b" (Qwen2.5-7B - enhanced reasoning for evaluation)
    /// Performance: Superior analytical capability, 128K context for comprehensive analysis
    /// </summary>
    public string QualityEvaluatorModel { get; set; } = "qwen2.5-7b";

    /// <summary>
    /// Model for red team adversarial critique (requires reasoning).
    /// Default: "qwen2.5-7b" (Qwen2.5-7B - critical thinking capability)
    /// Performance: Latest generation Qwen with improved reasoning
    /// </summary>
    public string RedTeamModel { get; set; } = "qwen2.5-7b";

    /// <summary>
    /// Model for context pruning and fact extraction (requires understanding).
    /// Default: "mistral-7b" (Mistral-7B - fast comprehension and extraction)
    /// Performance: Good comprehension, ~2,500 tok/s for quick processing
    /// </summary>
    public string ContextPrunerModel { get; set; } = "mistral-7b";

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