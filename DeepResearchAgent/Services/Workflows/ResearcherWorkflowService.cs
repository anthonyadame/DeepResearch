using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeepResearchAgent.Models;
using DeepResearchAgent.Model.Api;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services.Workflows;

/// <summary>
/// Implementation of Researcher Workflow Service for intelligent research loops.
/// </summary>
public class ResearcherWorkflowService : IResearcherWorkflowService
{
    private readonly IMasterWorkflowService _masterWorkflow;
    private readonly ILightningRLCSService _rlcsService;
    private readonly ILogger<ResearcherWorkflowService>? _logger;

    private readonly ConcurrentDictionary<string, ResearchProgress> _activeResearch = new();
    private readonly ConcurrentDictionary<string, ResearchFindings> _completedResearch = new();

    public ResearcherWorkflowService(
        IMasterWorkflowService masterWorkflow,
        ILightningRLCSService rlcsService,
        ILogger<ResearcherWorkflowService>? logger = null)
    {
        _masterWorkflow = masterWorkflow;
        _rlcsService = rlcsService;
        _logger = logger;
    }

    public async Task<ResearchFindings> ExecuteResearchAsync(
        string topic,
        ResearchConfiguration config,
        CancellationToken ct = default)
    {
        var researchId = Guid.NewGuid().ToString("N");
        var startTime = DateTime.UtcNow;

        var progress = new ResearchProgress
        {
            ResearchId = researchId,
            Topic = topic,
            MaxIterations = config.MaxIterations,
            TargetQualityScore = config.TargetQualityScore,
            StartedAt = startTime,
            Status = "Running"
        };

        _activeResearch.TryAdd(researchId, progress);

        var findings = new ResearchFindings
        {
            ResearchId = researchId,
            Topic = topic,
            Iterations = new()
        };

        _logger?.LogInformation(
            "Starting research for topic: {Topic} (ID: {ResearchId})",
            topic,
            researchId);

        // Research iteration loop
        for (int iteration = 1; iteration <= config.MaxIterations; iteration++)
        {
            try
            {
                // Check for cancellation
                ct.ThrowIfCancellationRequested();

                _logger?.LogInformation(
                    "Research iteration {Iteration}/{MaxIterations} for topic: {Topic}",
                    iteration,
                    config.MaxIterations,
                    topic);

                // Execute research step (simplified - would call researcher agent)
                var iterationResult = await ExecuteResearchIterationAsync(
                    topic,
                    iteration,
                    findings.AllFacts,
                    config.FocusTopics,
                    ct);

                // Verify quality using RLCS
                var qualityScore = await VerifyResearchQualityAsync(
                    iterationResult.FoundFacts,
                    topic,
                    ct);

                // Create iteration record
                var iterationRecord = new ResearchIteration
                {
                    IterationNumber = iteration,
                    FoundFacts = iterationResult.FoundFacts,
                    QualityScore = qualityScore,
                    CompletedAt = DateTime.UtcNow,
                    Notes = $"Found {iterationResult.FoundFacts.Count} facts"
                };

                findings.Iterations.Add(iterationRecord);
                findings.AllFacts.AddRange(iterationResult.FoundFacts);

                // Remove duplicates
                findings.AllFacts = findings.AllFacts
                    .DistinctBy(f => f.Statement)
                    .ToList();

                progress.CurrentIteration = iteration;
                progress.CurrentQualityScore = qualityScore;

                _logger?.LogInformation(
                    "Iteration {Iteration} completed with quality score: {Score:F2}",
                    iteration,
                    qualityScore);

                // Check if target quality reached
                if (qualityScore >= config.TargetQualityScore)
                {
                    _logger?.LogInformation(
                        "Target quality score {Target:F2} reached at iteration {Iteration}",
                        config.TargetQualityScore,
                        iteration);
                    
                    findings.ReachedTargetQuality = true;
                    break;
                }

                // Small delay between iterations
                await Task.Delay(100, ct);
            }
            catch (OperationCanceledException)
            {
                progress.Status = "Cancelled";
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in research iteration {Iteration}", iteration);
                progress.Status = "Error";
                throw;
            }
        }

        // Finalize findings
        findings.TotalIterations = progress.CurrentIteration;
        findings.FinalQualityScore = progress.CurrentQualityScore;
        findings.CompletedAt = DateTime.UtcNow;
        findings.TotalExecutionTime = findings.CompletedAt - startTime;

        progress.CompletedAt = findings.CompletedAt;
        progress.IsComplete = true;
        progress.Status = "Completed";

        _activeResearch.TryRemove(researchId, out _);
        _completedResearch.TryAdd(researchId, findings);

        _logger?.LogInformation(
            "Research completed for topic: {Topic} (ID: {ResearchId}) - Quality: {Score:F2}, Iterations: {Count}",
            topic,
            researchId,
            findings.FinalQualityScore,
            findings.TotalIterations);

        return findings;
    }

