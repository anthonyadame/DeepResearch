using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeepResearchAgent.Agents;
using DeepResearchAgent.Model;
using DeepResearchAgent.Services.Checkpointing;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services.Workflows;

/// <summary>
/// Implementation of Master Workflow Service for orchestrating multi-agent workflows.
/// Manages workflow definitions, execution state, checkpointing, and pause/resume.
/// </summary>
public class MasterWorkflowService : IMasterWorkflowService
{
    private readonly ICheckpointService _checkpointService;
    private readonly IWorkflowPauseResumeService _pauseResumeService;
    private readonly AgentPipelineService _agentPipeline;
    private readonly ILogger<MasterWorkflowService>? _logger;

    // In-memory storage (replace with database in production)
    private readonly ConcurrentDictionary<string, WorkflowDefinition> _workflowDefinitions = new();
    private readonly ConcurrentDictionary<string, WorkflowExecution> _executionHistory = new();
    private readonly ConcurrentDictionary<string, WorkflowExecution> _activeExecutions = new();

    public MasterWorkflowService(
        ICheckpointService checkpointService,
        IWorkflowPauseResumeService pauseResumeService,
        AgentPipelineService agentPipeline,
        ILogger<MasterWorkflowService>? logger = null)
    {
        _checkpointService = checkpointService;
        _pauseResumeService = pauseResumeService;
        _agentPipeline = agentPipeline;
        _logger = logger;
    }

    public async Task<WorkflowExecution> ExecuteWorkflowAsync(
        WorkflowDefinition workflow,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default)
    {
        try
        {
            var execution = new WorkflowExecution
            {
                WorkflowDefinition = workflow,
                Context = context ?? new(),
                State = WorkflowExecutionState.Initializing,
                StartedAt = DateTime.UtcNow
            };

            _activeExecutions.TryAdd(execution.ExecutionId, execution);

            _logger?.LogInformation(
                "Starting workflow execution: {ExecutionId} for workflow {WorkflowName}",
                execution.ExecutionId,
                workflow.Name);

            // Initialize workflow state machine
            await _pauseResumeService.TransitionWorkflowStateAsync(
                execution.ExecutionId,
                Model.WorkflowState.Running,
                "workflow-started",
                ct);

            execution.State = WorkflowExecutionState.Running;

            // Execute workflow steps
            for (int stepIndex = 0; stepIndex < workflow.Steps.Count; stepIndex++)
            {
                var step = workflow.Steps[stepIndex];
                execution.CurrentStepId = step.StepId;

                // Check if workflow is paused
                var pauseSignal = _pauseResumeService.GetSignal(execution.ExecutionId);
                if (pauseSignal?.PauseRequested == true)
                {
                    execution.State = WorkflowExecutionState.Paused;
                    _logger?.LogInformation(
                        "Workflow {ExecutionId} paused at step {StepId}",
                        execution.ExecutionId,
                        step.StepId);
                    break;
                }

                // Execute step with retry logic
                var record = await ExecuteStepAsync(execution, step, ct);
                execution.ExecutionHistory.Add(record);

                // Accumulate state
                if (record.AgentOutput != null)
                {
                    execution.AccumulatedState[step.StepId] = record.AgentOutput;
                }

                if (step.SaveCheckpointAfter)
                {
                    await SaveWorkflowCheckpointAsync(execution, ct);
                }

                // Check for failures
                if (record.Status == "Failed")
                {
                    execution.State = WorkflowExecutionState.Failed;
                    execution.ErrorMessage = record.ErrorMessage;
                    break;
                }

                // Evaluate conditional branching
                if (record.QualityScore.HasValue && step.ConditionalNextSteps.Any())
                {
                    var nextStepId = EvaluateBranching(record.QualityScore.Value, step);
                    if (nextStepId != null && nextStepId != "continue")
                    {
                        // Jump to specific step
                        var targetStep = workflow.Steps.FirstOrDefault(s => s.StepId == nextStepId);
                        if (targetStep != null)
                        {
                            stepIndex = workflow.Steps.IndexOf(targetStep) - 1;
                        }
                    }
                }
            }

            execution.CompletedAt = DateTime.UtcNow;
            if (execution.State != WorkflowExecutionState.Paused && 
                execution.State != WorkflowExecutionState.Failed)
            {
                execution.State = WorkflowExecutionState.Completed;
            }

            // Store execution history
            _executionHistory.TryAdd(execution.ExecutionId, execution);
            _activeExecutions.TryRemove(execution.ExecutionId, out _);

            _logger?.LogInformation(
                "Workflow {ExecutionId} completed with state {State}",
                execution.ExecutionId,
                execution.State);

            return execution;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing workflow {WorkflowName}", workflow.Name);
            throw;
        }
    }

