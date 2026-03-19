# DeepResearch Observability Stack

Comprehensive observability setup for DeepResearch using OpenTelemetry, Prometheus, Grafana, Jaeger, and AlertManager.

## 🎯 Overview

This observability stack provides:
- **Distributed Tracing** (Jaeger) - Track execution flow across the entire workflow
- **Metrics Collection** (Prometheus) - Monitor performance and business metrics
- **Visualization** (Grafana) - Real-time dashboards and insights
- **Alerting** (AlertManager) - Proactive notifications for issues
- **OpenTelemetry** - Modern, vendor-neutral instrumentation

## 📊 What You Can Monitor

### Workflow Execution
- **Current Method Running**: Real-time visibility into which method is executing
- **Execution History**: Track the code path and sequence of operations
- **Performance Metrics**: Duration, throughput, and latency for each step

### MasterWorkflow.StreamStateAsync Metrics
- **Step 1 (Clarify)**: Duration, success rate, clarification frequency
- **Step 2 (Research Brief)**: Generation time, brief length
- **Step 3 (Initial Draft)**: Draft generation performance
- **Step 4 (Supervisor Loop)**: Number of iterations, refinement metrics
- **Step 5 (Final Report)**: Report generation time, final report length

### Additional Metrics
- **LLM Performance**: Request duration, tokens used, provider latency
- **Tool Invocations**: Success/failure rates, duration per tool
- **Search Operations**: Query performance, result quality
- **State Management**: Cache hit rates, state operation latency
- **System Resources**: CPU, memory, thread usage

## 🚀 Quick Start

### 1. Start the Observability Stack

```powershell
# From the workspace root
cd C:\RepoEx\PhoenixAI\DeepResearch

# Start all observability services
docker-compose -f docker-compose.observability.yml up -d
```

### 2. Access the Dashboards

Once started, access:

- **Grafana**: http://localhost:3000
  - Username: `admin`
  - Password: `admin`
  - Pre-configured with DeepResearch dashboards

- **Jaeger UI**: http://localhost:16686
  - Distributed tracing visualization
  - Search traces by workflow, duration, or tags

- **Prometheus**: http://localhost:9090
  - Query metrics directly
  - View targets and alerting rules

- **AlertManager**: http://localhost:9093
  - View active alerts
  - Configure notification channels

### 3. Run Your Application with Observability

The instrumentation is already integrated! Just run your application:

```powershell
# DeepResearchAgent will automatically export metrics and traces
dotnet run --project DeepResearchAgent
```

### 4. View Real-Time Metrics

