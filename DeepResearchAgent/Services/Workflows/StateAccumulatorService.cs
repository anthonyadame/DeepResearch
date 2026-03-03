using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeepResearchAgent.Model;
using DeepResearchAgent.Services.Checkpointing;
using Microsoft.Extensions.Logging;

namespace DeepResearchAgent.Services.Workflows;

/// <summary>State conflict resolution strategy.</summary>
public enum ConflictResolutionStrategy { KeepNew, KeepOld, Merge }

/// <summary>Immutable snapshot of workflow state at a point in time.</summary>
public class StateSnapshot
{
    public int Version { get; set; }
    public string SnapshotId { get; set; } = Guid.NewGuid().ToString("N");
    public Dictionary<string, object> State { get; set; } = new();
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public string TriggeringAgent { get; set; } = string.Empty;
}

/// <summary>Diff between two state versions.</summary>
public class StateDiff
{
    public List<string> Added { get; set; } = new();
    public List<string> Modified { get; set; } = new();
    public List<string> Removed { get; set; } = new();
    public Dictionary<string, string> Conflicts { get; set; } = new();
}

/// <summary>State Accumulator Service interface.</summary>
public interface IStateAccumulatorService
{
    Task<Dictionary<string, object>> AccumulateStateAsync(
        string workflowId,
        Dictionary<string, object> agentOutput,
        string agentName,
        CancellationToken ct = default);

    Task<Dictionary<string, object>> GetCurrentStateAsync(
        string workflowId,
        CancellationToken ct = default);

    Task<IReadOnlyList<StateSnapshot>> GetStateHistoryAsync(
        string workflowId,
        CancellationToken ct = default);

    Task<StateDiff> GetStateDiffAsync(
        string workflowId,
        int version1,
        int version2,
        CancellationToken ct = default);

    Task<Dictionary<string, object>> RollbackAsync(
        string workflowId,
        int targetVersion,
        CancellationToken ct = default);

    Task<Dictionary<string, object>> MergeStatesAsync(
        Dictionary<string, object> state1,
        Dictionary<string, object> state2,
        ConflictResolutionStrategy strategy,
        CancellationToken ct = default);
}

/// <summary>Implementation of State Accumulator Service.</summary>
public class StateAccumulatorService : IStateAccumulatorService
{
    private readonly ICheckpointService _checkpointService;
    private readonly ILogger<StateAccumulatorService>? _logger;

    private readonly ConcurrentDictionary<string, List<StateSnapshot>> _stateHistory = new();
    private readonly ConcurrentDictionary<string, Dictionary<string, object>> _currentStates = new();

    public StateAccumulatorService(
        ICheckpointService checkpointService,
        ILogger<StateAccumulatorService>? logger = null)
    {
        _checkpointService = checkpointService;
        _logger = logger;
    }

    public async Task<Dictionary<string, object>> AccumulateStateAsync(
        string workflowId,
        Dictionary<string, object> agentOutput,
        string agentName,
        CancellationToken ct = default)
    {
        var history = _stateHistory.GetOrAdd(workflowId, _ => new List<StateSnapshot>());
        var currentState = _currentStates.GetOrAdd(workflowId, _ => new Dictionary<string, object>());

        // Merge new output into current state
        var mergedState = MergeStatesInternal(currentState, agentOutput, ConflictResolutionStrategy.KeepNew);

        // Create snapshot
        var snapshot = new StateSnapshot
        {
            Version = history.Count + 1,
            State = new Dictionary<string, object>(mergedState),
            CapturedAt = DateTime.UtcNow,
            TriggeringAgent = agentName
        };

        history.Add(snapshot);
        _currentStates[workflowId] = mergedState;

        _logger?.LogDebug(
            "State accumulated for workflow {WorkflowId} from agent {Agent}, version {Version}",
            workflowId,
            agentName,
            snapshot.Version);

        return mergedState;
    }

    public async Task<Dictionary<string, object>> GetCurrentStateAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        if (_currentStates.TryGetValue(workflowId, out var state))
        {
            return new Dictionary<string, object>(state);
        }

        return new Dictionary<string, object>();
    }

    public async Task<IReadOnlyList<StateSnapshot>> GetStateHistoryAsync(
        string workflowId,
        CancellationToken ct = default)
    {
        if (_stateHistory.TryGetValue(workflowId, out var history))
        {
            return history.AsReadOnly();
        }

        return new List<StateSnapshot>().AsReadOnly();
    }

    public async Task<StateDiff> GetStateDiffAsync(
        string workflowId,
        int version1,
        int version2,
        CancellationToken ct = default)
    {
        if (!_stateHistory.TryGetValue(workflowId, out var history))
        {
            throw new KeyNotFoundException($"Workflow not found: {workflowId}");
        }

        var snap1 = history.FirstOrDefault(s => s.Version == version1);
        var snap2 = history.FirstOrDefault(s => s.Version == version2);

        if (snap1 == null || snap2 == null)
        {
            throw new ArgumentException($"Version not found for workflow {workflowId}");
        }

        var diff = new StateDiff();

        // Find added/modified keys
        foreach (var kvp in snap2.State)
        {
            if (!snap1.State.ContainsKey(kvp.Key))
            {
                diff.Added.Add(kvp.Key);
            }
            else if (!Equals(snap1.State[kvp.Key], kvp.Value))
            {
                diff.Modified.Add(kvp.Key);
            }
        }

        // Find removed keys
        foreach (var key in snap1.State.Keys)
        {
            if (!snap2.State.ContainsKey(key))
            {
                diff.Removed.Add(key);
            }
        }

        return diff;
    }

    public async Task<Dictionary<string, object>> RollbackAsync(
        string workflowId,
        int targetVersion,
        CancellationToken ct = default)
    {
        if (!_stateHistory.TryGetValue(workflowId, out var history))
        {
            throw new KeyNotFoundException($"Workflow not found: {workflowId}");
        }

        var targetSnapshot = history.FirstOrDefault(s => s.Version == targetVersion);
        if (targetSnapshot == null)
        {
            throw new ArgumentException($"Version {targetVersion} not found");
        }

        var rolledBackState = new Dictionary<string, object>(targetSnapshot.State);
        _currentStates[workflowId] = rolledBackState;

        _logger?.LogInformation(
            "Workflow {WorkflowId} rolled back to version {Version}",
            workflowId,
            targetVersion);

        return rolledBackState;
    }

    public async Task<Dictionary<string, object>> MergeStatesAsync(
        Dictionary<string, object> state1,
        Dictionary<string, object> state2,
        ConflictResolutionStrategy strategy,
        CancellationToken ct = default)
    {
        return MergeStatesInternal(state1, state2, strategy);
    }

    private Dictionary<string, object> MergeStatesInternal(
        Dictionary<string, object> state1,
        Dictionary<string, object> state2,
        ConflictResolutionStrategy strategy)
    {
        var merged = new Dictionary<string, object>(state1);

        foreach (var kvp in state2)
        {
            if (merged.ContainsKey(kvp.Key))
            {
                // Conflict resolution
                if (strategy == ConflictResolutionStrategy.KeepNew)
                {
                    merged[kvp.Key] = kvp.Value;
                }
                else if (strategy == ConflictResolutionStrategy.Merge && 
                         merged[kvp.Key] is List<object> list1 && 
                         kvp.Value is List<object> list2)
                {
                    // Merge lists
                    list1.AddRange(list2);
                }
                // else KeepOld - do nothing
            }
            else
            {
                merged[kvp.Key] = kvp.Value;
            }
        }

        return merged;
    }
}
