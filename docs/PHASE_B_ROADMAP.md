# Phase B Implementation Roadmap - Core Performance Optimization

## Executive Summary

**Goal:** Reduce total workflow execution time from 120s to 38s (68% improvement)  
**Timeline:** 4 weeks (can be parallelized)  
**Expected ROI:** 1000x more impactful than Phase A monitoring optimization

### Current Performance Breakdown
```
Total Workflow Time: 120s
├── LLM Operations: 108s (90%)
│   ├── Step 1 (Clarify): 15s
│   ├── Step 2 (Research Brief): 20s
│   ├── Step 3 (Draft): 25s
│   ├── Step 4 (Supervisor): 38s
│   └── Step 5 (Final): 10s
├── Tool Execution: 10s (8%)
│   ├── WebSearch: 6s
│   ├── Summarize: 2s
│   └── ExtractFacts: 2s
└── Monitoring: 2s (2%)
```

### Phase B Tracks

| Track | Target | Improvement | Complexity | Priority |
|-------|--------|-------------|------------|----------|
| **B1: Tiered LLM** | 43s | 60% faster | Medium | HIGH |
| **B2: Parallel Tools** | 35s | 50% faster | Low | HIGH |
| **B3: LLM Caching** | 24s | 80% cache hits | High | MEDIUM |

**Combined Target:** 38s (68% overall improvement)

---

## Track B1: Tiered LLM Model Selection

### Objective
Use smaller, faster models for simple tasks and reserve large models for complex reasoning.

### Model Tiers Strategy

```yaml
Tier 1 - Fast (7B models):
  Models: llama3.1:7b, mistral:7b
  Use Cases:
    - Query validation (Step 1: Clarify)
    - Simple tool calls
    - Format conversion
  Avg Speed: 50 tokens/sec
  Avg Latency: 3-5s

Tier 2 - Balanced (14B models):
  Models: llama3.1:14b, qwen2.5:14b
  Use Cases:
    - Research brief creation (Step 2)
    - Summarization
    - Fact extraction
  Avg Speed: 30 tokens/sec
  Avg Latency: 8-12s

Tier 3 - Power (32B+ models):
  Models: llama3.1:32b, qwen2.5:32b
  Use Cases:
    - Complex analysis (Step 4: Supervisor)
    - Final report generation (Step 5)
    - Multi-document synthesis
  Avg Speed: 15 tokens/sec
  Avg Latency: 20-30s
```

### Implementation Steps

#### Step 1: Create Model Tier Configuration (Week 1, Days 1-2)

**File:** `DeepResearchAgent/Configuration/LlmModelTierConfiguration.cs`

```csharp
public class LlmModelTierConfiguration
{
    public ModelTierDefinition Tier1Fast { get; set; } = new()
    {
        Models = new[] { "llama3.1:7b", "mistral:7b" },
        MaxTokens = 2048,
        Temperature = 0.7,
        UseFor = new[] { "validation", "simple_tool_calls", "format_conversion" }
    };

    public ModelTierDefinition Tier2Balanced { get; set; } = new()
    {
        Models = new[] { "llama3.1:14b", "qwen2.5:14b" },
        MaxTokens = 4096,
        Temperature = 0.7,
        UseFor = new[] { "summarization", "brief_creation", "fact_extraction" }
    };

    public ModelTierDefinition Tier3Power { get; set; } = new()
    {
        Models = new[] { "llama3.1:32b", "qwen2.5:32b" },
        MaxTokens = 8192,
        Temperature = 0.7,
        UseFor = new[] { "complex_analysis", "report_generation", "synthesis" }
    };

    public string GetModelForTask(string taskType)
    {
        // Selection logic based on task type
        // Returns appropriate model from tier
    }
}

public class ModelTierDefinition
{
    public string[] Models { get; set; }
    public int MaxTokens { get; set; }
    public double Temperature { get; set; }
    public string[] UseFor { get; set; }
}
```

#### Step 2: Update LLM Provider Interface (Week 1, Days 3-4)

**File:** `DeepResearchAgent/Services/LLM/ILlmProvider.cs`

Add model selection parameter:

