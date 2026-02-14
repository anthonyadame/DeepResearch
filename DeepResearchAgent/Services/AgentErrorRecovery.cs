using DeepResearchAgent.Models;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services;

/// <summary>
/// Phase 5 Error Recovery: Handles agent failures and provides fallback strategies.
/// Ensures pipeline continues even when individual agents fail.
/// </summary>
public class AgentErrorRecovery
{
    private readonly ILogger<AgentErrorRecovery>? _logger;
    private readonly int _maxRetries;
    private readonly TimeSpan _retryDelay;

    public AgentErrorRecovery(
        ILogger<AgentErrorRecovery>? logger = null,
        int maxRetries = 2,
        TimeSpan? retryDelay = null)
    {
        _logger = logger;
        _maxRetries = maxRetries;
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
    }

    /// <summary>
    /// Execute agent with retry logic and fallback.
    /// Attempts execution up to maxRetries times before using fallback.
    /// </summary>
    public async Task<TOutput> ExecuteWithRetryAsync<TInput, TOutput>(
        Func<TInput, CancellationToken, Task<TOutput>> agentFunc,
        TInput input,
        Func<TInput, TOutput> fallbackFunc,
        string agentName,
        CancellationToken cancellationToken = default)
        where TOutput : class
    {
        int attempt = 0;
        Exception? lastException = null;

        while (attempt <= _maxRetries)
        {
            try
            {
                _logger?.LogDebug(
                    "Executing {AgentName}, attempt {Attempt}/{MaxAttempts}",
                    agentName,
                    attempt + 1,
                    _maxRetries + 1);

                var result = await agentFunc(input, cancellationToken);
                
                if (attempt > 0)
                {
                    _logger?.LogInformation(
                        "{AgentName} succeeded on attempt {Attempt}",
                        agentName,
                        attempt + 1);
                }

                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempt++;

                _logger?.LogWarning(ex,
                    "{AgentName} failed on attempt {Attempt}/{MaxAttempts}: {Message}",
                    agentName,
                    attempt,
                    _maxRetries + 1,
                    ex.Message);

                if (attempt <= _maxRetries)
                {
                    await Task.Delay(_retryDelay, cancellationToken);
                }
            }
        }

        // All retries exhausted, use fallback
        _logger?.LogError(lastException,
            "{AgentName} exhausted all {MaxRetries} retries, using fallback",
            agentName,
            _maxRetries + 1);

        try
        {
            var fallbackResult = fallbackFunc(input);
            _logger?.LogInformation("{AgentName} fallback executed successfully", agentName);
            return fallbackResult;
        }
        catch (Exception fallbackEx)
        {
            _logger?.LogError(fallbackEx, "{AgentName} fallback also failed", agentName);
            throw new AggregateException(
                $"{agentName} failed after {_maxRetries + 1} attempts and fallback also failed",
                lastException!,
                fallbackEx);
        }
    }

    /// <summary>
    /// Create fallback ResearchOutput for when ResearcherAgent fails.
    /// Returns minimal valid output to allow pipeline continuation.
    /// </summary>
    public ResearchOutput CreateFallbackResearchOutput(string topic, string errorMessage)
    {
        _logger?.LogWarning("Creating fallback ResearchOutput for topic: {Topic}", topic);

        return new ResearchOutput
        {
            Findings = new List<FactExtractionResult>
            {
                new()
                {
                    Facts = new List<ExtractedFact>
                    {
                        new()
                        {
                            Statement = $"Research on '{topic}' could not be completed due to an error: {errorMessage}",
                            Source = "error_fallback",
                            Confidence = 0.1f,
                            Category = "error"
                        }
                    }
                }
            },
            AverageQuality = 1.0f, // Low quality indicator
            IterationsUsed = 0
        };
    }

    /// <summary>
    /// Create fallback AnalysisOutput for when AnalystAgent fails.
    /// Returns minimal valid output to allow pipeline continuation.
    /// </summary>
    public AnalysisOutput CreateFallbackAnalysisOutput(string topic, string errorMessage)
    {
        _logger?.LogWarning("Creating fallback AnalysisOutput for topic: {Topic}", topic);

        return new AnalysisOutput
        {
            SynthesisNarrative = $"Analysis on '{topic}' could not be completed due to an error: {errorMessage}. " +
                                "This is a fallback response to ensure pipeline continuation.",
            KeyInsights = new List<KeyInsight>
            {
                new()
                {
                    Statement = "Analysis failed - fallback data",
                    Importance = 0.1f,
                    SourceFacts = new List<string>()
                }
            },
            Contradictions = new List<Contradiction>(),
            ConfidenceScore = 0.1f, // Low confidence indicator
            ThemesIdentified = new List<string> { "error_recovery" }
        };
    }

