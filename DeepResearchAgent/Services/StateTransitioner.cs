using DeepResearchAgent.Models;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services;

/// <summary>
/// Phase 5 State Management: Handles transitions between agent outputs and inputs.
/// Converts data between ResearcherAgent, AnalystAgent, and ReportAgent formats.
/// </summary>
public class StateTransitioner
{
    private readonly ILogger<StateTransitioner>? _logger;

    public StateTransitioner(ILogger<StateTransitioner>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Create AnalysisInput from ResearchOutput.
    /// Maps research findings to analyst input format.
    /// </summary>
    public AnalysisInput CreateAnalysisInput(
        ResearchOutput research,
        string topic,
        string researchBrief)
    {
        if (research == null)
            throw new ArgumentNullException(nameof(research));
        
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic cannot be empty", nameof(topic));

        _logger?.LogDebug("Creating AnalysisInput from ResearchOutput");

        var analysisInput = new AnalysisInput
        {
            Findings = research.Findings ?? new List<FactExtractionResult>(),
            Topic = topic,
            ResearchBrief = researchBrief ?? topic
        };

        _logger?.LogInformation(
            "Created AnalysisInput: {FindingCount} findings for topic '{Topic}'",
            analysisInput.Findings.Count,
            topic);

        return analysisInput;
    }

    /// <summary>
    /// Create ReportInput from ResearchOutput and AnalysisOutput.
    /// Combines research and analysis for report generation.
    /// </summary>
    public ReportInput CreateReportInput(
        ResearchOutput research,
        AnalysisOutput analysis,
        string topic,
        string? author = null)
    {
        if (research == null)
            throw new ArgumentNullException(nameof(research));
        
        if (analysis == null)
            throw new ArgumentNullException(nameof(analysis));
        
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic cannot be empty", nameof(topic));

        _logger?.LogDebug("Creating ReportInput from ResearchOutput and AnalysisOutput");

        var reportInput = new ReportInput
        {
            Research = research,
            Analysis = analysis,
            Topic = topic,
            Author = author ?? "Deep Research Agent"
        };

        _logger?.LogInformation(
            "Created ReportInput: {FindingCount} findings, {InsightCount} insights for topic '{Topic}'",
            research.Findings?.Count ?? 0,
            analysis.KeyInsights?.Count ?? 0,
            topic);

        return reportInput;
    }

    /// <summary>
    /// Validate ResearchOutput has required data for analysis.
    /// Ensures research output is ready for analyst processing.
    /// </summary>
    public ValidationResult ValidateResearchOutput(ResearchOutput output)
    {
        var result = new ValidationResult { IsValid = true };

        if (output == null)
        {
            result.IsValid = false;
            result.Errors.Add("ResearchOutput is null");
            return result;
        }

        // Check for findings
        if (output.Findings == null || !output.Findings.Any())
        {
            result.IsValid = false;
            result.Errors.Add("ResearchOutput has no findings");
        }

        // Check for facts in findings
        var totalFacts = output.Findings?.Sum(f => f.Facts?.Count ?? 0) ?? 0;
        if (totalFacts == 0)
        {
            result.IsValid = false;
            result.Errors.Add("ResearchOutput findings contain no facts");
        }

        // Warning for low quality
        if (output.AverageQuality < 5.0f)
        {
            result.Warnings.Add($"ResearchOutput quality is low: {output.AverageQuality:F1}/10");
        }

        // Warning for low iterations
        if (output.IterationsUsed < 1)
        {
            result.Warnings.Add($"ResearchOutput used {output.IterationsUsed} iterations");
        }

        _logger?.LogDebug(
            "Validated ResearchOutput: {IsValid}, Errors: {ErrorCount}, Warnings: {WarningCount}",
            result.IsValid,
            result.Errors.Count,
            result.Warnings.Count);

        return result;
    }

    /// <summary>
    /// Validate AnalysisOutput has required data for reporting.
    /// Ensures analysis output is ready for report generation.
    /// </summary>
    public ValidationResult ValidateAnalysisOutput(AnalysisOutput output)
    {
        var result = new ValidationResult { IsValid = true };

        if (output == null)
        {
            result.IsValid = false;
            result.Errors.Add("AnalysisOutput is null");
            return result;
        }

        // Check for synthesis narrative
        if (string.IsNullOrWhiteSpace(output.SynthesisNarrative))
        {
            result.IsValid = false;
            result.Errors.Add("AnalysisOutput has no synthesis narrative");
        }

        // Check for key insights
        if (output.KeyInsights == null || !output.KeyInsights.Any())
        {
            result.Warnings.Add("AnalysisOutput has no key insights");
        }

        // Warning for low confidence
        if (output.ConfidenceScore < 0.5f)
        {
            result.Warnings.Add($"AnalysisOutput confidence is low: {output.ConfidenceScore:F2}");
        }

        _logger?.LogDebug(
            "Validated AnalysisOutput: {IsValid}, Errors: {ErrorCount}, Warnings: {WarningCount}",
            result.IsValid,
            result.Errors.Count,
            result.Warnings.Count);

        return result;
    }

    /// <summary>
    /// Validate complete pipeline data flow.
    /// Checks research → analysis → report readiness.
    /// </summary>
    public ValidationResult ValidatePipelineState(
        ResearchOutput research,
        AnalysisOutput? analysis = null,
        string? topic = null)
    {
        var result = new ValidationResult { IsValid = true };

        // Validate research
        var researchValidation = ValidateResearchOutput(research);
        if (!researchValidation.IsValid)
        {
            result.IsValid = false;
            result.Errors.AddRange(researchValidation.Errors);
        }
        result.Warnings.AddRange(researchValidation.Warnings);

        // Validate analysis if provided
        if (analysis != null)
        {
            var analysisValidation = ValidateAnalysisOutput(analysis);
            if (!analysisValidation.IsValid)
            {
                result.IsValid = false;
                result.Errors.AddRange(analysisValidation.Errors);
            }
            result.Warnings.AddRange(analysisValidation.Warnings);
        }

        // Validate topic if provided
        if (topic != null && string.IsNullOrWhiteSpace(topic))
        {
            result.Errors.Add("Topic is empty or whitespace");
            result.IsValid = false;
        }

        _logger?.LogInformation(
            "Validated pipeline state: {IsValid}, Errors: {ErrorCount}, Warnings: {WarningCount}",
            result.IsValid,
            result.Errors.Count,
            result.Warnings.Count);

        return result;
    }

    /// <summary>
    /// Extract summary statistics from ResearchOutput.
    /// Provides quick metrics for monitoring and logging.
    /// </summary>
    public ResearchStatistics GetResearchStatistics(ResearchOutput output)
    {
        if (output == null)
            return new ResearchStatistics();

        var stats = new ResearchStatistics
        {
            TotalFindings = output.Findings?.Count ?? 0,
            TotalFacts = output.Findings?.Sum(f => f.Facts?.Count ?? 0) ?? 0,
            AverageQuality = output.AverageQuality,
            IterationsUsed = output.IterationsUsed,
            AverageConfidence = output.Findings?
                .SelectMany(f => f.Facts ?? Enumerable.Empty<ExtractedFact>())
                .Average(f => f.Confidence) ?? 0f
        };

        return stats;
    }

    /// <summary>
    /// Extract summary statistics from AnalysisOutput.
    /// Provides quick metrics for monitoring and logging.
    /// </summary>
    public AnalysisStatistics GetAnalysisStatistics(AnalysisOutput output)
    {
        if (output == null)
            return new AnalysisStatistics();

        var stats = new AnalysisStatistics
        {
            TotalInsights = output.KeyInsights?.Count ?? 0,
            TotalThemes = output.ThemesIdentified?.Count ?? 0,
            TotalContradictions = output.Contradictions?.Count ?? 0,
            ConfidenceScore = output.ConfidenceScore,
            NarrativeLength = output.SynthesisNarrative?.Length ?? 0
        };

        return stats;
    }
}

/// <summary>
/// Result of validation operation.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Research output statistics.
/// </summary>
public class ResearchStatistics
{
    public int TotalFindings { get; set; }
    public int TotalFacts { get; set; }
    public float AverageQuality { get; set; }
    public int IterationsUsed { get; set; }
    public float AverageConfidence { get; set; }
}

/// <summary>
/// Analysis output statistics.
/// </summary>
public class AnalysisStatistics
{
    public int TotalInsights { get; set; }
    public int TotalThemes { get; set; }
    public int TotalContradictions { get; set; }
    public float ConfidenceScore { get; set; }
    public int NarrativeLength { get; set; }
}