```csharp
public interface ILlmProvider
{
    Task<string> CompleteAsync(
        string prompt, 
        CancellationToken cancellationToken = default,
        ModelTier? tier = null);  // NEW PARAMETER
}

public enum ModelTier
{
    Fast,      // 7B models
    Balanced,  // 14B models  
    Power      // 32B+ models
}
```

#### Step 3: Update Agent Implementations (Week 1, Day 5)

**Update Files:**
- `ClarifyAgent.cs` → Use `ModelTier.Fast`
- `ResearchBriefAgent.cs` → Use `ModelTier.Balanced`
- `DraftReportAgent.cs` → Use `ModelTier.Balanced`
- `SupervisorWorkflow.cs` → Use `ModelTier.Power`
- `ReportAgent.cs` → Use `ModelTier.Power`

**Example for ClarifyAgent:**

```csharp
var response = await _llmService.CompleteAsync(
    prompt, 
    cancellationToken,
    tier: ModelTier.Fast  // Use 7B model for simple validation
);
```

#### Step 4: Add Model Tier Metrics (Week 2, Day 1)

**File:** `DeepResearchAgent/Observability/DiagnosticConfig.cs`

```csharp
public static readonly Counter<long> LlmCallsByTier = Meter.CreateCounter<long>(
    "deepresearch.llm.calls.by_tier",
    description: "LLM calls by model tier");

public static readonly Histogram<double> LlmLatencyByTier = Meter.CreateHistogram<double>(
    "deepresearch.llm.latency.by_tier",
    unit: "ms",
    description: "LLM latency by model tier");
```

#### Step 5: Test and Validate (Week 2, Days 2-3)

- Run full workflow with tiered models
- Compare latency: 108s → 43s (expected)
- Validate output quality remains high
- Monitor model selection distribution

### Expected Impact (B1)

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| Step 1 (Clarify) | 15s | 5s | 67% faster |
| Step 2 (Brief) | 20s | 10s | 50% faster |
| Step 4 (Supervisor) | 38s | 25s | 34% faster |
| **Total LLM Time** | 108s | 43s | **60% faster** |
| **Total Workflow** | 120s | 55s | **54% faster** |

---

## Track B2: Parallel Tool Execution

### Objective
Execute independent tool calls concurrently instead of sequentially.

### Current Sequential Flow
```
SupervisorWorkflow Loop (38s per iteration):
├── WebSearch (6s)        ← Sequential
├── Summarize (2s)        ← Sequential
├── ExtractFacts (2s)     ← Sequential
└── RefineDraft (3s)      ← Sequential
Total: 13s per iteration × 3 iterations = 39s
```

### Proposed Parallel Flow
```
SupervisorWorkflow Loop (7s per iteration):
├── ┌─ WebSearch (6s)      ← Parallel
│   ├─ Summarize (2s)      ← Parallel
│   └─ ExtractFacts (2s)   ← Parallel
└── RefineDraft (3s)       ← After parallel
Total: max(6s, 2s, 2s) + 3s = 9s per iteration × 3 = 27s
```

### Implementation Steps

#### Step 1: Identify Independent Operations (Week 2, Day 4)

**Analysis of SupervisorWorkflow:**

```csharp
// CURRENT (Sequential - 13s)
var searchResults = await WebSearchAsync(query);     // 6s - Depends on query only
var summary = await SummarizeAsync(searchResults);   // 2s - Depends on searchResults
var facts = await ExtractFactsAsync(searchResults);  // 2s - Depends on searchResults
var draft = await RefineDraftAsync(facts, summary);  // 3s - Depends on facts + summary

// OPTIMIZED (Parallel - 9s)
var searchTask = WebSearchAsync(query);                      // Start immediately
var searchResults = await searchTask;                        // 6s

// These can run in parallel (both depend on searchResults only)
var summaryTask = SummarizeAsync(searchResults);            // Start together
var factsTask = ExtractFactsAsync(searchResults);           // Start together
await Task.WhenAll(summaryTask, factsTask);                 // Wait 2s (max of 2s,2s)

var draft = await RefineDraftAsync(
    await factsTask, 
    await summaryTask);                                      // 3s
```