1. Open Grafana (http://localhost:3000)
2. Navigate to **Dashboards** → **DeepResearch** → **MasterWorkflow Observability**
3. Run a workflow and watch the metrics update in real-time!

## 📈 Key Dashboards

### MasterWorkflow Observability Dashboard

**Panels:**
1. **Workflow Execution Rate**: Steps executed per second
2. **Workflow Step Duration (p95/p50)**: Performance percentiles per step
3. **Total Workflow Duration**: End-to-end execution time
4. **Workflow Error Rate**: Failures and error types
5. **LLM Request Duration**: AI provider performance
6. **LLM Tokens Used**: Token consumption rate
7. **Tool Invocation Rate**: Tool usage patterns
8. **State Cache Hit Rate**: Caching efficiency
9. **Current Method Execution**: Live execution table

## 🔍 Distributed Tracing with Jaeger

### View Execution Flow

1. Open Jaeger UI: http://localhost:16686
2. Select Service: `DeepResearchAgent`
3. Search for traces
4. Click on a trace to see:
   - Complete workflow execution timeline
   - Each method call with duration
   - Parent-child relationships
   - Tags: workflow name, step number, query details
   - Events: step completions, errors, cache hits

### Example Trace Structure
```
MasterWorkflow.StreamStateAsync (Root Span)
├── Step1.Clarify
│   └── ClarifyWithUserAsync
├── Step2.ResearchBrief
│   └── WriteResearchBriefAsync
├── Step3.InitialDraft
│   └── WriteDraftReportAsync
├── Step4.SupervisorLoop
│   ├── Iteration 1
│   ├── Iteration 2
│   └── Iteration N
└── Step5.FinalReport
    └── GenerateFinalReportAsync
```

## 🔔 Alerting

### Pre-configured Alerts

| Alert | Threshold | Severity |
|-------|-----------|----------|
| HighWorkflowErrorRate | >0.1 errors/sec | Critical |
| SlowWorkflowExecution | P95 > 5 minutes | Warning |
| VerySlowWorkflowExecution | P95 > 10 minutes | Critical |
| LLMServiceDegradation | P95 > 30 seconds | Warning |
| LowCacheHitRate | < 50% | Warning |
| HighToolInvocationFailureRate | >0.05 failures/sec | Warning |

### Configure Notifications

Edit `observability/alertmanager/alertmanager.yml` to add:
- Email notifications
- Slack webhooks
- PagerDuty integration
- Custom webhooks

## 📊 Example Queries

### Prometheus Queries

```promql
# Average workflow duration
histogram_quantile(0.95, rate(deepresearch_workflow_total_duration_bucket[5m]))

# Step execution rate
rate(deepresearch_workflow_steps_total[5m])

# LLM token usage rate
rate(deepresearch_llm_tokens_total[1h])

# Cache hit rate
avg(deepresearch_state_cache_hit_rate)

# Error rate by workflow
rate(deepresearch_workflow_errors_total[5m])
```

## 🛠 Advanced Configuration

### Custom Metrics in Your Code

```csharp
using DeepResearchAgent.Observability;
using System.Diagnostics;

public async Task MyMethodAsync()
{
    // Add distributed tracing
    using var activity = ActivityScope.Start("MyMethod");

    // Track execution metrics
    using var metrics = MetricsCollector.TrackExecution(
        "MyMethodAsync", 
        workflow: "CustomWorkflow",
        step: "Step1",
        metadata: new Dictionary<string, object>
        {
            ["user_id"] = userId,
            ["operation"] = "process"
        });

    activity.AddTag("user.id", userId);

    try
    {
        // Your code here
        await DoWorkAsync();

        activity.SetStatus(ActivityStatusCode.Ok);
    }
    catch (Exception ex)
    {
        activity.RecordException(ex);
        throw;
    }
}
```

### Add Custom Counter

```csharp
var myCounter = DiagnosticConfig.Meter.CreateCounter<long>(
    "my_custom_counter",
    description: "Counts custom events");

myCounter.Add(1, 
    new KeyValuePair<string, object?>("operation", "custom"),
    new KeyValuePair<string, object?>("status", "success"));
```

## 📝 Execution History API

Get recent execution history programmatically:

```csharp
// Get last 100 executions
var history = MetricsCollector.GetExecutionHistory(maxCount: 100);

// Get history for specific workflow
var workflowHistory = MetricsCollector.GetExecutionHistory(
    workflow: "MasterWorkflow",
    maxCount: 50);

// Get method metrics
var metrics = MetricsCollector.GetMetrics(
    "StreamStateAsync", 
    workflow: "MasterWorkflow");

Console.WriteLine($"Avg Duration: {metrics.AvgDurationMs}ms");
Console.WriteLine($"Success Rate: {metrics.SuccessRate}%");
```

## 🧹 Maintenance

### Stop Services
```powershell
docker-compose -f docker-compose.observability.yml down
```

### Stop and Remove Data
```powershell
docker-compose -f docker-compose.observability.yml down -v
```

### View Logs
```powershell
# All services
docker-compose -f docker-compose.observability.yml logs -f

# Specific service
docker-compose -f docker-compose.observability.yml logs -f prometheus
docker-compose -f docker-compose.observability.yml logs -f jaeger
docker-compose -f docker-compose.observability.yml logs -f grafana
```

### Restart Services
```powershell
docker-compose -f docker-compose.observability.yml restart
```

## 🔧 Troubleshooting

### Metrics Not Showing in Prometheus

1. Check Prometheus targets: http://localhost:9090/targets
2. Ensure your app exposes `/metrics` endpoint
3. Verify `host.docker.internal` resolves (use `localhost` on Linux)

### Traces Not Appearing in Jaeger

1. Check OTLP endpoint: http://localhost:4317
2. Verify `OpenTelemetry:Exporters:Otlp:Endpoint` in appsettings.json
3. Check Jaeger collector logs:
   ```powershell
   docker logs deepresearch-jaeger
   ```

### Grafana Dashboard Not Loading

1. Check datasource connectivity in Grafana
2. Verify Prometheus is running: http://localhost:9090
3. Check Grafana logs:
   ```powershell
   docker logs deepresearch-grafana
   ```

## 📦 What's Included

### Files Created

```
📁 DeepResearchAgent/
├── 📁 Observability/
│   ├── DiagnosticConfig.cs          # Metrics and activity sources
│   ├── TelemetryExtensions.cs       # DI registration
│   ├── ActivityScope.cs             # Distributed tracing helper
│   └── MetricsCollector.cs          # Execution tracking

📁 observability/
├── 📁 prometheus/
│   ├── prometheus.yml               # Scraping configuration
│   └── 📁 rules/
│       └── deepresearch-alerts.yml  # Alert rules
├── 📁 grafana/
│   ├── 📁 dashboards/
│   │   ├── masterworkflow-dashboard.json
│   │   └── dashboard-provider.yml
│   └── 📁 datasources/
│       └── datasources.yml
├── 📁 alertmanager/
│   └── alertmanager.yml
└── 📁 otel-collector/
    └── otel-collector-config.yml

📄 docker-compose.observability.yml  # Full stack deployment
```

## 🎓 Best Practices

1. **Use Tags Wisely**: Add meaningful tags to activities for better filtering
2. **Monitor P95/P99**: Focus on high percentiles, not just averages
3. **Set Realistic Alerts**: Tune thresholds based on actual performance
4. **Regular Reviews**: Check dashboards weekly to spot trends
5. **Trace Sampling**: In production, consider sampling to reduce overhead

## 📚 Resources

- [OpenTelemetry Docs](https://opentelemetry.io/docs/)
- [Prometheus Query Guide](https://prometheus.io/docs/prometheus/latest/querying/basics/)
- [Grafana Dashboards](https://grafana.com/docs/grafana/latest/dashboards/)
- [Jaeger Tracing](https://www.jaegertracing.io/docs/)

## 🤝 Contributing

To add new metrics or dashboards:
1. Define metrics in `DiagnosticConfig.cs`
2. Record metrics in your code
3. Update Grafana dashboard JSON
4. Add alert rules to `deepresearch-alerts.yml`

---

**Happy Monitoring! 📊🔍**
