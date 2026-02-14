namespace DeepResearchAgent.Models;

/// <summary>
/// Validates state objects to ensure consistency and correctness throughout the workflow.
/// Prevents invalid state transitions and catches bugs early.
/// </summary>
public class StateValidator
{
    /// <summary>
    /// Validation result with status and error messages.
    /// </summary>
    public record ValidationResult(bool IsValid, List<string> Errors)
    {
        public static ValidationResult Success() => new(true, new List<string>());
        public static ValidationResult Failure(params string[] errors) => new(false, errors.ToList());
        
        /// <summary>
        /// Gets a formatted message of all errors, or empty string if valid.
        /// </summary>
        public string Message => IsValid ? "" : string.Join("; ", Errors);
    }

    /// <summary>
    /// Validate an AgentState before workflow execution.
    /// </summary>
    public static ValidationResult ValidateAgentState(AgentState state)
    {
        var errors = new List<string>();

        if (state == null)
        {
            errors.Add("AgentState cannot be null");
            return new ValidationResult(false, errors);
        }

        if (state.Messages == null)
            errors.Add("Messages list cannot be null");
        else if (state.Messages.Count == 0)
            errors.Add("AgentState must have at least one user message");

        if (state.SupervisorMessages == null)
            errors.Add("SupervisorMessages list cannot be null");

        if (state.RawNotes == null)
            errors.Add("RawNotes list cannot be null");

        if (state.Notes == null)
            errors.Add("Notes list cannot be null");

        if (string.IsNullOrEmpty(state.DraftReport) && !string.IsNullOrEmpty(state.FinalReport))
            errors.Add("DraftReport should be populated before FinalReport");

        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(false, errors);
    }

    /// <summary>
    /// Validate a SupervisorState before diffusion loop execution.
    /// </summary>
    public static ValidationResult ValidateSupervisorState(SupervisorState state)
    {
        var errors = new List<string>();

        if (state == null)
        {
            errors.Add("SupervisorState cannot be null");
            return new ValidationResult(false, errors);
        }

        if (string.IsNullOrEmpty(state.ResearchBrief))
            errors.Add("ResearchBrief must be set before diffusion");

        if (string.IsNullOrEmpty(state.DraftReport))
            errors.Add("DraftReport must be populated before diffusion");

        if (state.SupervisorMessages == null)
            errors.Add("SupervisorMessages list cannot be null");

        if (state.RawNotes == null)
            errors.Add("RawNotes list cannot be null");

        if (state.KnowledgeBase == null)
            errors.Add("KnowledgeBase list cannot be null");

        if (state.ActiveCritiques == null)
            errors.Add("ActiveCritiques list cannot be null");

        if (state.QualityHistory == null)
            errors.Add("QualityHistory list cannot be null");

        if (state.ResearchIterations < 0)
            errors.Add("ResearchIterations cannot be negative");

        // Validate quality metrics if they exist
        foreach (var metric in state.QualityHistory ?? new List<QualityMetric>())
        {
            if (metric.Score < 0 || metric.Score > 10)
                errors.Add($"Quality score must be between 0-10, got {metric.Score}");

            if (string.IsNullOrEmpty(metric.Feedback))
                errors.Add("Quality metric feedback cannot be empty");
        }

        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(false, errors);
    }

    /// <summary>
    /// Validate a ResearcherState before research execution.
    /// </summary>
    public static ValidationResult ValidateResearcherState(ResearcherState state)
    {
        var errors = new List<string>();

        if (state == null)
        {
            errors.Add("ResearcherState cannot be null");
            return new ValidationResult(false, errors);
        }

        if (string.IsNullOrEmpty(state.ResearchTopic))
            errors.Add("ResearchTopic must be set");

        if (state.ResearcherMessages == null)
            errors.Add("ResearcherMessages list cannot be null");

        if (state.RawNotes == null)
            errors.Add("RawNotes list cannot be null");

        if (state.ToolCallIterations < 0)
            errors.Add("ToolCallIterations cannot be negative");

        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(false, errors);
    }

    /// <summary>
    /// Validate a FactState to ensure proper knowledge representation.
    /// </summary>
    public static ValidationResult ValidateFact(FactState fact)
    {
        var errors = new List<string>();

        if (fact == null)
        {
            errors.Add("FactState cannot be null");
            return new ValidationResult(false, errors);
        }

        if (string.IsNullOrWhiteSpace(fact.Content))
            errors.Add("Fact content cannot be empty");

        if (string.IsNullOrWhiteSpace(fact.SourceUrl))
            errors.Add("SourceUrl must be specified for traceability");

        if (fact.ConfidenceScore < 1 || fact.ConfidenceScore > 100)
            errors.Add($"Confidence score must be between 1-100, got {fact.ConfidenceScore}");

        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(false, errors);
    }