#### Step 2: Create Parallel Execution Helper (Week 2, Day 5)

**File:** `DeepResearchAgent/Services/ParallelToolExecutor.cs`

```csharp
public class ParallelToolExecutor
{
    private readonly IWebSearchProvider _searchProvider;
    private readonly ILlmProvider _llmService;
    private readonly ILogger<ParallelToolExecutor> _logger;

    public async Task<(string summary, List<Fact> facts)> ProcessSearchResultsAsync(
        string searchResults,
        CancellationToken cancellationToken)
    {
        // Start both operations in parallel
        var summaryTask = SummarizeAsync(searchResults, cancellationToken);
        var factsTask = ExtractFactsAsync(searchResults, cancellationToken);

        // Wait for both to complete
        await Task.WhenAll(summaryTask, factsTask);

        return (await summaryTask, await factsTask);
    }

    public async Task<List<T>> ExecuteParallelToolsAsync<T>(
        IEnumerable<Func<Task<T>>> operations,
        CancellationToken cancellationToken)
    {
        var tasks = operations.Select(op => op()).ToList();
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }
}
```

#### Step 3: Update SupervisorWorkflow (Week 3, Day 1)

**File:** `DeepResearchAgent/Workflows/SupervisorWorkflow.cs`

```csharp
// OLD (Sequential)
var searchResults = await _toolService.WebSearchAsync(query, ct);
var summary = await _toolService.SummarizeAsync(searchResults, ct);
var facts = await _toolService.ExtractFactsAsync(searchResults, ct);

// NEW (Parallel)
var searchResults = await _toolService.WebSearchAsync(query, ct);

// Parallel processing
var (summary, facts) = await _parallelExecutor.ProcessSearchResultsAsync(
    searchResults, 
    ct);
```

#### Step 4: Add Parallel Execution Metrics (Week 3, Day 2)

```csharp
public static readonly Counter<long> ParallelOperations = Meter.CreateCounter<long>(
    "deepresearch.parallel.operations",
    description: "Number of parallel tool executions");

public static readonly Histogram<double> ParallelSpeedupRatio = Meter.CreateHistogram<double>(
    "deepresearch.parallel.speedup_ratio",
    description: "Speedup achieved by parallelization");
```

#### Step 5: Test and Validate (Week 3, Days 3-4)

- Compare sequential vs parallel execution
- Measure speedup ratio
- Validate no race conditions
- Check resource utilization (CPU, memory)

### Expected Impact (B2)

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| Tool Execution per iteration | 13s | 9s | 31% faster |
| 3 Supervisor iterations | 39s | 27s | 31% faster |
| **Total Workflow** | 55s (after B1) | 43s | **22% faster** |

---

## Track B3: LLM Response Caching

### Objective
Cache LLM responses for repeated queries to avoid redundant API calls.

### Caching Strategy

```yaml
Cache Key Components:
  - Prompt hash (SHA256)
  - Model name
  - Temperature
  - Max tokens

Cache Storage:
  - In-memory: MemoryCache (for current session)
  - Persistent: Redis or file-based (optional)

TTL (Time-To-Live):
  - Simple queries: 1 hour
  - Complex analyses: 30 minutes
  - Research briefs: 24 hours

Cache Size Limits:
  - Max entries: 1000
  - Max memory: 500MB
  - Eviction: LRU (Least Recently Used)
```

### Implementation Steps

#### Step 1: Create Caching Infrastructure (Week 3, Day 5)

**File:** `DeepResearchAgent/Services/LLM/LlmCacheService.cs`

```csharp
public class LlmCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<LlmCacheService> _logger;

    public async Task<string?> GetCachedResponseAsync(
        string prompt, 
        string model, 
        double temperature)
    {
        var cacheKey = GenerateCacheKey(prompt, model, temperature);
        return _cache.Get<string>(cacheKey);
    }

    public async Task SetCachedResponseAsync(
        string prompt, 
        string model, 
        double temperature,
        string response,
        TimeSpan? ttl = null)
    {
        var cacheKey = GenerateCacheKey(prompt, model, temperature);
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromHours(1),
            Size = response.Length
        };

        _cache.Set(cacheKey, response, options);
    }

    private string GenerateCacheKey(string prompt, string model, double temp)
    {
        var combined = $"{prompt}|{model}|{temp}";
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
        return Convert.ToBase64String(hash);
    }
}
```

