# 🚀 Metrics Quick Reference Guide

## ✅ All Metrics Now Active

### 📊 **12 Working Metrics (63% Coverage)**

#### Workflow Metrics (5)
```promql
deepresearch_workflow_steps_total
deepresearch_workflow_step_duration_milliseconds_bucket
deepresearch_workflow_total_duration_milliseconds_bucket
deepresearch_workflow_errors_total
deepresearch_workflow_active
```

#### LLM Metrics (2 + 1 error)
```promql
deepresearch_llm_requests_total              # ✨ NEW
deepresearch_llm_request_duration_milliseconds_bucket  # ✨ NEW
deepresearch_llm_errors_total
```

#### Tool Metrics (2 + 1 error)
```promql
deepresearch_tools_invocations_total         # ✨ NEW
deepresearch_tools_invocation_duration_milliseconds_bucket  # ✨ NEW
deepresearch_tools_errors_total
```

#### State Metrics (3)
```promql
deepresearch_state_operations_total          # ✨ NEW
deepresearch_state_cache_hits_total
deepresearch_state_cache_misses_total
```

---

## 🎯 **Quick Start**

### 1. Start Application
```powershell
cd C:\RepoEx\PhoenixAI\DeepResearch\DeepResearchAgent
dotnet run  # Must run as Administrator
```

### 2. Check Metrics
```powershell
curl http://localhost:5000/metrics/
```

### 3. View Dashboard
Navigate to: http://localhost:3001/d/deepresearch-masterworkflow

---

## 📈 **Common PromQL Queries**

### LLM Performance
```promql
# LLM request rate
rate(deepresearch_llm_requests_total[5m])

# LLM p95 latency
histogram_quantile(0.95, rate(deepresearch_llm_request_duration_milliseconds_bucket[5m]))

# LLM error rate
rate(deepresearch_llm_errors_total[5m])
```

### Tool Performance
```promql
# Tool invocation rate by tool name
rate(deepresearch_tools_invocations_total[5m])

# Tool p95 duration
histogram_quantile(0.95, rate(deepresearch_tools_invocation_duration_milliseconds_bucket[5m]))

# Tool error rate
rate(deepresearch_tools_errors_total[5m])
```

### Cache Performance
```promql
# Cache hit rate percentage
sum(rate(deepresearch_state_cache_hits_total[5m])) / 
(sum(rate(deepresearch_state_cache_hits_total[5m])) + 
 sum(rate(deepresearch_state_cache_misses_total[5m]))) * 100

# State operations rate
rate(deepresearch_state_operations_total[5m])
```

### Workflow Monitoring
```promql
# Active workflows
deepresearch_workflow_active

# Workflow step duration p99
histogram_quantile(0.99, rate(deepresearch_workflow_step_duration_milliseconds_bucket[5m]))

# Overall error rate
sum(rate(deepresearch_workflow_errors_total[5m]) or 
    rate(deepresearch_llm_errors_total[5m]) or 
    rate(deepresearch_tools_errors_total[5m]))
```

---

## 🎨 **Dashboard Panels**

### ✅ Working (10 panels)
1. **Workflow Execution Rate** - Step execution trends
2. **Workflow Step Duration** - p95/p50 latency
3. **Total Workflow Duration** - End-to-end timing
4. **Workflow Error Rate** - Error trends
5. **LLM Request Duration** - p95/p50 LLM latency ✨ NEW
6. **Tool Invocation Rate** - Tool usage by type ✨ NEW
7. **State Cache Hit Rate** - Cache performance
8. **Current Method Execution** - Real-time table
9. **Active Workflows** - Concurrent execution count
10. **Error Rates by Component** - Error breakdown

### ❌ Not Working (1 panel)
6. **LLM Tokens Used** - Blocked by Ollama API limitation

---

## 🔍 **Troubleshooting**

### Metrics Not Appearing
```powershell
# 1. Check if app is running
curl http://localhost:5000/metrics/

# 2. Check Prometheus targets
curl http://localhost:9090/api/v1/targets

# 3. Restart Grafana
docker restart deepresearch-grafana
```

### Dashboard Not Updating
1. Verify auto-refresh is enabled (5s interval)
2. Check time range (last 5-15 minutes)
3. Verify Prometheus data source configured
4. Check browser console for errors

### No Data in Panels
1. Run a workflow to generate metrics
2. Wait 5-10 seconds for Prometheus scrape
3. Refresh Grafana dashboard
4. Check that queries use correct metric names

---

## 📝 **Code Locations**

### Metric Definitions
```
DeepResearchAgent/Observability/DiagnosticConfig.cs
```

### Instrumented Files
```
DeepResearchAgent/Workflows/MasterWorkflow.cs         # Active workflows
DeepResearchAgent/Services/LLM/OllamaLlmProvider.cs   # LLM metrics
DeepResearchAgent/Services/ToolInvocationService.cs   # Tool metrics
DeepResearchAgent/Services/StateManagement/LightningStateService.cs  # State metrics
```

### Dashboard Configuration
```
Docker/Observability/config/grafana/dashboards/masterworkflow-dashboard.json
```

---

## 🎓 **Documentation**

Full implementation details in:
- `DIAGNOSTIC-CONFIG-IMPROVEMENTS.md`
- `METRICS-INSTRUMENTATION-SUMMARY.md`
- `FINAL-IMPLEMENTATION-COMPLETE.md`

---

**Last Updated:** January 2025  
**Status:** ✅ Production Ready  
**Coverage:** 12/19 metrics (63%), 10/11 panels (91%)
