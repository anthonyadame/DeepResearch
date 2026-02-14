using System.Diagnostics;
using DeepResearchAgent.Agents;
using DeepResearchAgent.Models;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services;

/// <summary>
/// Service for comparing performance between standard and iterative clarification agents.
/// Enables A/B testing and performance evaluation.
/// </summary>
public class ClarificationComparisonService
{
    private readonly ClarifyAgent _standardAgent;
    private readonly ClarifyIterativeAgent _iterativeAgent;
    private readonly ILogger<ClarificationComparisonService>? _logger;

    public ClarificationComparisonService(
        ClarifyAgent standardAgent,
        ClarifyIterativeAgent iterativeAgent,
        ILogger<ClarificationComparisonService>? logger = null)
    {
        _standardAgent = standardAgent ?? throw new ArgumentNullException(nameof(standardAgent));
        _iterativeAgent = iterativeAgent ?? throw new ArgumentNullException(nameof(iterativeAgent));
        _logger = logger;
    }

    /// <summary>
    /// Compare both agents side-by-side on the same conversation.
    /// </summary>
    public async Task<ComparisonResult> CompareAgentsAsync(
        List<ChatMessage> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("ClarificationComparison: Starting side-by-side comparison");

        var stopwatch = Stopwatch.StartNew();

        // Run standard agent
        var standardStopwatch = Stopwatch.StartNew();
        ClarificationResult? standardResult = null;
        Exception? standardError = null;
        
        try
        {
            standardResult = await _standardAgent.ClarifyAsync(conversationHistory, cancellationToken);
        }
        catch (Exception ex)
        {
            standardError = ex;
            _logger?.LogError(ex, "ClarificationComparison: Standard agent failed");
        }
        
        standardStopwatch.Stop();

        // Run iterative agent
        var iterativeStopwatch = Stopwatch.StartNew();
        IterativeClarificationResult? iterativeResult = null;
        Exception? iterativeError = null;
        
        try
        {
            iterativeResult = await _iterativeAgent.ClarifyWithIterationsAsync(conversationHistory, cancellationToken);
        }
        catch (Exception ex)
        {
            iterativeError = ex;
            _logger?.LogError(ex, "ClarificationComparison: Iterative agent failed");
        }
        
        iterativeStopwatch.Stop();
        stopwatch.Stop();

        var comparison = new ComparisonResult
        {
            StandardResult = standardResult,
            IterativeResult = iterativeResult,
            StandardDurationMs = standardStopwatch.Elapsed.TotalMilliseconds,
            IterativeDurationMs = iterativeStopwatch.Elapsed.TotalMilliseconds,
            StandardError = standardError?.Message,
            IterativeError = iterativeError?.Message,
            TotalComparisonDurationMs = stopwatch.Elapsed.TotalMilliseconds
        };

        LogComparisonResults(comparison);

        return comparison;
    }

    /// <summary>
    /// Log detailed comparison results for analysis.
    /// </summary>
    private void LogComparisonResults(ComparisonResult comparison)
    {
        _logger?.LogInformation(
            "ClarificationComparison Results:\n" +
            "  Standard Agent:\n" +
            "    Duration: {StandardDuration:F0}ms\n" +
            "    NeedClarification: {StandardNeedClarification}\n" +
            "    Question Length: {StandardQuestionLength} chars\n" +
            "  Iterative Agent:\n" +
            "    Duration: {IterativeDuration:F0}ms\n" +
            "    Iterations: {Iterations}\n" +
            "    QualityScore: {QualityScore:F1}\n" +
            "    NeedClarification: {IterativeNeedClarification}\n" +
            "    Question Length: {IterativeQuestionLength} chars\n" +
            "  Performance:\n" +
            "    Latency Overhead: {LatencyOverhead:F1}x\n" +
            "    Quality Improvement: {QualityImprovement:F1} points",
            comparison.StandardDurationMs,
            comparison.StandardResult?.NeedClarification ?? false,
            comparison.StandardResult?.Question?.Length ?? 0,
            comparison.IterativeDurationMs,
            comparison.IterativeResult?.IterationCount ?? 0,
            comparison.IterativeResult?.QualityMetrics?.OverallScore ?? 0,
            comparison.IterativeResult?.NeedClarification ?? false,
            comparison.IterativeResult?.Question?.Length ?? 0,
            comparison.LatencyOverheadMultiplier,
            comparison.QualityImprovement);
    }
}

/// <summary>
/// Results from comparing standard vs iterative clarification agents.
/// </summary>
public class ComparisonResult
{
    public ClarificationResult? StandardResult { get; init; }
    public IterativeClarificationResult? IterativeResult { get; init; }
    
    public double StandardDurationMs { get; init; }
    public double IterativeDurationMs { get; init; }
    
    public string? StandardError { get; init; }
    public string? IterativeError { get; init; }
    
    public double TotalComparisonDurationMs { get; init; }

    // Computed metrics
    public double LatencyOverheadMultiplier => 
        StandardDurationMs > 0 ? IterativeDurationMs / StandardDurationMs : 0;

    public double QualityImprovement => 
        IterativeResult?.QualityMetrics?.OverallScore ?? 0;

    public bool StandardSucceeded => StandardError == null;
    public bool IterativeSucceeded => IterativeError == null;

    public string Summary => 
        $"Standard: {StandardDurationMs:F0}ms | " +
        $"Iterative: {IterativeDurationMs:F0}ms ({IterativeResult?.IterationCount ?? 0} iterations) | " +
        $"Quality: {QualityImprovement:F1}/100 | " +
        $"Overhead: {LatencyOverheadMultiplier:F1}x";
}
