# Observability Stack Architecture - Corrected

## Current Implementation

### Stack Components

```
┌─────────────────────────────────────────────────────────────────┐
│                    DeepResearch Application                     │
│                                                                 │
│  ┌────────────────┐  ┌────────────────┐  ┌─────────────────┐ │
│  │ DeepResearch   │  │  AsyncMetrics  │  │   Health Check  │ │
│  │     .Api       │  │   Collector    │  │   /health       │ │
│  │  (Port 5000)   │  │  Background    │  │   Endpoint      │ │
│  └────────┬───────┘  └───────┬────────┘  └─────────────────┘ │
│           │                   │                                 │
│           │  /metrics         │                                 │
│           │  endpoint         │  Async Queue                   │
└───────────┼───────────────────┼─────────────────────────────────┘
            │                   │
            │                   │
            │                   └──────────────────┐
            │                                      │
            ▼                                      ▼
┌──────────────────────┐              ┌──────────────────────┐
│                      │              │                      │
│   Prometheus         │◄─────────────┤  System.Diagnostics  │
│   (Port 9090)        │  Scrape      │      .Metrics       │
│                      │  /metrics    │                      │
│  • Metrics Storage   │              │  • Counters          │
│  • 15s Interval      │              │  • Histograms        │
│  • 30d Retention     │              │  • Gauges            │
│                      │              │                      │
└──────────┬───────────┘              └──────────────────────┘
           │
           │
           ▼
┌──────────────────────┐
│                      │
│   AlertManager       │
│   (Port 9093)        │
│                      │
│  • Alert Rules       │
│  • Notifications     │
│                      │
└──────────────────────┘


            ┌──────────────────────────────────────┐
            │    OpenTelemetry SDK (.NET)          │
            │                                      │
            │  ┌──────────────────────────────┐   │
            │  │  ActivitySource              │   │
            │  │  (DiagnosticConfig)          │   │
            │  │                              │   │
            │  │  • MasterWorkflow spans      │   │
            │  │  • Agent execution traces    │   │
            │  │  • Tool invocation spans     │   │
            │  └────────────┬─────────────────┘   │
            │               │ OTLP                 │
            │               │ (gRPC/HTTP)          │
            └───────────────┼──────────────────────┘
                            │
                            │ Port 4317 (gRPC)
                            │ Port 4318 (HTTP)
                            ▼
            ┌──────────────────────────────────────┐
            │                                      │
            │   Jaeger All-in-One                  │
            │   (Port 16686 UI)                    │
            │                                      │
            │  • OTLP Receiver (4317/4318)         │
            │  • Trace Storage (in-memory)         │
            │  • Jaeger UI (16686)                 │
            │  • Query API (16686)                 │
            │  • Supports flame graphs             │
            │                                      │
            └──────────┬───────────────────────────┘
                       │
                       │ Query traces
                       ▼
            ┌──────────────────────────────────────┐
            │                                      │
            │   Grafana                            │
            │   (Port 3001)                        │
            │                                      │
            │  Datasources:                        │
            │  ┌────────────────────────────────┐  │
            │  │ • Prometheus (metrics)         │  │
            │  │ • Jaeger (traces)              │  │
            │  └────────────────────────────────┘  │
            │                                      │
            │  Dashboards:                         │
            │  • MasterWorkflow Performance        │
            │  • System Metrics                    │
            │  • Trace Explorer                    │
            │                                      │
            └──────────────────────────────────────┘
```

## Data Flow

### Tracing (OpenTelemetry → Jaeger)
```
ActivityScope.Start()
    ↓
System.Diagnostics.Activity
    ↓
OpenTelemetry Exporter
    ↓
OTLP Protocol (gRPC:4317 or HTTP:4318)
    ↓
Jaeger Collector
    ↓
Jaeger Storage (in-memory)
    ↓
Jaeger UI (16686) & Grafana (3001)
```

### Metrics (System.Diagnostics.Metrics → Prometheus)
```
DiagnosticConfig.WorkflowStepDuration.Record()
    ↓
System.Diagnostics.Metrics.Histogram<double>
    ↓
/metrics endpoint (Prometheus format)
    ↓
Prometheus scrape (every 15s)
    ↓
Prometheus TSDB
    ↓
Grafana queries (PromQL)
```

### Async Metrics (Optional)
```
RecordStepDuration()
    ↓
AsyncMetricsCollector.RecordHistogram()
    ↓
BlockingCollection<MetricEntry> (queue)
    ↓
Background service processes queue
    ↓
DiagnosticConfig.WorkflowStepDuration.Record()
    ↓
/metrics endpoint
    ↓
Prometheus
```

## Key Differences from Initial Design

### Original Plan (Not Implemented)
- ❌ Grafana Tempo for trace storage
- ❌ Direct Tempo OTLP endpoint

### Actual Implementation (Correct)
- ✅ Jaeger All-in-One for trace storage
- ✅ Jaeger OTLP receiver (4317/4318)
- ✅ Jaeger supports flame graphs
- ✅ Grafana has Jaeger datasource configured

