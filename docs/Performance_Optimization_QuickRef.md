# Performance Optimization Plan - Quick Reference

## Executive Summary

This document provides a quick reference for the comprehensive performance optimization plan for the Deep Research Agent. For full details, see `MasterWorkflow_StreamStateAsync_ExecutionTrace.md`.

---

## Current vs. Target Performance

| Metric | Current (Baseline) | Target (Optimized) | Improvement |
|--------|-------------------|-------------------|-------------|
| **Total Workflow Duration** | 120s | 38s | **68% faster** |
| **Monitoring Overhead** | 19ms | <1ms | **95% reduction** |
| **LLM Latency** | 75s | 21s | **72% faster** |
| **Tool Execution** | 20s | 4s | **80% faster** |
| **Cache Hit Rate** | 0% | 35-50% | **New capability** |

---

## Two-Track Optimization Strategy

### 🎯 Track A: Monitoring Overhead Reduction (<1% of total time)

**Goal:** Reduce telemetry overhead from 19ms to <1ms
**Timeline:** 2 weeks
**Effort:** LOW-MEDIUM

#### A1: Feature Toggle (Week 1)
- ✅ Enable/disable telemetry per environment
- ✅ Sampling (10% in production)
- ✅ Conditional detailed tracing
- **Impact:** 19ms → 2ms (90% reduction)
- **Files:** 
  - `ObservabilityConfiguration.cs` (new)
  - `ActivityScope.cs` (modify)
  - `appsettings.json` (update)

#### A2: Async Metrics Queue (Week 2)
- ✅ Move metrics to background thread
- ✅ Non-blocking enqueue (~0.05ms)
- ✅ Health check for queue
- **Impact:** 3ms → 0.3ms (90% reduction)
- **Files:**
  - `AsyncMetricsCollector.cs` (new)
  - `MasterWorkflow.cs` (modify)

---

### 🚀 Track B: Core Performance Optimization (98-99% of total time)

**Goal:** Reduce LLM and tool execution time
**Timeline:** 4 weeks
**Effort:** MEDIUM-HIGH

#### B1: LLM Model Optimization (Weeks 1-2)
- ✅ Use smaller models for simple tasks
- ✅ Tiered strategy: 7b, 14b, 32b
- ✅ Task complexity classification
- **Impact:** 75s → 49s (35% reduction)
- **Files:**
  - `ModelSelector.cs` (new)
  - All agents (modify)

| Task | Current Model | New Model | Latency |
|------|--------------|-----------|---------|
| Clarify | qwen2.5:14b | qwen2.5:7b | 3s → 1.5s |
| QualityEval | qwen2.5:14b | qwen2.5:7b | 3s → 1.5s |
| ContextPruner | qwen2.5:14b | qwen2.5:7b | 3s → 1.5s |
| SupervisorBrain | qwen2.5:14b | qwen2.5:32b | 5s → 5s (quality↑) |
| FinalReport | qwen2.5:14b | qwen2.5:32b | 8s → 8s (quality↑) |

#### B2: Parallel Tool Execution (Week 3)
- ✅ Process search results concurrently
- ✅ Configurable concurrency (max 3-5)
- ✅ Individual failure isolation
- **Impact:** 20s → 4s (80% reduction)
- **Files:**
  - `ParallelToolExecutor.cs` (new)
  - `SupervisorWorkflow.cs` (modify)

**Example:**
```
Before: Summarize(1) → Extract(1) → Summarize(2) → Extract(2) → ... (30s)
After:  [Summarize(1→3) + Extract(1→3)] in parallel (6s)
```

#### B3: LLM Response Caching (Week 4)
- ✅ Cache identical prompts
- ✅ SHA256-based cache keys
- ✅ 1-hour TTL
- **Impact:** 35-50% cache hit rate
- **Files:**
  - `LlmResponseCache.cs` (new)
  - `ILlmProvider` (modify)

**Cache Hits:**
- Repeated clarifications
- Quality evaluations
- Common summarizations

---

## Implementation Roadmap

### Week 0: Baseline (2-3 days)
- [ ] Run performance benchmarks
- [ ] Document current latencies
- [ ] Establish SLOs

### Week 1: Track A1 + Track B1 Start
- [ ] **A1:** Implement feature toggle (4 hours)
- [ ] **B1:** Benchmark LLM models (3 days)
- [ ] Set up Grafana dashboards

### Week 2: Track A2 + Track B1 Complete
- [ ] **A2:** Implement async metrics (6-8 hours)
- [ ] **B1:** Integrate ModelSelector (3 days)
- [ ] A/B test model tiers

### Week 3: Track B2
- [ ] **B2:** Implement ParallelToolExecutor (3-5 days)
- [ ] Test concurrency limits
- [ ] Measure API rate limits

### Week 4: Track B3
- [ ] **B3:** Implement LLM cache (2-4 days)
- [ ] Test cache effectiveness
- [ ] Optimize TTL settings

---

## Configuration Quick Reference

### Development (Full Observability)
```json
{
  "Observability": {
    "EnableTracing": true,
    "EnableDetailedTracing": true,
    "TraceSamplingRate": 1.0,
    "UseAsyncMetrics": false
  },
  "LLM": {
    "SimpleModel": "qwen2.5:7b",
    "MediumModel": "qwen2.5:14b",
    "ComplexModel": "qwen2.5:32b",
    "CacheEnabled": true
  },
  "ToolExecution": {
    "MaxConcurrency": 3,
    "EnableParallelExecution": true
  }
}
```

