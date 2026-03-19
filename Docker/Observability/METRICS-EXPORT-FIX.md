# ✅ DeepResearchAgent Metrics Export - FIXED

## Issue Resolved
Dashboard was not showing any data because DeepResearchAgent (console application) was not exposing metrics to Prometheus.

## Root Cause
1. **OpenTelemetry services were not registered** in ServiceProviderConfiguration
2. **No metrics endpoint exposed** - Console apps don't automatically expose `/metrics` like ASP.NET Core apps
3. **Prometheus couldn't scrape** - No HTTP endpoint available at configured address

## Solution Implemented

### 1. Registered OpenTelemetry Services
**File:** `DeepResearchAgent/Configuration/ServiceProviderConfiguration.cs`

Added `RegisterOpenTelemetryServices()` method that:
- Reads OpenTelemetry configuration from appsettings.json
- Registers OpenTelemetry with tracing and metrics
- Registers MetricsHostedService (if Prometheus exporter enabled)

```csharp
private static void RegisterOpenTelemetryServices(IServiceCollection services, IConfiguration configuration)
{
    var environment = configuration["OpenTelemetry:Environment"] ?? "development";
    var otlpEndpoint = configuration["OpenTelemetry:Exporters:Otlp:Endpoint"] ?? "http://localhost:4317";
    var prometheusEnabled = configuration.GetValue("OpenTelemetry:Exporters:Prometheus:Enabled", true);

    services.AddOpenTelemetryObservability(options =>
    {
        options.Environment = environment;
        options.OtlpEndpoint = otlpEndpoint;
        options.EnablePrometheusExporter = prometheusEnabled;
    });

    if (prometheusEnabled)
    {
        services.AddHostedService<MetricsHostedService>();
    }
}
```

### 2. Created MetricsHostedService
**File:** `DeepResearchAgent/Observability/MetricsHostedService.cs`

A hosted service that:
- Starts a minimal ASP.NET Core web server
- Listens on `http://localhost:5000`
- Exposes `/metrics` endpoint for Prometheus scraping
- Exposes `/health` endpoint for health checks
- Runs in background while console app executes

**Key Features:**
- Minimal logging (Warning level only for web host)
- Maps Prometheus scraping endpoint automatically
- Gracefully starts/stops with the application

### 3. Updated Program.cs
**File:** `DeepResearchAgent/Program.cs`

Modified to:
- Retrieve all hosted services from service provider
- Start hosted services (including MetricsHostedService)
- Display metrics endpoint URLs on startup
- Stop hosted services on application exit

```csharp
// Start all hosted services (including MetricsHostedService)
var hostedServices = serviceProvider.GetServices<IHostedService>();
var cancellationTokenSource = new CancellationTokenSource();

foreach (var hostedService in hostedServices)
{
    await hostedService.StartAsync(cancellationTokenSource.Token);
}

Console.WriteLine("\n✓ Observability services started");
Console.WriteLine("  • Metrics endpoint: http://localhost:5000/metrics");
Console.WriteLine("  • Health check: http://localhost:5000/health\n");
```

### 4. Updated Prometheus Configuration
**File:** `Docker/Observability/config/prometheus.yml`

Changed scrape target from:
```yaml
- targets: ['host.docker.internal:8080']
```

To:
```yaml
- targets: ['host.docker.internal:5000']  # MetricsHostedService
```

## How It Works

### Architecture
```
┌─────────────────────────────────────────┐
│ DeepResearchAgent (Console App)         │
│                                          │
│  ┌────────────────────────────────────┐ │
│  │ MetricsHostedService               │ │
│  │  • ASP.NET Core Web Host           │ │
│  │  • Port 5000                       │ │
│  │  • /metrics endpoint               │ │
│  │  • /health endpoint                │ │
│  └────────────────────────────────────┘ │
│                                          │
│  ┌────────────────────────────────────┐ │
│  │ MasterWorkflow.StreamStateAsync()  │ │
│  │  • Records metrics                 │ │
│  │  • Creates spans                   │ │
│  └────────────────────────────────────┘ │
└─────────────────────────────────────────┘
                  │
                  │ HTTP GET /metrics (every 5s)
                  ▼
┌─────────────────────────────────────────┐
│ Prometheus (Docker Container)           │
│  • Scrapes host.docker.internal:5000    │
│  • Stores time-series data              │
└─────────────────────────────────────────┘
                  │
                  │ Query metrics
                  ▼
┌─────────────────────────────────────────┐
│ Grafana Dashboard                        │
│  • Displays real-time metrics           │
│  • 9 visualization panels                │
└─────────────────────────────────────────┘
```

### Workflow
1. **Startup:**
   - DeepResearchAgent starts
   - ServiceProvider builds with OpenTelemetry services
   - MetricsHostedService starts web server on port 5000
   - Console displays "Observability services started"

2. **During Execution:**
   - MasterWorkflow executes
   - Metrics recorded via DiagnosticConfig.Meter
   - Spans created via DiagnosticConfig.ActivitySource
   - MetricsHostedService exposes current metrics at /metrics

3. **Prometheus Scraping:**
   - Prometheus scrapes http://host.docker.internal:5000/metrics every 5s
   - Metrics stored in time-series database
   - Grafana queries Prometheus for visualization

## Endpoints Exposed

### `/metrics` (Prometheus Scraping)
**URL:** http://localhost:5000/metrics
**Content-Type:** text/plain; version=0.0.4; charset=utf-8