#### Step 2: Add Cache Layer to LLM Provider (Week 4, Day 1)

**File:** `DeepResearchAgent/Services/LLM/CachedLlmProvider.cs`

```csharp
public class CachedLlmProvider : ILlmProvider
{
    private readonly ILlmProvider _innerProvider;
    private readonly LlmCacheService _cacheService;

    public async Task<string> CompleteAsync(
        string prompt, 
        CancellationToken cancellationToken = default,
        ModelTier? tier = null)
    {
        // Check cache first
        var cached = await _cacheService.GetCachedResponseAsync(
            prompt, 
            GetModelForTier(tier), 
            0.7);

        if (cached != null)
        {
            DiagnosticConfig.LlmCacheHits.Add(1);
            return cached;
        }

        // Cache miss - call actual LLM
        DiagnosticConfig.LlmCacheMisses.Add(1);
        var response = await _innerProvider.CompleteAsync(
            prompt, 
            cancellationToken, 
            tier);

        // Store in cache
        await _cacheService.SetCachedResponseAsync(
            prompt, 
            GetModelForTier(tier), 
            0.7, 
            response);

        return response;
    }
}
```

#### Step 3: Add Semantic Similarity Matching (Week 4, Days 2-3)

For similar but not identical queries:

```csharp
public class SemanticCacheService
{
    public async Task<string?> FindSimilarCachedResponse(
        string prompt, 
        double similarityThreshold = 0.85)
    {
        // Use embedding similarity to find close matches
        var embedding = await GetEmbedding(prompt);
        var similar = await FindSimilarEmbeddings(embedding, similarityThreshold);

        return similar?.CachedResponse;
    }
}
```

#### Step 4: Add Cache Metrics (Week 4, Day 4)

```csharp
public static readonly Counter<long> LlmCacheHits = Meter.CreateCounter<long>(
    "deepresearch.llm.cache.hits",
    description: "LLM cache hits");

public static readonly Counter<long> LlmCacheMisses = Meter.CreateCounter<long>(
    "deepresearch.llm.cache.misses",
    description: "LLM cache misses");

public static readonly ObservableGauge<long> CachedResponseCount = 
    Meter.CreateObservableGauge<long>(
        "deepresearch.llm.cache.size",
        () => _cacheService.GetCacheSize(),
        description: "Number of cached LLM responses");
```

#### Step 5: Test and Validate (Week 4, Day 5)

- Test cache hit rates
- Measure response time improvement
- Validate cache eviction works
- Check memory usage

### Expected Impact (B3)

| Metric | Before | After (80% cache hit) | Improvement |
|--------|--------|----------------------|-------------|
| Cached queries | 0% | 80% | N/A |
| Avg query time | 10s | 2s (cached) + 10s (miss) | 80% faster for cached |
| **Total Workflow** | 43s (after B1+B2) | 24s (with warm cache) | **44% faster** |

**Note:** B3 impact is cumulative and increases over time as cache warms up.

---

## Combined Phase B Impact

### Timeline Overview

```
Week 1: B1 Implementation (Tiered LLM)
  ├── Days 1-2: Model tier configuration
  ├── Days 3-4: LLM provider updates
  └── Day 5: Agent updates

Week 2: B1 Testing + B2 Start (Parallel Execution)
  ├── Days 1-3: B1 testing and validation
  ├── Day 4: B2 analysis
  └── Day 5: B2 helper implementation

Week 3: B2 Completion + B3 Start (Caching)
  ├── Days 1-2: B2 workflow updates
  ├── Days 3-4: B2 testing
  └── Day 5: B3 cache infrastructure

Week 4: B3 Completion and Integration
  ├── Days 1-3: B3 implementation
  ├── Day 4: B3 metrics
  └── Day 5: Final testing and documentation
```

