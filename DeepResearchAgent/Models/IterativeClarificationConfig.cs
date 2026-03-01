namespace DeepResearchAgent.Models;

/// <summary>
/// Configuration for iterative clarification behavior.
/// Controls PromptWizard-style refinement parameters.
/// </summary>
public class IterativeClarificationConfig
{
    /// <summary>
    /// Maximum number of refinement iterations (default: 3)
    /// PromptWizard uses 3-5 iterations for optimal results
    /// </summary>
    public int MaxIterations { get; set; } = 3;

    /// <summary>
    /// Minimum quality threshold to proceed (0-100, default: 75)
    /// </summary>
    public double QualityThreshold { get; set; } = 75.0;

    /// <summary>
    /// Enable critique step for each iteration
    /// </summary>
    public bool EnableCritique { get; set; } = true;

    /// <summary>
    /// Enable quality metrics evaluation
    /// </summary>
    public bool EnableQualityMetrics { get; set; } = true;

    /// <summary>
    /// Maximum timeout for entire clarification process (seconds)
    /// </summary>
    public int MaxTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to use iterative agent (true) or standard agent (false)
    /// </summary>
    public bool UseIterativeAgent { get; set; } = false;

    /// <summary>
    /// Store iteration history for analysis
    /// </summary>
    public bool StoreIterationHistory { get; set; } = true;

    /// <summary>
    /// Minimum confidence for critique feedback (0.0-1.0)
    /// </summary>
    public double MinCritiqueConfidence { get; set; } = 0.6;
}