    public async Task<WorkflowExecution?> GetWorkflowStatusAsync(
        string executionId,
        CancellationToken ct = default)
    {
        if (_activeExecutions.TryGetValue(executionId, out var active))
            return active;

        _executionHistory.TryGetValue(executionId, out var historical);
        return historical;
    }

    public async Task PauseWorkflowAsync(
        string executionId,
        string reason,
        CancellationToken ct = default)
    {
        await _pauseResumeService.RequestPauseAsync(executionId, ct);
        await _pauseResumeService.TransitionWorkflowStateAsync(
            executionId,
            Model.WorkflowState.Paused,
            reason,
            ct);

        _logger?.LogInformation(
            "Workflow {ExecutionId} pause requested: {Reason}",
            executionId,
            reason);
    }

    public async Task ResumeWorkflowAsync(
        string executionId,
        CancellationToken ct = default)
    {
        if (_activeExecutions.TryGetValue(executionId, out var execution))
        {
            execution.State = WorkflowExecutionState.Running;
            await _pauseResumeService.TransitionWorkflowStateAsync(
                executionId,
                Model.WorkflowState.Running,
                "workflow-resumed",
                ct);

            _logger?.LogInformation("Workflow {ExecutionId} resumed", executionId);
        }
    }

    public async Task CancelWorkflowAsync(
        string executionId,
        string reason,
        CancellationToken ct = default)
    {
        if (_activeExecutions.TryGetValue(executionId, out var execution))
        {
            execution.State = WorkflowExecutionState.Cancelled;
            execution.CompletedAt = DateTime.UtcNow;
            execution.ErrorMessage = reason;

            _activeExecutions.TryRemove(executionId, out _);
            _executionHistory.TryAdd(executionId, execution);

            _logger?.LogInformation(
                "Workflow {ExecutionId} cancelled: {Reason}",
                executionId,
                reason);
        }
    }

    public async Task<IReadOnlyList<WorkflowExecution>> GetWorkflowHistoryAsync(
        string? workflowName = null,
        string? agentFilter = null,
        int limit = 10,
        CancellationToken ct = default)
    {
        var query = _executionHistory.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(workflowName))
        {
            query = query.Where(e => e.WorkflowDefinition.Name == workflowName);
        }