    /// <summary>
    /// Create fallback ReportOutput for when ReportAgent fails.
    /// Returns minimal valid output to complete pipeline.
    /// </summary>
    public ReportOutput CreateFallbackReportOutput(string topic, string errorMessage)
    {
        _logger?.LogWarning("Creating fallback ReportOutput for topic: {Topic}", topic);

        return new ReportOutput
        {
            Title = $"Research Report: {topic} (Error Recovery)",
            ExecutiveSummary = $"The report on '{topic}' could not be fully generated due to an error: {errorMessage}. " +
                              "This is a fallback report to ensure pipeline completion.",
            Sections = new List<ReportSection>
            {
                new()
                {
                    Heading = "Error Notice",
                    Content = $"Report generation encountered an error: {errorMessage}",
                    CitationIndices = new List<int>()
                }
            },
            Citations = new List<Citation>(),
            QualityScore = 0.1f, // Low quality indicator
            CompletionStatus = "completed_with_errors",
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Validate and repair ResearchOutput if possible.
    /// Attempts to fix common issues in research output.
    /// </summary>
    public ResearchOutput ValidateAndRepairResearchOutput(ResearchOutput output, string topic)
    {
        if (output == null)
        {
            _logger?.LogWarning("ResearchOutput is null, creating fallback");
            return CreateFallbackResearchOutput(topic, "Output was null");
        }

        bool needsRepair = false;

        // Repair null findings
        if (output.Findings == null)
        {
            _logger?.LogWarning("ResearchOutput.Findings is null, repairing");
            output.Findings = new List<FactExtractionResult>();
            needsRepair = true;
        }

        // Repair empty findings
        if (!output.Findings.Any())
        {
            _logger?.LogWarning("ResearchOutput has no findings, adding fallback");
            output.Findings.Add(new FactExtractionResult
            {
                Facts = new List<ExtractedFact>
                {
                    new()
                    {
                        Statement = $"No research findings available for '{topic}'",
                        Source = "repair_fallback",
                        Confidence = 0.3f,
                        Category = "fallback"
                    }
                }
            });
            needsRepair = true;
        }

        // Repair findings with null facts
        foreach (var finding in output.Findings.Where(f => f.Facts == null))
        {
            finding.Facts = new List<ExtractedFact>();
            needsRepair = true;
        }

        if (needsRepair)
        {
            _logger?.LogInformation("ResearchOutput repaired successfully");
        }

        return output;
    }

    /// <summary>
    /// Validate and repair AnalysisOutput if possible.
    /// Attempts to fix common issues in analysis output.
    /// </summary>
    public AnalysisOutput ValidateAndRepairAnalysisOutput(AnalysisOutput output, string topic)
    {
        if (output == null)
        {
            _logger?.LogWarning("AnalysisOutput is null, creating fallback");
            return CreateFallbackAnalysisOutput(topic, "Output was null");
        }

        bool needsRepair = false;

        // Repair null/empty synthesis narrative
        if (string.IsNullOrWhiteSpace(output.SynthesisNarrative))
        {
            _logger?.LogWarning("AnalysisOutput.SynthesisNarrative is empty, repairing");
            output.SynthesisNarrative = $"Analysis for '{topic}' is incomplete. Available data has been synthesized to the best extent possible.";
            needsRepair = true;
        }

        // Repair null collections
        if (output.KeyInsights == null)
        {
            output.KeyInsights = new List<KeyInsight>();
            needsRepair = true;
        }

        if (output.Contradictions == null)
        {
            output.Contradictions = new List<Contradiction>();
            needsRepair = true;
        }

        if (output.ThemesIdentified == null)
        {
            output.ThemesIdentified = new List<string>();
            needsRepair = true;
        }

        // Add default insight if none exist
        if (!output.KeyInsights.Any())
        {
            output.KeyInsights.Add(new KeyInsight
            {
                Statement = "Insufficient data for detailed insights",
                Importance = 0.3f,
                SourceFacts = new List<string>()
            });
            needsRepair = true;
        }

        if (needsRepair)
        {
            _logger?.LogInformation("AnalysisOutput repaired successfully");
        }

        return output;
    }

    /// <summary>
    /// Validate and repair ReportOutput if possible.
    /// Attempts to fix common issues in report output.
    /// </summary>
    public ReportOutput ValidateAndRepairReportOutput(ReportOutput output, string topic)
    {
        if (output == null)
        {
            _logger?.LogWarning("ReportOutput is null, creating fallback");
            return CreateFallbackReportOutput(topic, "Output was null");
        }

        bool needsRepair = false;

        // Repair null/empty title
        if (string.IsNullOrWhiteSpace(output.Title))
        {
            output.Title = $"Research Report: {topic}";
            needsRepair = true;
        }

        // Repair null/empty summary
        if (string.IsNullOrWhiteSpace(output.ExecutiveSummary))
        {
            output.ExecutiveSummary = $"This report covers research on '{topic}'. Full summary unavailable.";
            needsRepair = true;
        }

        // Repair null collections
        if (output.Sections == null)
        {
            output.Sections = new List<ReportSection>();
            needsRepair = true;
        }

        if (output.Citations == null)
        {
            output.Citations = new List<Citation>();
            needsRepair = true;
        }

        // Add default section if none exist
        if (!output.Sections.Any())
        {
            output.Sections.Add(new ReportSection
            {
                Heading = "Overview",
                Content = $"Research on '{topic}' is incomplete. Available information has been compiled.",
                CitationIndices = new List<int>()
            });
            needsRepair = true;
        }

        // Set creation date if not set
        if (output.CreatedAt == default)
        {
            output.CreatedAt = DateTime.UtcNow;
            needsRepair = true;
        }

        // Set completion status if empty
        if (string.IsNullOrWhiteSpace(output.CompletionStatus))
        {
            output.CompletionStatus = needsRepair ? "completed_with_repairs" : "completed";
        }

        if (needsRepair)
        {
            _logger?.LogInformation("ReportOutput repaired successfully");
        }

        return output;
    }

    /// <summary>
    /// Get error recovery statistics.
    /// Tracks fallbacks and repairs performed.
    /// </summary>
    public ErrorRecoveryStats GetStats()
    {
        // In a real implementation, this would track stats across requests
        return new ErrorRecoveryStats
        {
            TotalAttempts = 0,
            TotalRetries = 0,
            TotalFallbacks = 0,
            TotalRepairs = 0
        };
    }
}

/// <summary>
/// Error recovery statistics.
/// </summary>
public class ErrorRecoveryStats
{
    public int TotalAttempts { get; set; }
    public int TotalRetries { get; set; }
    public int TotalFallbacks { get; set; }
    public int TotalRepairs { get; set; }
}
