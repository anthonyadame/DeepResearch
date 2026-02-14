namespace DeepResearchAgent.Models;

using System.Collections.Concurrent;

/// <summary>
/// Manages hierarchical state transitions throughout the research workflow,
/// similar to LangGraph's state management but adapted for C# async patterns.
/// 
/// Key responsibilities:
/// 1. Maintains immutable snapshots of state at each iteration
/// 2. Provides accumulator semantics for lists (raw_notes, messages, etc.)
/// 3. Tracks state history for debugging and analysis
/// 4. Enables transactional updates across multi-agent systems
/// </summary>
public class StateManager
{
    private readonly ConcurrentDictionary<int, StateSnapshot> _history;
    private int _iterationCounter = 0;
    private readonly object _lockObject = new();

    public StateManager()
    {
        _history = new ConcurrentDictionary<int, StateSnapshot>();
    }

    /// <summary>
    /// Create a snapshot of the current agent state at a specific iteration.
    /// </summary>
    public StateSnapshot CaptureSnapshot(AgentState state, string phase)
    {
        lock (_lockObject)
        {
            var iteration = _iterationCounter++;
            var snapshot = new StateSnapshot
            {
                Iteration = iteration,
                Timestamp = DateTime.UtcNow,
                Phase = phase,
                ResearchBrief = state.ResearchBrief,
                DraftReport = state.DraftReport,
                FinalReport = state.FinalReport,
                MessageCount = state.Messages.Count,
                RawNotesCount = state.RawNotes.Count,
                SupervisorMessagesCount = state.SupervisorMessages.Count,
                NotesCount = state.Notes.Count,
                QualityScores = new List<float>()
            };

            _history.TryAdd(iteration, snapshot);
            return snapshot;
        }
    }

    /// <summary>
    /// Capture a supervisor state snapshot with full metrics.
    /// </summary>
    public SupervisorStateSnapshot CaptureSupervisorSnapshot(SupervisorState state, string phase)
    {
        lock (_lockObject)
        {
            var iteration = _iterationCounter;
            var snapshot = new SupervisorStateSnapshot
            {
                Iteration = iteration,
                Timestamp = DateTime.UtcNow,
                Phase = phase,
                ResearchBrief = state.ResearchBrief,
                DraftReport = state.DraftReport,
                ResearchIterations = state.ResearchIterations,
                MessageCount = state.SupervisorMessages.Count,
                RawNotesCount = state.RawNotes.Count,
                KnowledgeBaseCount = state.KnowledgeBase.Count,
                ActiveCritiquesCount = state.ActiveCritiques.Count,
                QualityHistoryCount = state.QualityHistory.Count,
                NeedsQualityRepair = state.NeedsQualityRepair,
                LastQualityScore = state.QualityHistory.LastOrDefault()?.Score ?? 0f
            };

            return snapshot;
        }
    }

    /// <summary>
    /// Update supervisor state with merged lists (accumulator pattern).
    /// </summary>
    public void MergeSupervisorState(SupervisorState target, SupervisorState source)
    {
        lock (_lockObject)
        {
            target.SupervisorMessages.AddRange(source.SupervisorMessages);
            target.RawNotes.AddRange(source.RawNotes);
            target.KnowledgeBase.AddRange(source.KnowledgeBase);
            target.ActiveCritiques.AddRange(source.ActiveCritiques);
            target.QualityHistory.AddRange(source.QualityHistory);
        }
    }

    /// <summary>
    /// Update agent state with merged lists.
    /// </summary>
    public void MergeAgentState(AgentState target, AgentState source)
    {
        lock (_lockObject)
        {
            target.Messages.AddRange(source.Messages);
            target.RawNotes.AddRange(source.RawNotes);
            target.Notes.AddRange(source.Notes);
            target.SupervisorMessages.AddRange(source.SupervisorMessages);
        }
    }

    /// <summary>
    /// Get the complete history of state transitions.
    /// </summary>
    public IReadOnlyDictionary<int, StateSnapshot> GetHistory()
    {
        return _history.AsReadOnly();
    }

    /// <summary>
    /// Get the supervisor state history.
    /// </summary>
    public IEnumerable<SupervisorStateSnapshot> GetSupervisorHistory()
    {
        return _history.Values
            .Where(s => s is SupervisorStateSnapshot)
            .Cast<SupervisorStateSnapshot>()
            .OrderBy(s => s.Iteration);
    }

    /// <summary>
    /// Clear history (useful for testing or long-running sessions).
    /// </summary>
    public void ClearHistory()
    {
        lock (_lockObject)
        {
            _history.Clear();
            _iterationCounter = 0;
        }
    }

    /// <summary>
    /// Get current iteration number.
    /// </summary>
    public int CurrentIteration
    {
        get
        {
            lock (_lockObject)
            {
                return _iterationCounter;
            }
        }
    }
}

/// <summary>
/// A point-in-time snapshot of the agent state.
/// </summary>
public class StateSnapshot
{
    public int Iteration { get; set; }
    public DateTime Timestamp { get; set; }
    public string Phase { get; set; } = string.Empty;
    public string? ResearchBrief { get; set; }
    public string DraftReport { get; set; } = string.Empty;
    public string FinalReport { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public int RawNotesCount { get; set; }
    public int SupervisorMessagesCount { get; set; }
    public int NotesCount { get; set; }
    public List<float> QualityScores { get; set; } = new();
}

/// <summary>
/// A point-in-time snapshot of the supervisor state during diffusion.
/// </summary>
public class SupervisorStateSnapshot
{
    public int Iteration { get; set; }
    public DateTime Timestamp { get; set; }
    public string Phase { get; set; } = string.Empty;
    public string ResearchBrief { get; set; } = string.Empty;
    public string DraftReport { get; set; } = string.Empty;
    public int ResearchIterations { get; set; }
    public int MessageCount { get; set; }
    public int RawNotesCount { get; set; }
    public int KnowledgeBaseCount { get; set; }
    public int ActiveCritiquesCount { get; set; }
    public int QualityHistoryCount { get; set; }
    public bool NeedsQualityRepair { get; set; }
    public float LastQualityScore { get; set; }
}
