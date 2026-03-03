using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services.Workflows;

/// <summary>Workflow execution metrics.</summary>
public class WorkflowMetrics
{
    public string WorkflowId { get; set; } = string.Empty;
    public TimeSpan TotalExecutionTime { get; set; }
    public Dictionary<string, TimeSpan> AgentExecutionTimes { get; set; } = new();
    public double QualityScore { get; set; }
    public int IterationCount { get; set; }
    public int ApiCallCount { get; set; }
    public double TokensUsed { get; set; }
    public DateTime CompletedAt { get; set; }
}

/// <summary>Agent performance rating.</summary>
public class AgentPerformanceRating
{
    public string AgentName { get; set; } = string.Empty;
    public int ExecutionCount { get; set; }
    public double AverageExecutionTime { get; set; }
    public double AverageQualityScore { get; set; }
    public double SuccessRate { get; set; }
}

/// <summary>Optimization recommendation.</summary>
public class OptimizationRecommendation
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double PotentialImprovementPercent { get; set; }
    public string Implementation { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium"; // Low, Medium, High, Critical
}

/// <summary>Analytics dashboard data.</summary>
public class AnalyticsDashboard
{
    public int TotalWorkflows { get; set; }
    public double AverageQualityScore { get; set; }
    public double AverageExecutionTime { get; set; }
    public Dictionary<string, int> AgentUsageCount { get; set; } = new();
    public List<OptimizationRecommendation> TopRecommendations { get; set; } = new();
}

/// <summary>Cost analysis.</summary>
public class CostAnalysis
{
    public string WorkflowId { get; set; } = string.Empty;
    public double EstimatedCost { get; set; }
    public int ApiCallsUsed { get; set; }
    public double TokensUsed { get; set; }
    public double CostPerIteration { get; set; }
}

/// <summary>Performance Analytics Service interface.</summary>
public interface IPerformanceAnalyticsService
{
    Task RecordWorkflowMetricsAsync(
        string workflowId,
        WorkflowMetrics metrics,
        CancellationToken ct = default);

    Task<WorkflowMetrics> GetWorkflowMetricsAsync(
        string workflowId,
        CancellationToken ct = default);

    Task<IReadOnlyList<AgentPerformanceRating>> GetAgentPerformanceAsync(
        int topN = 10,
        CancellationToken ct = default);

    Task<IReadOnlyList<OptimizationRecommendation>> GetRecommendationsAsync(
        string workflowId,
        CancellationToken ct = default);

    Task<AnalyticsDashboard> GetDashboardAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default);

    Task<CostAnalysis> GetCostAnalysisAsync(
        string workflowId,
        CancellationToken ct = default);
}

/// <summary>Implementation of Performance Analytics Service.</summary>
public class PerformanceAnalyticsService : IPerformanceAnalyticsService
{
    private readonly ILogger<PerformanceAnalyticsService>? _logger;

    private readonly ConcurrentDictionary<string, WorkflowMetrics> _metricsStorage = new();
    private readonly ConcurrentDictionary<string, List<WorkflowMetrics>> _historicalMetrics = new();

    public PerformanceAnalyticsService(
        ILogger<PerformanceAnalyticsService>? logger = null)
    {
        _logger = logger;
    }

    public async Task RecordWorkflowMetricsAsync(
        string workflowId,
        WorkflowMetrics metrics,
        CancellationToken ct = default)
    {
        _metricsStorage.AddOrUpdate(workflowId, metrics, (k, v) => metrics);

        // Store in history
        var history = _historicalMetrics.GetOrAdd(workflowId, _ => new List<WorkflowMetrics>());
        lock (history)
        {
            history.Add(metrics);
            if (history.Count > 100) // Keep last 100
            {
                history.RemoveAt(0);
            }
        }

        _logger?.LogInformation(
            "Metrics recorded for workflow {WorkflowId}: Execution time {Time}, Quality {Quality:F2}",
            workflowId,
            metrics.TotalExecutionTime,
            metrics.QualityScore);
    }

    public async Task<WorkflowMetrics> GetWorkflowMetricsAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        if (!_metricsStorage.TryGetValue(workflowId, out var metrics))
        {
            throw new KeyNotFoundException($"Metrics not found for workflow: {workflowId}");
        }

