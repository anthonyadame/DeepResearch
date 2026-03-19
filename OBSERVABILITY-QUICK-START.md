# 🚀 DeepResearch Observability - Quick Reference

## One-Command Setup

```powershell
# Start entire observability stack
docker-compose -f docker-compose.observability.yml up -d
```

## 🌐 Access URLs

| Service | URL | Credentials |
|---------|-----|-------------|
| **Grafana** | http://localhost:3000 | admin / admin |
| **Jaeger** | http://localhost:16686 | - |
| **Prometheus** | http://localhost:9090 | - |
| **AlertManager** | http://localhost:9093 | - |

## 📊 Key Metrics

### Workflow Performance
```promql
# P95 workflow duration
histogram_quantile(0.95, rate(deepresearch_workflow_total_duration_bucket[5m]))

# Step execution rate
rate(deepresearch_workflow_steps_total[5m])

# Error rate
rate(deepresearch_workflow_errors_total[5m])
```

### LLM Monitoring
```promql
# LLM request duration
histogram_quantile(0.95, rate(deepresearch_llm_request_duration_bucket[5m]))

# Token usage
rate(deepresearch_llm_tokens_total[1h])
```

### Cache Performance
```promql
# Cache hit rate
avg(deepresearch_state_cache_hit_rate)
```

## 🔍 Jaeger Trace Search

**Find slow traces:**
- Service: `DeepResearchAgent`
- Min Duration: `5s`
- Tags: `workflow=MasterWorkflow`

**Find errors:**
- Tags: `error=true`

## 🔔 Active Alerts

| Alert | Meaning |
|-------|---------|
| HighWorkflowErrorRate | >0.1 errors/sec |
| SlowWorkflowExecution | P95 > 5 min |
| LLMServiceDegradation | P95 > 30 sec |
| LowCacheHitRate | < 50% |

## 💻 Code Instrumentation

### Add Tracing
```csharp
using var activity = ActivityScope.Start();
```

### Track Metrics
```csharp
using var metrics = MetricsCollector.TrackExecution(
    "MethodName", 
    workflow: "WorkflowName",
    step: "StepNumber");
```

### Get Execution History
```csharp
var history = MetricsCollector.GetExecutionHistory(maxCount: 100);
var metrics = MetricsCollector.GetMetrics("MethodName");
```

## 🧰 Common Commands

```powershell
# View logs
docker-compose -f docker-compose.observability.yml logs -f

# Restart services
docker-compose -f docker-compose.observability.yml restart

# Stop all
docker-compose -f docker-compose.observability.yml down

# Stop and remove data
docker-compose -f docker-compose.observability.yml down -v
```

## 🎯 Workflow Visibility

### What Method is Running?
→ Jaeger UI: Click on active trace → See current span

### Execution History
→ Use `MetricsCollector.GetExecutionHistory()`

### Performance Metrics
→ Grafana: "MasterWorkflow Observability" dashboard

---

**Need help?** See [OBSERVABILITY.md](./OBSERVABILITY.md) for full documentation