    /// <summary>
    /// Validate a CritiqueState for proper feedback structure.
    /// </summary>
    public static ValidationResult ValidateCritique(CritiqueState critique)
    {
        var errors = new List<string>();

        if (critique == null)
        {
            errors.Add("CritiqueState cannot be null");
            return new ValidationResult(false, errors);
        }

        if (string.IsNullOrWhiteSpace(critique.Author))
            errors.Add("Critique author must be specified");

        if (string.IsNullOrWhiteSpace(critique.Concern))
            errors.Add("Critique concern cannot be empty");

        if (critique.Severity < 1 || critique.Severity > 10)
            errors.Add($"Severity must be between 1-10, got {critique.Severity}");

        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(false, errors);
    }

    /// <summary>
    /// Validate a QualityMetric for proper scoring.
    /// </summary>
    public static ValidationResult ValidateQualityMetric(QualityMetric metric)
    {
        var errors = new List<string>();

        if (metric == null)
        {
            errors.Add("QualityMetric cannot be null");
            return new ValidationResult(false, errors);
        }

        if (metric.Score < 0 || metric.Score > 10)
            errors.Add($"Quality score must be between 0-10, got {metric.Score}");

        if (string.IsNullOrWhiteSpace(metric.Feedback))
            errors.Add("Feedback must be provided for each quality metric");

        if (metric.Iteration < 0)
            errors.Add("Iteration number cannot be negative");

        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(false, errors);
    }

    /// <summary>
    /// Validate all facts in a knowledge base.
    /// </summary>
    public static ValidationResult ValidateKnowledgeBase(IEnumerable<FactState> facts)
    {
        var errors = new List<string>();

        foreach (var fact in facts ?? new List<FactState>())
        {
            var factValidation = ValidateFact(fact);
            if (!factValidation.IsValid)
                errors.AddRange(factValidation.Errors);
        }

        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(false, errors);
    }

    /// <summary>
    /// Validate all critiques in the active feedback list.
    /// </summary>
    public static ValidationResult ValidateActiveCritiques(IEnumerable<CritiqueState> critiques)
    {
        var errors = new List<string>();

        foreach (var critique in critiques ?? new List<CritiqueState>())
        {
            var critiqueValidation = ValidateCritique(critique);
            if (!critiqueValidation.IsValid)
                errors.AddRange(critiqueValidation.Errors);
        }

        return errors.Count == 0 ? ValidationResult.Success() : new ValidationResult(false, errors);
    }

    /// <summary>
    /// Check if supervisor should continue the diffusion loop.
    /// </summary>
    public static bool ShouldContinueDiffusion(SupervisorState state, int maxIterations)
    {
        if (state.ResearchIterations >= maxIterations)
            return false;

        if (state.NeedsQualityRepair)
            return true;

        if (state.ActiveCritiques.Any(c => !c.Addressed))
            return true;

        var lastScore = state.QualityHistory.LastOrDefault()?.Score ?? 0;
        return lastScore < 8.0f; // Continue if quality is below excellent
    }

    /// <summary>
    /// Get a summary of state health.
    /// </summary>
    public static StateHealthReport GetHealthReport(SupervisorState state)
    {
        return new StateHealthReport
        {
            IsValid = ValidateSupervisorState(state).IsValid,
            ResearchIterations = state.ResearchIterations,
            ActiveCritiquesCount = state.ActiveCritiques.Count,
            UnaddressedCritiquesCount = state.ActiveCritiques.Count(c => !c.Addressed),
            KnowledgeBaseSize = state.KnowledgeBase.Count,
            AverageConfidence = state.KnowledgeBase.Any()
                ? state.KnowledgeBase.Average(f => f.ConfidenceScore)
                : 0,
            CurrentDraftQuality = state.QualityHistory.LastOrDefault()?.Score ?? 0,
            NeedsRepair = state.NeedsQualityRepair,
            DisputedFactsCount = state.KnowledgeBase.Count(f => f.IsDisputed)
        };
    }
}

/// <summary>
/// Health status report for supervisor state.
/// </summary>
public record StateHealthReport
{
    public bool IsValid { get; set; }
    public int ResearchIterations { get; set; }
    public int ActiveCritiquesCount { get; set; }
    public int UnaddressedCritiquesCount { get; set; }
    public int KnowledgeBaseSize { get; set; }
    public double AverageConfidence { get; set; }
    public float CurrentDraftQuality { get; set; }
    public bool NeedsRepair { get; set; }
    public int DisputedFactsCount { get; set; }

    public override string ToString()
    {
        return $@"State Health Report:
  Valid: {IsValid}
  Iterations: {ResearchIterations}
  Active Critiques: {ActiveCritiquesCount} (unaddressed: {UnaddressedCritiquesCount})
  Knowledge Base: {KnowledgeBaseSize} facts (avg confidence: {AverageConfidence:F1}%)
  Draft Quality: {CurrentDraftQuality:F1}/10
  Disputed Facts: {DisputedFactsCount}
  Needs Repair: {NeedsRepair}";
    }
}