## Port Configuration

| Service | Port | Protocol | Purpose |
|---------|------|----------|---------|
| **DeepResearch.Api** | 5000 | HTTP | API & /metrics endpoint |
| **Jaeger UI** | 16686 | HTTP | Trace visualization |
| **Jaeger OTLP gRPC** | 4317 | gRPC | Trace ingestion (OTLP) |
| **Jaeger OTLP HTTP** | 4318 | HTTP | Trace ingestion (OTLP) |
| **Prometheus** | 9090 | HTTP | Metrics storage & query |
| **Grafana** | 3001 | HTTP | Dashboards & visualization |
| **AlertManager** | 9093 | HTTP | Alert management |
| **OTEL Collector** | 4319 | gRPC | Optional aggregation (alternate port) |
| **OTEL Collector** | 4320 | HTTP | Optional aggregation (alternate port) |

## Configuration Files

### Jaeger Configuration
- **Location:** `Docker/Observability/docker-compose-monitoring.yml`
- **Image:** `jaegertracing/all-in-one:latest`
- **OTLP Support:** Enabled via `COLLECTOR_OTLP_ENABLED=true`

### Grafana Datasources
- **Location:** `Docker/Observability/config/grafana/datasources/datasources.yml`
- **Datasources:**
  - Prometheus (default): `http://prometheus:9090`
  - Jaeger: `http://jaeger:16686`

### Prometheus Configuration
- **Location:** `Docker/Observability/config/prometheus.yml`
- **Scrape Targets:**
  - DeepResearch.Api: `http://host.docker.internal:5000/metrics`
  - Interval: 15s
  - Retention: 30 days

## Starting the Stack

### Full Observability Stack
```bash
cd Docker/Observability
docker-compose -f docker-compose-monitoring.yml up -d
```

### Verify Services
```bash
docker ps | findstr "jaeger\|prometheus\|grafana"
```

### Check Health
```bash
# Jaeger
curl http://localhost:16686/api/health

# Prometheus
curl http://localhost:9090/-/healthy

# Grafana
curl http://localhost:3001/api/health
```

## Accessing Services

### Jaeger UI (Direct Trace Access)
```
http://localhost:16686

Service: DeepResearchAgent
Operation: MasterWorkflow.StreamStateAsync
```

### Grafana (Unified Dashboard)
```
http://localhost:3001
Username: admin
Password: admin

Navigate:
- Explore → Jaeger → Search traces
- Explore → Prometheus → Query metrics
- Dashboards → MasterWorkflow Performance
```

### Prometheus (Direct Metrics Access)
```
http://localhost:9090

Example Queries:
- deepresearch_workflow_step_duration_bucket
- rate(deepresearch_workflow_steps_total[5m])
- deepresearch_workflow_active_workflows
```

## Compatibility with Phase A Implementation

### OpenTelemetry SDK Configuration
The Phase A implementation uses OpenTelemetry SDK with OTLP exporters. This is **fully compatible** with Jaeger because:

1. ✅ Jaeger supports OTLP protocol (gRPC and HTTP)
2. ✅ No code changes needed in .NET application
3. ✅ Same ActivitySource and Activity API
4. ✅ Flame graphs work in Jaeger UI
5. ✅ Grafana can query Jaeger datasource

### Configuration in appsettings.json
The application configuration remains the same:
```json
{
  "Observability": {
    "EnableTracing": true,
    "EnableMetrics": true,
    "TraceSamplingRate": 0.1
  }
}
```

### OTLP Endpoint
The DeepResearch.Api should be configured to export traces to:
```
OTLP gRPC: http://localhost:4317
OTLP HTTP: http://localhost:4318
```

## Benefits of Jaeger vs Tempo

### Advantages of Jaeger for this Project
1. ✅ **All-in-one deployment** - Single container, easy setup
2. ✅ **Mature UI** - Rich trace visualization with flame graphs
3. ✅ **OTLP support** - Compatible with OpenTelemetry
4. ✅ **Service dependency graph** - Shows workflow relationships
5. ✅ **Already deployed** - No infrastructure changes needed

### Tempo Trade-offs (Not Used)
- ❌ Requires separate storage backend (S3, local file)
- ❌ More complex deployment
- ❌ Better for very large-scale deployments
- ❌ Optimized for object storage cost

## Migration Notes

If you want to switch from Jaeger to Tempo in the future:

1. Update `docker-compose-monitoring.yml`:
   - Replace Jaeger with Tempo service
   - Add S3/local storage backend

2. Update Grafana datasource:
   - Change from Jaeger to Tempo datasource
   - Update query syntax (Jaeger vs Tempo query language)

3. Application code:
   - No changes needed! OTLP works with both

## Summary

The current implementation uses:
- ✅ **Jaeger** for distributed tracing (not Tempo)
- ✅ **Prometheus** for metrics collection
- ✅ **Grafana** for unified visualization
- ✅ **OTLP protocol** for trace export
- ✅ **Full compatibility** with Phase A implementation

All Phase A code and configuration is correct and compatible with the actual Jaeger-based infrastructure.