        return metrics;
    }

    public async Task<IReadOnlyList<AgentPerformanceRating>> GetAgentPerformanceAsync(
        int topN = 10,
        CancellationToken ct = default)
    {
        var agentMetrics = new Dictionary<string, List<WorkflowMetrics>>();

        foreach (var metrics in _metricsStorage.Values)
        {
            foreach (var agentTime in metrics.AgentExecutionTimes)
            {
                if (!agentMetrics.ContainsKey(agentTime.Key))
                {
                    agentMetrics[agentTime.Key] = new List<WorkflowMetrics>();
                }
                agentMetrics[agentTime.Key].Add(metrics);
            }
        }

        var ratings = agentMetrics
            .Select(kvp => new AgentPerformanceRating
            {
                AgentName = kvp.Key,
                ExecutionCount = kvp.Value.Count,
                AverageExecutionTime = kvp.Value
                    .Average(m => m.AgentExecutionTimes.TryGetValue(kvp.Key, out var t) ? t.TotalMilliseconds : 0),
                AverageQualityScore = kvp.Value.Average(m => m.QualityScore),
                SuccessRate = 0.95 // Placeholder
            })
            .OrderByDescending(r => r.AverageQualityScore)
            .Take(topN)
            .ToList();

        return ratings.AsReadOnly();
    }

    public async Task<IReadOnlyList<OptimizationRecommendation>> GetRecommendationsAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        if (!_metricsStorage.TryGetValue(workflowId, out var metrics))
        {
            return new List<OptimizationRecommendation>().AsReadOnly();
        }

        var recommendations = new List<OptimizationRecommendation>();

        // Identify slow steps
        var slowestStep = metrics.AgentExecutionTimes
            .OrderByDescending(kvp => kvp.Value)
            .FirstOrDefault();

        if (slowestStep.Value.TotalSeconds > 5)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Title = "Optimize Slow Agent",
                Description = $"Agent '{slowestStep.Key}' is taking {slowestStep.Value.TotalSeconds:F1}s",
                PotentialImprovementPercent = 20,
                Implementation = "Consider parallel execution or caching",
                Priority = "High"
            });
        }

        // Check quality score
        if (metrics.QualityScore < 0.7)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Title = "Improve Quality",
                Description = $"Quality score {metrics.QualityScore:F2} is below target",
                PotentialImprovementPercent = 15,
                Implementation = "Increase iterations or improve source quality",
                Priority = "Medium"
            });
        }

        return recommendations.AsReadOnly();
    }

    public async Task<AnalyticsDashboard> GetDashboardAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        var recentMetrics = _metricsStorage.Values
            .Where(m => m.CompletedAt >= from && m.CompletedAt <= to)
            .ToList();

        var dashboard = new AnalyticsDashboard
        {
            TotalWorkflows = recentMetrics.Count,
            AverageQualityScore = recentMetrics.Any() ? recentMetrics.Average(m => m.QualityScore) : 0,
            AverageExecutionTime = recentMetrics.Any()
                ? recentMetrics.Average(m => m.TotalExecutionTime.TotalSeconds)
                : 0
        };

        // Count agent usage
        foreach (var metrics in recentMetrics)
        {
            foreach (var agentTime in metrics.AgentExecutionTimes)
            {
                if (!dashboard.AgentUsageCount.ContainsKey(agentTime.Key))
                {
                    dashboard.AgentUsageCount[agentTime.Key] = 0;
                }
                dashboard.AgentUsageCount[agentTime.Key]++;
            }
        }

        // Get top recommendations
        var allRecommendations = new List<OptimizationRecommendation>();
        foreach (var metrics in recentMetrics)
        {
            var recs = await GetRecommendationsAsync(metrics.WorkflowId, ct);
            allRecommendations.AddRange(recs);
        }

        dashboard.TopRecommendations = allRecommendations
            .GroupBy(r => r.Title)
            .Select(g => g.First())
            .OrderByDescending(r => r.PotentialImprovementPercent)
            .Take(5)
            .ToList();

        return dashboard;
    }

    public async Task<CostAnalysis> GetCostAnalysisAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        if (!_metricsStorage.TryGetValue(workflowId, out var metrics))
        {
            throw new KeyNotFoundException($"Metrics not found for workflow: {workflowId}");
        }

        // Simplified cost calculation
        var costPerToken = 0.00001; // $0.00001 per token
        var costPerApiCall = 0.001; // $0.001 per API call

        var totalCost = (metrics.TokensUsed * costPerToken) + (metrics.ApiCallCount * costPerApiCall);

        return new CostAnalysis
        {
            WorkflowId = workflowId,
            EstimatedCost = totalCost,
            ApiCallsUsed = metrics.ApiCallCount,
            TokensUsed = metrics.TokensUsed,
            CostPerIteration = metrics.IterationCount > 0 ? totalCost / metrics.IterationCount : 0
        };
    }
}
