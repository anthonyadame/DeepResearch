using System.ComponentModel.DataAnnotations;

namespace DeepResearchAgent.Configuration;

/// <summary>
/// Configuration for tiered LLM model selection.
/// Maps model tiers (Fast/Balanced/Power) to specific model names.
/// Enables workload-appropriate model selection for 60% LLM latency reduction.
/// </summary>
public class LlmModelTierConfiguration
{
    /// <summary>
    /// Fast tier model (7B) for simple tasks.
    /// Target: 3-5s latency for validation, classification, format conversion.
    /// Recommended: llama3.2:3b, mistral:7b, phi-3.5:mini
    /// </summary>
    [Required]
    public string FastModel { get; set; } = "llama3.2:3b";

    /// <summary>
    /// Balanced tier model (14B) for medium complexity.
    /// Target: 8-12s latency for summarization, brief generation, content structuring.
    /// Recommended: llama3.1:13b, mixtral:8x7b, qwen2.5:14b
    /// </summary>
    [Required]
    public string BalancedModel { get; set; } = "llama3.1:13b";

    /// <summary>
    /// Power tier model (32B+) for complex reasoning.
    /// Target: 20-30s latency for analysis, synthesis, critique, strategic decisions.
    /// Recommended: llama3.1:70b, qwen2.5:32b, mixtral:8x22b
    /// </summary>
    [Required]
    public string PowerModel { get; set; } = "qwen2.5:32b";

    /// <summary>
    /// Enable automatic tier selection based on task complexity.
    /// When true, agents specify tier instead of explicit model names.
    /// </summary>
    public bool EnableTierSelection { get; set; } = true;

    /// <summary>
    /// Enable tier-specific metrics tracking.
    /// Tracks latency and token usage separately by tier for optimization analysis.
    /// </summary>
    public bool EnableTierMetrics { get; set; } = true;

    /// <summary>
    /// Validates configuration settings.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FastModel))
            throw new InvalidOperationException("FastModel must be specified");

        if (string.IsNullOrWhiteSpace(BalancedModel))
            throw new InvalidOperationException("BalancedModel must be specified");

        if (string.IsNullOrWhiteSpace(PowerModel))
            throw new InvalidOperationException("PowerModel must be specified");
    }

    /// <summary>
    /// Get model name for specified tier.
    /// </summary>
    public string GetModelForTier(LlmModelTier tier)
    {
        return tier switch
        {
            LlmModelTier.Fast => FastModel,
            LlmModelTier.Balanced => BalancedModel,
            LlmModelTier.Power => PowerModel,
            _ => throw new ArgumentException($"Unknown model tier: {tier}", nameof(tier))
        };
    }

    /// <summary>
    /// Factory method: Development preset (smaller models for faster iteration).
    /// </summary>
    public static LlmModelTierConfiguration Development()
    {
        return new LlmModelTierConfiguration
        {
            FastModel = "llama3.2:3b",
            BalancedModel = "llama3.1:8b",  // Smaller than default for dev speed
            PowerModel = "llama3.1:13b",    // Smaller than default for dev speed
            EnableTierSelection = true,
            EnableTierMetrics = true
        };
    }

    /// <summary>
    /// Factory method: Production preset (optimized models for best results).
    /// </summary>
    public static LlmModelTierConfiguration Production()
    {
        return new LlmModelTierConfiguration
        {
            FastModel = "llama3.2:3b",
            BalancedModel = "llama3.1:13b",
            PowerModel = "qwen2.5:32b",
            EnableTierSelection = true,
            EnableTierMetrics = true
        };
    }

    /// <summary>
    /// Factory method: High-performance preset (largest models for maximum quality).
    /// </summary>
    public static LlmModelTierConfiguration HighPerformance()
    {
        return new LlmModelTierConfiguration
        {
            FastModel = "mistral:7b",
            BalancedModel = "mixtral:8x7b",
            PowerModel = "llama3.1:70b",
            EnableTierSelection = true,
            EnableTierMetrics = true
        };
    }

    /// <summary>
    /// Returns summary of configuration for logging/debugging.
    /// </summary>
    public string GetSummary()
    {
        return $"LlmModelTier Configuration:\n" +
               $"  Fast (7B):     {FastModel}\n" +
               $"  Balanced (14B): {BalancedModel}\n" +
               $"  Power (32B+):   {PowerModel}\n" +
               $"  Tier Selection: {(EnableTierSelection ? "Enabled" : "Disabled")}\n" +
               $"  Tier Metrics:   {(EnableTierMetrics ? "Enabled" : "Disabled")}";
    }
}