### Production (High Performance)
```json
{
  "Observability": {
    "EnableTracing": true,
    "EnableDetailedTracing": false,        // ⚡ Per-step activities OFF
    "TraceSamplingRate": 0.1,              // ⚡ 10% sampling
    "UseAsyncMetrics": true                // ⚡ Background queue
  },
  "LLM": {
    "SimpleModel": "qwen2.5:3b",           // ⚡ Even faster for simple tasks
    "MediumModel": "qwen2.5:14b",
    "ComplexModel": "qwen2.5:32b",
    "CacheEnabled": true,
    "CacheTtlMinutes": 60
  },
  "ToolExecution": {
    "MaxConcurrency": 5,                   // ⚡ Higher concurrency
    "EnableParallelExecution": true
  }
}
```

---

## Success Metrics

### Primary KPIs
| Metric | Baseline | Target | Status |
|--------|----------|--------|--------|
| **p50 Duration** | 120s | <45s | 🔴 Not started |
| **p95 Duration** | 180s | <70s | 🔴 Not started |
| **Throughput** | 0.5/min | 1.5/min | 🔴 Not started |
| **Cache Hit Rate** | 0% | >35% | 🔴 Not started |
| **Error Rate** | 2% | <5% | 🟢 Within target |

### Quality Metrics
| Metric | Baseline | Target | Status |
|--------|----------|--------|--------|
| **Quality Score** | 7.5/10 | ≥7.3/10 | 🔴 Not measured |
| **User Satisfaction** | TBD | TBD | 🔴 Not measured |

---

## Testing Checklist

### Unit Tests
- [ ] `ObservabilityConfigurationTests`
- [ ] `AsyncMetricsCollectorTests`
- [ ] `ModelSelectorTests`
- [ ] `ParallelToolExecutorTests`
- [ ] `LlmResponseCacheTests`

### Integration Tests
- [ ] End-to-end with all optimizations
- [ ] Telemetry disabled workflow
- [ ] Cache cold vs. warm
- [ ] High concurrency test
- [ ] Failure scenarios

### Performance Tests
- [ ] Baseline vs. Optimized benchmark
- [ ] Load test: 100 concurrent workflows
- [ ] Stress test: Metrics queue
- [ ] Soak test: 24-hour run

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| **Quality degradation** | A/B testing; Revert if >5% drop |
| **Cache poisoning** | Short TTL (1 hour); Invalidation strategy |
| **Queue overflow** | Health checks; Increase size; Drop oldest |
| **API rate limits** | Configurable concurrency; Backoff |
| **Memory increase** | Limits; LRU eviction; Alerts |

---

## Rollback Plan

**Immediate (No deploy required):**
```json
{
  "Observability": { "UseAsyncMetrics": false },
  "LLM": { "CacheEnabled": false },
  "ToolExecution": { "EnableParallelExecution": false }
}
```

**Quick (<5 minutes):**
- Revert to previous deployment

---

## Files to Create/Modify

### New Files (7 total):
1. ✅ `ObservabilityConfiguration.cs`
2. ✅ `AsyncMetricsCollector.cs`
3. ✅ `ModelSelector.cs`
4. ✅ `ParallelToolExecutor.cs`
5. ✅ `LlmResponseCache.cs`
6. ✅ `MetricsQueueHealthCheck.cs`
7. ✅ `LlmCacheHealthCheck.cs`

### Modified Files (5 major):
1. ✅ `ActivityScope.cs`
2. ✅ `MasterWorkflow.cs`
3. ✅ `SupervisorWorkflow.cs`
4. ✅ `Program.cs`
5. ✅ `appsettings.*.json`

---

## Grafana Dashboard Panels

### Dashboard 1: Performance Overview
- Total workflow duration (line chart, p50/p95/p99)
- Step latency breakdown (stacked bar chart)
- Throughput (gauge, workflows/min)

### Dashboard 2: Optimization Impact
- Before/After comparison (bar chart)
- LLM latency by model (heatmap)
- Cache hit rate (gauge, %)
- Parallel execution efficiency (time series)

### Dashboard 3: System Health
- Monitoring overhead (line chart, ms)
- Metrics queue depth (gauge)
- Dropped metrics (counter)
- Error rate (gauge, %)

---

## Next Actions (This Week)

1. ✅ **Review plan** with team
2. ✅ **Assign track owners:**
   - Track A: [Name]
   - Track B: [Name]
3. ✅ **Set up benchmark environment**
4. ✅ **Run baseline measurements**
5. ✅ **Create tracking board** (Jira/GitHub)

---

## Contact & Support

- **Plan Owner:** [Your Name]
- **Track A Lead:** [Name]
- **Track B Lead:** [Name]
- **Documentation:** `docs/MasterWorkflow_StreamStateAsync_ExecutionTrace.md`
- **Code Samples:** All in main document (production-ready)

---

## Expected Outcome Summary

```
Current:  ████████████████████████████████████████  120s
Track A:  ███████████████████████████████████████▌  119.5s (-0.4%)
Track B1: ████████████████████████████               78s (-35%)
Track B2: ██████████████████▌                        54s (-55%)
Track B3: ████████████▌                              38s (-68%)
          └────────────────────────────────────────┘
          0s                                      120s

Final Result: 68% faster (120s → 38s)
```

**🎉 From 2 minutes to under 40 seconds!**

---

**Document Version:** 1.0
**Last Updated:** {{DATE}}
**See Also:** `MasterWorkflow_StreamStateAsync_ExecutionTrace.md` (full details)
