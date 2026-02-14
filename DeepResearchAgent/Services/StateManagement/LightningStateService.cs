using DeepResearchAgent.Services;
using DeepResearchAgent.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeepResearchAgent.Services.StateManagement;

/// <summary>
/// Centralized state management using Agent-Lightning for distributed coordination.
/// Implements multi-level caching: Local (5min) -> Redis (15min) -> Lightning Server (persistent).
/// </summary>
public interface ILightningStateService
{
    // Agent State Operations
    Task<AgentStateModel> GetAgentStateAsync(string agentId, CancellationToken ct = default);
    Task SetAgentStateAsync(string agentId, AgentStateModel state, CancellationToken ct = default);
    Task<bool> UpdateAgentStatusAsync(string agentId, AgentStatus newStatus, CancellationToken ct = default);
    Task<Dictionary<string, AgentStateModel>> GetMultipleAgentStatesAsync(CancellationToken ct = default, params string[] agentIds);

    // Research State Operations
    Task<ResearchStateModel> GetResearchStateAsync(string researchId, CancellationToken ct = default);
    Task SetResearchStateAsync(string researchId, ResearchStateModel state, CancellationToken ct = default);
    Task<bool> UpdateResearchProgressAsync(string researchId, int iterationCount, double qualityScore, CancellationToken ct = default);

    // Verification State Operations
    Task<VerificationStateModel> GetVerificationStateAsync(string verificationId, CancellationToken ct = default);
    Task SetVerificationStateAsync(string verificationId, VerificationStateModel state, CancellationToken ct = default);
    Task<List<VerificationStateModel>> GetVerificationsForSourceAsync(string sourceId, CancellationToken ct = default);

    // Cache Management
    Task InvalidateCacheAsync(string key, CancellationToken ct = default);
    Task InvalidateCategoryAsync(string category, CancellationToken ct = default);
    
    // Metrics
    StateManagementMetrics GetMetrics();
}