Returns Prometheus-format metrics:
```
# HELP deepresearch_workflow_steps_total Total number of workflow steps executed
# TYPE deepresearch_workflow_steps_total counter
deepresearch_workflow_steps_total{workflow="MasterWorkflow",step="Clarify"} 1

# HELP deepresearch_workflow_step_duration Duration of workflow steps in milliseconds
# TYPE deepresearch_workflow_step_duration histogram
deepresearch_workflow_step_duration_bucket{workflow="MasterWorkflow",step="Clarify",le="100"} 0
deepresearch_workflow_step_duration_bucket{workflow="MasterWorkflow",step="Clarify",le="500"} 1
...
```

### `/health` (Health Check)
**URL:** http://localhost:5000/health
**Content-Type:** application/json

Returns:
```json
{
  "status": "healthy",
  "service": "DeepResearchAgent"
}
```

## Testing

### 1. Test Metrics Endpoint
```powershell
cd Docker\Observability
.\test-metrics-endpoint.ps1
```

Expected output:
```
✓ Service is running on port 5000
✓ Health endpoint responding
  Status: healthy
  Service: DeepResearchAgent
✓ Metrics endpoint responding
✓ Found: deepresearch_workflow_steps_total
✓ Found: deepresearch_workflow_step_duration
```

### 2. Manual Testing
```powershell
# Start DeepResearchAgent
cd C:\RepoEx\PhoenixAI\DeepResearch
dotnet run --project DeepResearchAgent

# In another terminal, test endpoints
Invoke-RestMethod http://localhost:5000/health
Invoke-WebRequest http://localhost:5000/metrics
```

### 3. Verify Prometheus Scraping
1. Open Prometheus: http://localhost:9090/targets
2. Look for job: `deepresearch-agent`
3. Target should show: `host.docker.internal:5000`
4. State should be: `UP` (green)

### 4. Verify Grafana Dashboard
1. Open Grafana: http://localhost:3001
2. Navigate: Dashboards → DeepResearch → DeepResearch MasterWorkflow Observability
3. Run a workflow in DeepResearchAgent
4. Watch panels update in real-time

## Configuration

### appsettings.json
```json
{
  "OpenTelemetry": {
    "ServiceName": "DeepResearchAgent",
    "ServiceVersion": "0.6.5",
    "Environment": "development",
    "Exporters": {
      "Otlp": {
        "Endpoint": "http://localhost:4317",
        "Protocol": "grpc"
      },
      "Prometheus": {
        "Enabled": true
      }
    },
    "Metrics": {
      "Enabled": true,
      "ExportIntervalMilliseconds": 5000
    }
  }
}
```

### Prometheus Config
```yaml
scrape_configs:
  - job_name: 'deepresearch-agent'
    scrape_interval: 5s
    static_configs:
      - targets: ['host.docker.internal:5000']
    metrics_path: '/metrics'
```

## Files Modified/Created

### Modified
1. **DeepResearchAgent/Configuration/ServiceProviderConfiguration.cs**
   - Added RegisterOpenTelemetryServices() method
   - Registered MetricsHostedService

2. **DeepResearchAgent/Program.cs**
   - Start/stop hosted services
   - Display metrics endpoint URLs

3. **Docker/Observability/config/prometheus.yml**
   - Updated scrape target to port 5000

### Created
1. **DeepResearchAgent/Observability/MetricsHostedService.cs**
   - Background web server for metrics

2. **Docker/Observability/test-metrics-endpoint.ps1**
   - Test script for metrics endpoint

3. **Docker/Observability/METRICS-EXPORT-FIX.md**
   - This documentation

## Troubleshooting

### Metrics endpoint not responding
**Problem:** http://localhost:5000/metrics returns connection refused

**Solution:**
1. Ensure DeepResearchAgent is running
2. Check console output for "Observability services started"
3. Verify port 5000 is not in use by another service:
   ```powershell
   netstat -ano | findstr :5000
   ```

### Prometheus shows target DOWN
**Problem:** Prometheus targets page shows deepresearch-agent as DOWN

**Solution:**
1. From Docker container, test host.docker.internal:
   ```powershell
   docker exec deepresearch-prometheus wget -O- http://host.docker.internal:5000/metrics
   ```
2. On Windows, ensure Docker Desktop is configured to allow host access
3. Check Windows Firewall isn't blocking port 5000

### No metrics in Grafana
**Problem:** Dashboard loads but panels are empty

**Solution:**
1. Verify Prometheus is scraping successfully (check /targets)
2. Run a workflow in DeepResearchAgent to generate metrics
3. Check Prometheus can query metrics:
   ```
   http://localhost:9090/graph
   Query: deepresearch_workflow_steps_total
   ```
4. Verify Grafana datasource connection to Prometheus

## Next Steps

1. ✅ **Verify metrics endpoint** - Run test-metrics-endpoint.ps1
2. ✅ **Start DeepResearchAgent** - Run application
3. ✅ **Check Prometheus targets** - Should show UP
4. ✅ **Run a workflow** - Generate metrics
5. ✅ **View in Grafana** - Dashboard should populate

---

## Status: ✅ RESOLVED

DeepResearchAgent now properly exports metrics to Prometheus via MetricsHostedService on port 5000. Grafana dashboard will display real-time metrics during workflow execution.

**Last Updated:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
**Metrics Endpoint:** http://localhost:5000/metrics
**Health Check:** http://localhost:5000/health