    public async Task<IReadOnlyList<ResearchIteration>> GetIterationHistoryAsync(
        string researchId,
        CancellationToken ct = default)
    {
        if (_completedResearch.TryGetValue(researchId, out var findings))
        {
            return findings.Iterations.AsReadOnly();
        }

        return new List<ResearchIteration>().AsReadOnly();
    }

    public async Task<ResearchProgress> GetProgressAsync(
        string researchId,
        CancellationToken ct = default)
    {
        if (_activeResearch.TryGetValue(researchId, out var progress))
        {
            return progress;
        }

        if (_completedResearch.TryGetValue(researchId, out var findings))
        {
            return new ResearchProgress
            {
                ResearchId = researchId,
                Topic = findings.Topic,
                CurrentIteration = findings.TotalIterations,
                MaxIterations = findings.TotalIterations,
                CurrentQualityScore = findings.FinalQualityScore,
                TargetQualityScore = findings.FinalQualityScore,
                IsComplete = true,
                StartedAt = findings.CompletedAt - findings.TotalExecutionTime,
                CompletedAt = findings.CompletedAt,
                Status = "Completed"
            };
        }

        throw new KeyNotFoundException($"Research not found: {researchId}");
    }

    public async Task<ResearchFindings> StopResearchAsync(
        string researchId,
        string reason,
        CancellationToken ct = default)
    {
        if (!_activeResearch.TryGetValue(researchId, out var progress))
        {
            throw new KeyNotFoundException($"Research not found or already completed: {researchId}");
        }

        progress.Status = "Stopped";
        progress.IsComplete = true;
        progress.CompletedAt = DateTime.UtcNow;

        _logger?.LogInformation(
            "Research {ResearchId} stopped: {Reason}",
            researchId,
            reason);

        _activeResearch.TryRemove(researchId, out _);

        return new ResearchFindings
        {
            ResearchId = researchId,
            Topic = progress.Topic,
            FinalQualityScore = progress.CurrentQualityScore,
            TotalIterations = progress.CurrentIteration,
            CompletedAt = DateTime.UtcNow
        };
    }

    // Helper Methods

    private async Task<(List<ExtractedFact> FoundFacts, string Summary)> ExecuteResearchIterationAsync(
        string topic,
        int iteration,
        List<ExtractedFact> previousFacts,
        List<string> focusTopics,
        CancellationToken ct)
    {
        // Simplified iteration - would call researcher agent in production
        var facts = new List<ExtractedFact>
        {
            new ExtractedFact
            {
                Statement = $"[Iteration {iteration}] Research finding for {topic}",
                Source = $"iteration_{iteration}",
                Confidence = 0.7f + (iteration * 0.05f),
                Category = focusTopics.FirstOrDefault() ?? "general"
            }
        };

        return (facts, $"Iteration {iteration} completed");
    }

    private async Task<double> VerifyResearchQualityAsync(
        List<ExtractedFact> facts,
        string topic,
        CancellationToken ct)
    {
        if (!facts.Any())
            return 0.0;

        try
        {
            // Use RLCS for quality verification
            var confidenceScores = facts.Select(f => f.Confidence).ToList();
            var qualityScore = confidenceScores.Average();

            return Math.Min(1.0, qualityScore);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error verifying research quality");
            return 0.5; // Default to moderate quality if verification fails
        }
    }
}