/// <summary>
/// Agent state model - represents the state of a single agent.
/// </summary>
public class AgentStateModel
{
    public string AgentId { get; set; } = string.Empty;
    public string AgentType { get; set; } = string.Empty;
    public AgentStatus Status { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public List<string> ActiveTaskIds { get; set; } = new();
    public Dictionary<string, double> PerformanceMetrics { get; set; } = new();
    public DateTime LastUpdated { get; set; }
    public int Version { get; set; }
}

/// <summary>
/// Research state model - represents the state of a research task.
/// </summary>
public class ResearchStateModel
{
    public string ResearchId { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public ResearchStatus Status { get; set; }
    public List<FactState> ExtractedFacts { get; set; } = new();
    public List<string> Sources { get; set; } = new();
    public double CurrentQualityScore { get; set; }
    public int IterationCount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Verification state model - represents verification results.
/// </summary>
public class VerificationStateModel
{
    public string VerificationId { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public bool IsVerified { get; set; }
    public List<string> Issues { get; set; } = new();
    public DateTime VerifiedAt { get; set; }
    public string VerifiedBy { get; set; } = string.Empty;
}

public enum AgentStatus
{
    Initializing,
    Ready,
    Processing,
    Paused,
    Completed,
    Failed
}

public enum ResearchStatus
{
    Pending,
    InProgress,
    Verifying,
    Completed,
    Failed
}

/// <summary>
/// Metrics for state management operations.
/// </summary>
public class StateManagementMetrics
{
    private long _cacheHits = 0;
    private long _cacheMisses = 0;
    private readonly ConcurrentDictionary<string, long> _operationCounts = new();
    private readonly ConcurrentDictionary<string, long> _operationTotalMs = new();

    public double CacheHitRate =>
        (_cacheHits + _cacheMisses) > 0
            ? (double)_cacheHits / (_cacheHits + _cacheMisses)
            : 0;

    public long TotalCacheHits => _cacheHits;
    public long TotalCacheMisses => _cacheMisses;

    public void RecordCacheHit() => Interlocked.Increment(ref _cacheHits);
    public void RecordCacheMiss() => Interlocked.Increment(ref _cacheMisses);

    public void RecordOperation(string operationName, long durationMs)
    {
        _operationCounts.AddOrUpdate(operationName, 1, (k, v) => v + 1);
        _operationTotalMs.AddOrUpdate(operationName, durationMs, (k, v) => v + durationMs);
    }

    public double GetAverageOperationDuration(string operationName)
    {
        if (_operationCounts.TryGetValue(operationName, out var count) &&
            _operationTotalMs.TryGetValue(operationName, out var total))
        {
            return (double)total / count;
        }
        return 0;
    }
}

/// <summary>
/// Lightning-based state service with multi-level caching and APO optimization.
/// </summary>
public class LightningStateService : ILightningStateService
{
    private readonly IAgentLightningService _lightningService;
    private readonly ILightningVERLService _verlService;
    private readonly IMemoryCache _cache;
    private readonly StateManagementMetrics _metrics;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string AGENT_STATE_PREFIX = "agent_state:";
    private const string RESEARCH_STATE_PREFIX = "research_state:";
    private const string VERIFICATION_STATE_PREFIX = "verification_state:";
    private const int CACHE_DURATION_MINUTES = 5;

    public LightningStateService(
        IAgentLightningService lightningService,
        ILightningVERLService verlService,
        IMemoryCache cache)
    {
        _lightningService = lightningService;
        _verlService = verlService;
        _cache = cache;
        _metrics = new StateManagementMetrics();
        _locks = new ConcurrentDictionary<string, SemaphoreSlim>();
        _jsonOptions = new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    #region Agent State Management

    public async Task<AgentStateModel> GetAgentStateAsync(string agentId, CancellationToken ct = default)
    {
        var cacheKey = $"{AGENT_STATE_PREFIX}{agentId}";

        if (_cache.TryGetValue(cacheKey, out AgentStateModel? cached) && cached != null)
        {
            _metrics.RecordCacheHit();
            return cached;
        }

        _metrics.RecordCacheMiss();

        try
        {
            var taskData = new AgentTask
            {
                Name = "GetAgentState",
                Input = new Dictionary<string, object> { { "agentId", agentId } }
            };

            var result = await _lightningService.SubmitTaskAsync(agentId, taskData);

            if (result.Result != null)
            {
                var state = JsonSerializer.Deserialize<AgentStateModel>(result.Result, _jsonOptions);
                if (state != null)
                {
                    _cache.Set(cacheKey, state, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
                    return state;
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get agent state for {agentId}", ex);
        }

        throw new InvalidOperationException($"Agent state not found for {agentId}");
    }

    public async Task SetAgentStateAsync(string agentId, AgentStateModel state, CancellationToken ct = default)
    {
        var lockObj = _locks.GetOrAdd($"lock:agent:{agentId}", _ => new SemaphoreSlim(1, 1));

        await lockObj.WaitAsync(ct);
        try
        {
            // Verify with VERL using EvaluateConfidenceAsync
            var json = JsonSerializer.Serialize(state, _jsonOptions);
            var confidence = await _verlService.EvaluateConfidenceAsync(json, "agent_state");

            if (confidence.Score < 0.7)
            {
                throw new InvalidOperationException($"State verification failed: Low confidence score {confidence.Score}");
            }

            var taskData = new AgentTask
            {
                Name = "SetAgentState",
                Input = new Dictionary<string, object> { { "state", state } }
            };

            await _lightningService.SubmitTaskAsync(agentId, taskData);

            var cacheKey = $"{AGENT_STATE_PREFIX}{agentId}";
            _cache.Set(cacheKey, state, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));

            _metrics.RecordOperation("SetAgentState", 0);
        }
        finally
        {
            lockObj.Release();
        }
    }

    public async Task<bool> UpdateAgentStatusAsync(string agentId, AgentStatus newStatus, CancellationToken ct = default)
    {
        try
        {
            var state = await GetAgentStateAsync(agentId, ct);
            state.Status = newStatus;
            state.LastUpdated = DateTime.UtcNow;
            state.Version++;

            await SetAgentStateAsync(agentId, state, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Dictionary<string, AgentStateModel>> GetMultipleAgentStatesAsync(
        CancellationToken ct = default,
        params string[] agentIds)
    {
        var results = new Dictionary<string, AgentStateModel>();
        var tasks = agentIds.Select(id => GetAgentStateAsync(id, ct)).ToList();

        await Task.WhenAll(tasks);

        for (int i = 0; i < agentIds.Length; i++)
        {
            results[agentIds[i]] = tasks[i].Result;
        }

        return results;
    }

    #endregion

    #region Research State Management

    public async Task<ResearchStateModel> GetResearchStateAsync(string researchId, CancellationToken ct = default)
    {
        var cacheKey = $"{RESEARCH_STATE_PREFIX}{researchId}";

        if (_cache.TryGetValue(cacheKey, out ResearchStateModel? cached) && cached != null)
        {
            _metrics.RecordCacheHit();
            return cached;
        }

        _metrics.RecordCacheMiss();

        try
        {
            var taskData = new AgentTask
            {
                Name = "GetResearchState",
                Input = new Dictionary<string, object> { { "researchId", researchId } }
            };

            var result = await _lightningService.SubmitTaskAsync("research-manager", taskData);

            if (result.Result != null)
            {
                var state = JsonSerializer.Deserialize<ResearchStateModel>(result.Result, _jsonOptions);
                if (state != null)
                {
                    _cache.Set(cacheKey, state, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
                    return state;
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get research state for {researchId}", ex);
        }

        throw new InvalidOperationException($"Research state not found for {researchId}");
    }

    public async Task SetResearchStateAsync(string researchId, ResearchStateModel state, CancellationToken ct = default)
    {
        var lockObj = _locks.GetOrAdd($"lock:research:{researchId}", _ => new SemaphoreSlim(1, 1));

        await lockObj.WaitAsync(ct);
        try
        {
            var taskData = new AgentTask
            {
                Name = "SetResearchState",
                Input = new Dictionary<string, object> { { "state", state } }
            };

            await _lightningService.SubmitTaskAsync("research-manager", taskData);

            var cacheKey = $"{RESEARCH_STATE_PREFIX}{researchId}";
            _cache.Set(cacheKey, state, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));

            _metrics.RecordOperation("SetResearchState", 0);
        }
        finally
        {
            lockObj.Release();
        }
    }

    public async Task<bool> UpdateResearchProgressAsync(
        string researchId,
        int iterationCount,
        double qualityScore,
        CancellationToken ct = default)
    {
        try
        {
            var state = await GetResearchStateAsync(researchId, ct);
            state.IterationCount = iterationCount;
            state.CurrentQualityScore = qualityScore;
            state.LastUpdated = DateTime.UtcNow;

            await SetResearchStateAsync(researchId, state, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Verification State Management

    public async Task<VerificationStateModel> GetVerificationStateAsync(string verificationId, CancellationToken ct = default)
    {
        var cacheKey = $"{VERIFICATION_STATE_PREFIX}{verificationId}";

        if (_cache.TryGetValue(cacheKey, out VerificationStateModel? cached) && cached != null)
        {
            _metrics.RecordCacheHit();
            return cached;
        }

        _metrics.RecordCacheMiss();

        try
        {
            var taskData = new AgentTask
            {
                Name = "GetVerificationState",
                Input = new Dictionary<string, object> { { "verificationId", verificationId } }
            };

            var result = await _lightningService.SubmitTaskAsync("verification-manager", taskData);

            if (result.Result != null)
            {
                var state = JsonSerializer.Deserialize<VerificationStateModel>(result.Result, _jsonOptions);
                if (state != null)
                {
                    _cache.Set(cacheKey, state, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
                    return state;
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get verification state for {verificationId}", ex);
        }

        throw new InvalidOperationException($"Verification state not found for {verificationId}");
    }

    public async Task SetVerificationStateAsync(string verificationId, VerificationStateModel state, CancellationToken ct = default)
    {
        var lockObj = _locks.GetOrAdd($"lock:verification:{verificationId}", _ => new SemaphoreSlim(1, 1));

        await lockObj.WaitAsync(ct);
        try
        {
            var taskData = new AgentTask
            {
                Name = "SetVerificationState",
                Input = new Dictionary<string, object> { { "state", state } }
            };

            var json = JsonSerializer.Serialize(state, _jsonOptions);
            var confidence = await _verlService.EvaluateConfidenceAsync(json, "verification_state");

            if (confidence.Score < 0.7)
            {
                throw new InvalidOperationException(
                    $"Verification failed: Low confidence score {confidence.Score}"
                );
            }

            await _lightningService.SubmitTaskAsync("verification-manager", taskData);

            var cacheKey = $"{VERIFICATION_STATE_PREFIX}{verificationId}";
            _cache.Set(cacheKey, state, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));

            _metrics.RecordOperation("SetVerificationState", 0);
        }
        finally
        {
            lockObj.Release();
        }
    }

    public async Task<List<VerificationStateModel>> GetVerificationsForSourceAsync(
        string sourceId,
        CancellationToken ct = default)
    {
        var cacheKey = $"verifications:{sourceId}";

        if (_cache.TryGetValue(cacheKey, out List<VerificationStateModel>? cached) && cached != null)
        {
            _metrics.RecordCacheHit();
            return cached;
        }

        _metrics.RecordCacheMiss();

        try
        {
            var taskData = new AgentTask
            {
                Name = "GetVerificationsForSource",
                Input = new Dictionary<string, object> { { "sourceId", sourceId } }
            };

            var result = await _lightningService.SubmitTaskAsync("verification-manager", taskData);

            if (result.Result != null)
            {
                var verifications = JsonSerializer.Deserialize<List<VerificationStateModel>>(
                    result.Result,
                    _jsonOptions
                );

                if (verifications != null)
                {
                    _cache.Set(cacheKey, verifications, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));
                    return verifications;
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get verifications for {sourceId}", ex);
        }

        return new List<VerificationStateModel>();
    }

    #endregion

    #region Cache Management

    public async Task InvalidateCacheAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);

        var taskData = new AgentTask
        {
            Name = "InvalidateCache",
            Input = new Dictionary<string, object> { { "key", key } }
        };

        await _lightningService.SubmitTaskAsync("cache-manager", taskData);
    }

    public async Task InvalidateCategoryAsync(string category, CancellationToken ct = default)
    {
        var taskData = new AgentTask
        {
            Name = "InvalidateCategoryCache",
            Input = new Dictionary<string, object> { { "category", category } }
        };

        await _lightningService.SubmitTaskAsync("cache-manager", taskData);
    }

    #endregion

    #region Metrics

    public StateManagementMetrics GetMetrics() => _metrics;

    #endregion
}