        return query
            .OrderByDescending(e => e.StartedAt)
            .Take(limit)
            .ToList()
            .AsReadOnly();
    }

    public async Task<WorkflowAnalytics> GetWorkflowAnalyticsAsync(
        string executionId,
        CancellationToken ct = default)
    {
        if (!_executionHistory.TryGetValue(executionId, out var execution))
        {
            throw new ArgumentException($"Workflow execution not found: {executionId}");
        }

        var analytics = new WorkflowAnalytics
        {
            ExecutionId = executionId,
            TotalStepsPlanned = execution.WorkflowDefinition.Steps.Count,
            StepsCompleted = execution.ExecutionHistory.Count(r => r.Status == "Completed"),
            StepsFailed = execution.ExecutionHistory.Count(r => r.Status == "Failed"),
            TotalExecutionTime = execution.Duration ?? TimeSpan.Zero,
            TotalRetries = execution.RetryCount
        };

        // Calculate per-step times
        foreach (var record in execution.ExecutionHistory.Where(r => r.Duration.HasValue))
        {
            analytics.StepExecutionTimes[record.StepId] = record.Duration.Value;
        }

        // Calculate average quality score
        var qualityScores = execution.ExecutionHistory
            .Where(r => r.QualityScore.HasValue)
            .Select(r => r.QualityScore.Value)
            .ToList();

        if (qualityScores.Any())
        {
            analytics.AverageQualityScore = qualityScores.Average();
        }

        // Identify bottlenecks (steps taking > 50% of step execution time)
        var avgStepTime = analytics.StepExecutionTimes.Values.Any()
            ? TimeSpan.FromMilliseconds(
                analytics.StepExecutionTimes.Values.Average(t => t.TotalMilliseconds))
            : TimeSpan.Zero;

        analytics.BottleneckSteps = analytics.StepExecutionTimes
            .Where(kvp => kvp.Value > avgStepTime * 1.5)
            .Select(kvp => kvp.Key)
            .ToList();

        analytics.SuccessRate = analytics.StepsCompleted > 0
            ? (double)analytics.StepsCompleted / analytics.TotalStepsPlanned
            : 0;

        return analytics;
    }

    public async Task<WorkflowDefinition> RegisterWorkflowAsync(
        string name,
        string description,
        List<WorkflowStep> steps,
        List<string>? tags = null,
        CancellationToken ct = default)
    {
        var definition = new WorkflowDefinition
        {
            Name = name,
            Description = description,
            Steps = steps,
            Tags = tags ?? new(),
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        _workflowDefinitions.AddOrUpdate(name, definition, (k, v) =>
        {
            v.Version++;
            return definition;
        });

        _logger?.LogInformation(
            "Workflow definition registered: {WorkflowName} with {StepCount} steps",
            name,
            steps.Count);

        return definition;
    }

    public async Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(
        string workflowName,
        CancellationToken ct = default)
    {
        _workflowDefinitions.TryGetValue(workflowName, out var definition);
        return definition;
    }

    public async Task<IReadOnlyList<WorkflowDefinition>> ListWorkflowDefinitionsAsync(
        CancellationToken ct = default)
    {
        return _workflowDefinitions.Values.ToList().AsReadOnly();
    }

    public async Task DeleteWorkflowDefinitionAsync(
        string workflowName,
        CancellationToken ct = default)
    {
        _workflowDefinitions.TryRemove(workflowName, out _);
        _logger?.LogInformation("Workflow definition deleted: {WorkflowName}", workflowName);
    }

    // Helper Methods

    private async Task<StepExecutionRecord> ExecuteStepAsync(
        WorkflowExecution execution,
        WorkflowStep step,
        CancellationToken ct)
    {
        var record = new StepExecutionRecord
        {
            StepId = step.StepId,
            StartedAt = DateTime.UtcNow
        };

        int retryCount = 0;
        Exception? lastException = null;

        while (retryCount <= step.MaxRetries)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(step.TimeoutSeconds));

                record.ExecutionNumber = retryCount + 1;

                _logger?.LogInformation(
                    "Executing step {StepId} with agent {AgentName} (attempt {Attempt}/{MaxAttempts})",
                    step.StepId,
                    step.AgentName,
                    retryCount + 1,
                    step.MaxRetries + 1);

                // Execute agent (placeholder - would call actual agent)
                record.AgentOutput = $"[{step.AgentName}] Processed: {step.Input}";
                record.QualityScore = 0.8;
                record.Status = "Completed";

                record.CompletedAt = DateTime.UtcNow;
                return record;
            }
            catch (Exception ex)
            {
                lastException = ex;
                retryCount++;

                if (retryCount <= step.MaxRetries)
                {
                    await Task.Delay(step.RetryDelayMs, ct);
                }
                else
                {
                    record.Status = "Failed";
                    record.ErrorMessage = ex.Message;
                    record.CompletedAt = DateTime.UtcNow;
                    execution.RetryCount += retryCount;
                    return record;
                }
            }
        }

        record.Status = "Failed";
        record.ErrorMessage = lastException?.Message ?? "Unknown error";
        record.CompletedAt = DateTime.UtcNow;
        return record;
    }

    private async Task SaveWorkflowCheckpointAsync(
        WorkflowExecution execution,
        CancellationToken ct)
    {
        try
        {
            await _checkpointService.SaveCheckpointAsync(
                execution.ExecutionId,
                execution.WorkflowDefinition.Name,
                "MasterWorkflow",
                execution.ExecutionHistory.Count,
                System.Text.Json.JsonSerializer.Serialize(execution.AccumulatedState),
                new Model.CheckpointMetadata
                {
                    Reason = "workflow-checkpoint",
                    IsAutomated = true
                },
                ct);

            _logger?.LogDebug(
                "Checkpoint saved for workflow {ExecutionId}",
                execution.ExecutionId);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save checkpoint for workflow {ExecutionId}", execution.ExecutionId);
        }
    }

    private string? EvaluateBranching(double qualityScore, WorkflowStep step)
    {
        foreach (var condition in step.ConditionalNextSteps)
        {
            // Simple condition evaluation (quality < 0.5 => next_step, etc.)
            if (EvaluateCondition(condition.Key, qualityScore))
            {
                return condition.Value;
            }
        }

        return "continue"; // Continue to next step
    }

    private bool EvaluateCondition(string condition, double value)
    {
        // Simple evaluation (production would use expression parser)
        if (condition.Contains("<"))
        {
            var parts = condition.Split('<');
            if (double.TryParse(parts[1].Trim(), out var threshold))
            {
                return value < threshold;
            }
        }
        else if (condition.Contains(">"))
        {
            var parts = condition.Split('>');
            if (double.TryParse(parts[1].Trim(), out var threshold))
            {
                return value > threshold;
            }
        }

        return false;
    }
}