### Performance Progression

| Phase | Workflow Time | Improvement | Cumulative |
|-------|--------------|-------------|------------|
| **Baseline** | 120s | - | - |
| **After Phase A** | 120s | 0s (monitoring optimized) | 0% |
| **After B1 (Tiered LLM)** | 55s | -65s | 54% |
| **After B2 (Parallel)** | 43s | -12s | 64% |
| **After B3 (Caching)** | 24s | -19s | **80%** |

**Final Target:** 38s (68% improvement)  
**Stretch Goal:** 24s (80% improvement with warm cache)

### Resource Requirements

#### Development
- 1 developer × 4 weeks
- Can be parallelized with 2 developers (2 weeks)

#### Infrastructure
- Multi-model Ollama setup (7B, 14B, 32B)
- Redis for caching (optional, can use in-memory)
- No additional cloud costs

#### Testing
- Performance benchmarks after each track
- Quality validation for model tiers
- Load testing for parallel execution

---

## Success Metrics

### Primary KPIs

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Total Workflow Time** | 38s | End-to-end execution |
| **LLM Time** | 43s → 24s | Sum of all LLM calls |
| **Tool Time** | 10s → 7s | Sum of all tool calls |
| **Cache Hit Rate** | 80% | Cached / Total queries |

### Quality Metrics

| Metric | Threshold | Validation |
|--------|-----------|------------|
| Output Quality | >95% | Human evaluation |
| Error Rate | <2% | Automated tests |
| Model Tier Accuracy | >90% | Correct tier selection |
| Cache Staleness | <5% | Outdated responses |

### Operational Metrics

| Metric | Target | Monitoring |
|--------|--------|------------|
| CPU Utilization | <80% | Runtime metrics |
| Memory Usage | <4GB | Runtime metrics |
| Model Availability | >99.5% | Health checks |
| Cache Size | <500MB | Cache metrics |

---

## Risk Mitigation

### B1 Risks: Model Quality
- **Risk:** Smaller models produce lower quality output
- **Mitigation:** 
  - Start with conservative tier assignments
  - A/B test model quality
  - Add quality metrics and alerts
  - Fallback to larger model if confidence low

### B2 Risks: Race Conditions
- **Risk:** Parallel execution causes data corruption
- **Mitigation:**
  - Use immutable data structures
  - Careful dependency analysis
  - Extensive parallel testing
  - Add synchronization where needed

### B3 Risks: Cache Staleness
- **Risk:** Cached responses become outdated
- **Mitigation:**
  - Appropriate TTL settings
  - Cache versioning
  - Manual cache invalidation API
  - Monitor cache hit quality

---

## Next Actions

### Immediate (This Week)
1. ✅ Complete this roadmap document
2. ⏳ Set up model tier configuration
3. ⏳ Identify all LLM calls in codebase
4. ⏳ Create task complexity mapping

### Week 1 (B1 Start)
1. Implement `LlmModelTierConfiguration.cs`
2. Update `ILlmProvider` interface
3. Update all agent implementations
4. Add model tier metrics

### Week 2 (B1 Complete, B2 Start)
1. Complete B1 testing
2. Analyze tool dependencies
3. Implement `ParallelToolExecutor`
4. Update `SupervisorWorkflow`

### Weeks 3-4 (B2 Complete, B3)
1. Complete B2 testing
2. Implement cache infrastructure
3. Add semantic matching
4. Final integration and documentation

---

## Documentation Deliverables

1. ✅ **This Roadmap** - High-level strategy
2. ⏳ **LLM Usage Analysis** - All LLM calls mapped to tiers
3. ⏳ **Parallel Execution Map** - Dependency graph
4. ⏳ **Cache Strategy Guide** - TTL, sizing, eviction
5. ⏳ **Performance Benchmark Results** - Before/after metrics
6. ⏳ **Phase B Completion Report** - Final summary

---

**Status:** Phase B Roadmap Complete  
**Ready to Begin:** B1 - Tiered LLM Model Selection  
**Expected Completion:** 4 weeks from start  
**Target Impact:** 68-80% performance improvement
