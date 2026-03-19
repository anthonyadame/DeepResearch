# Quick Start Guide - Phase A Implementation

## ⚠️ Prerequisites

Before getting started, ensure you have:
- ✅ Docker and Docker Compose installed
- ✅ .NET 8 SDK installed
- ✅ PowerShell or bash terminal
- ✅ Ports available: 4317 (Jaeger OTLP), 16686 (Jaeger UI), 3001 (Grafana), 9090 (Prometheus), 5000 (API)

---

## 🚀 Get Started in 10 Minutes

### Step 1️⃣ Verify Build
```bash
cd C:\RepoEx\PhoenixAI\DeepResearch
dotnet build
```
**Expected:** ✅ Build succeeded

### Step 2️⃣ Start Observability Stack (IMPORTANT - Required for tracing/metrics)
```bash
cd Docker/Observability
docker-compose -f docker-compose-monitoring.yml up -d
```

**Verify services are running:**
```bash
docker ps | findstr deepresearch
```

**Expected services:**
- ✅ deepresearch-jaeger (Jaeger tracing on port 16686)
- ✅ deepresearch-prometheus (Prometheus metrics on port 9090)
- ✅ deepresearch-grafana (Grafana dashboards on port 3001)
- ✅ deepresearch-alertmanager (Alert management on port 9093)

> **⚠️ Common Issue:** If you skip this step, traces and metrics will NOT appear in Grafana/Jaeger. Make sure docker-compose completes startup (wait ~30 seconds).

### Step 3️⃣ Run Unit Tests
```bash
dotnet test --filter "Observability"
```
**Expected:** All tests pass

### Step 4️⃣ Start the API (Development Mode)
```bash
cd DeepResearch.Api
dotnet run
```
**Expected:** API starts on `http://localhost:5000`

**Expected output:**
```
✓ Observability services started
  • Metrics endpoint: http://localhost:5000/metrics/
```

### Step 5️⃣ Check Health Status
```bash
curl http://localhost:5000/health
```
**Expected Output:**
```json
{
  "status": "Healthy",
  "checks": {
    "metrics_queue": {
      "status": "Healthy",
      "description": "Async metrics is disabled, no queue monitoring needed"
    }
  }
}
```

### Step 6️⃣ Check Metrics Endpoint
```bash
curl http://localhost:5000/metrics
```
**Expected:** Prometheus metrics output with `deepresearch_*` metrics

---

## 🧪 Test Configurations

### Test Feature Toggle (Disable Tracing)
1. Edit `DeepResearch.Api/appsettings.Development.json`
2. Set `"EnableTracing": false`
3. Restart API
4. Verify no traces in Grafana Tempo

### Test Sampling (10% Traces)
1. Edit `DeepResearch.Api/appsettings.Development.json`
2. Set `"TraceSamplingRate": 0.1`
3. Restart API
4. Verify ~10% of operations create traces

### Test Async Metrics
1. Edit `DeepResearch.Api/appsettings.Development.json`
2. Set `"UseAsyncMetrics": true`
3. Restart API
4. Check health endpoint shows queue stats

---

## 📊 Run Benchmarks

### Quick Benchmark
```bash
cd DeepResearchAgent.Tests
dotnet run -c Release --project DeepResearchAgent.Tests.csproj
```

### Full Benchmark with Reports
```bash
cd DeepResearchAgent.Tests
dotnet run -c Release -- --filter "*WorkflowPerformanceBenchmark*" --memory --exporters json html
```

**Output Location:** `DeepResearchAgent.Tests/BenchmarkDotNet.Artifacts/results/`

---

## 🔍 Verify Grafana & Jaeger Integration

> **Prerequisite:** You must have completed Step 2️⃣ (Start Observability Stack) before these services will be available.

### Access Jaeger UI (Distributed Traces)
```
URL: http://localhost:16686
```

**⚠️ IMPORTANT: DeepResearchAgent only sends traces via the Streaming Endpoint**

The "DeepResearchAgent" service will appear in Jaeger ONLY when you call the **streaming workflow endpoint**, which creates `MasterWorkflow.StreamStateAsync` spans.

**To generate and view traces:**

1. **Verify Jaeger is running:**
   ```bash
   curl http://localhost:16686/api/health
   # Expected: {"status":"UP"}
   ```

2. **Start the API (if not already running):**
   ```bash
   cd DeepResearch.Api
   dotnet run
   # Expected: API listens on http://localhost:5000
   ```

3. **Call the streaming workflow endpoint to generate traces:**
   ```bash
   # PowerShell
   $body = @{ query = "What are the latest developments in AI in 2024?" } | ConvertTo-Json
   Invoke-WebRequest -Uri "http://localhost:5000/api/workflows/stream" `
     -Method POST `
     -ContentType "application/json" `
     -Body $body

   # Or curl
   curl -X POST http://localhost:5000/api/workflows/stream \
     -H "Content-Type: application/json" \
     -d '{"query":"What are the latest developments in AI in 2024?"}'
   ```

4. **View traces in Jaeger:**
   - Open http://localhost:16686
   - Service dropdown → Select **"DeepResearchAgent"** (now visible after the request above)
   - Operation dropdown → Select **"MasterWorkflow.StreamStateAsync"**
   - Click "Find Traces"
   - Click on any trace to view the detailed flame graph with all workflow steps

### Access Grafana Dashboards
```
URL: http://localhost:3001
Username: admin
Password: admin
```

**Navigate to:**
- Dashboards → DeepResearch → MasterWorkflow Dashboard
- View workflow metrics, step durations, and performance graphs

### Query Traces in Grafana (with Jaeger datasource)
1. Open Grafana → Explore (left sidebar)
2. Select "Jaeger" datasource (dropdown at top)
3. Configure query:
   - Service: "DeepResearchAgent"
   - Operation: "MasterWorkflow.StreamStateAsync"
4. Click "Run query"
5. Traces will appear with timeline and span details

### Query Metrics in Prometheus
1. Open Grafana → Explore (left sidebar)
2. Select "Prometheus" datasource
3. Query: `deepresearch_workflow_step_duration_bucket`
4. Visualize: Select "Heatmap" or "Graph" panel
5. Adjust time range to see recent metrics

### Verify Metrics Direct from Prometheus
```bash
# Access Prometheus directly (bypassing Grafana)
curl http://localhost:9090/api/v1/targets
```

Expected: Shows "deepresearch-api" and "deepresearch-agent" as healthy scrape targets

---

## 🎛️ Configuration Presets

### Development (Full Observability)
```json
"Observability": {
  "EnableTracing": true,
  "TraceSamplingRate": 1.0,
  "UseAsyncMetrics": false
}
```

### Production (Minimal Overhead)
```json
"Observability": {
  "EnableTracing": true,
  "TraceSamplingRate": 0.1,
  "UseAsyncMetrics": true,
  "SlowOperationThresholdMs": 10000
}
```

### Disabled (Zero Overhead)
```json
"Observability": {
  "EnableTracing": false,
  "EnableMetrics": false
}
```

---

## 📋 Validation Checklist

Run through this checklist to validate implementation:

### Build & Tests
- [ ] `dotnet build` succeeds
- [ ] `dotnet test --filter "Observability"` passes
- [ ] No warnings in build output

### Configuration
- [ ] API starts with Development config
- [ ] API starts with Production config
- [ ] Invalid config throws validation error
- [ ] Health endpoint returns HTTP 200

### Feature Toggle
- [ ] Tracing can be disabled (`EnableTracing: false`)
- [ ] Metrics can be disabled (`EnableMetrics: false`)
- [ ] Sampling works (`TraceSamplingRate: 0.1` = 10% traces)
- [ ] Slow operation threshold filters (`SlowOperationThresholdMs: 10000`)

### Async Metrics
- [ ] Async collector starts when `UseAsyncMetrics: true`
- [ ] Health check shows queue stats
- [ ] Metrics appear in Prometheus
- [ ] No metrics dropped (check `TotalDropped: 0`)

### Grafana Integration
- [ ] Traces appear in Jaeger UI (http://localhost:16686)
- [ ] Traces accessible via Grafana Jaeger datasource
- [ ] Metrics appear in Prometheus
- [ ] Flame graphs render correctly in Jaeger
- [ ] Dashboard shows workflow steps

---

## 🐛 Troubleshooting

### Issue: Build Fails
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

### Issue: Tests Fail
```bash
# Run specific test
dotnet test --filter "ObservabilityConfigurationTests.Development_ReturnsFullObservabilityConfiguration"

# Check test output
dotnet test --logger "console;verbosity=detailed"
```

### Issue: API Won't Start
```bash
# Check port availability
netstat -ano | findstr :5000

# Check logs
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project DeepResearch.Api
```

### Issue: DeepResearchAgent Service Missing in Jaeger (SOLVED ✅)
**Problem:** Jaeger UI shows no "DeepResearchAgent" service in the dropdown.

**Root Cause:** The DeepResearchAgent application must ACTUALLY SEND TRACES to Jaeger. This only happens when:
1. ✅ ActivityScope is properly configured (NOW FIXED in Program.cs)
2. ✅ The streaming workflow endpoint is called (which uses ActivityScope.Start())
3. ✅ Traces are flushed before the application exits (NOW ADDED ForceFlush)

**Solution (Now Fixed):**
- ✅ `DeepResearchAgent/Program.cs` now initializes `ActivityScope.Configure()`
- ✅ `ActivityScope.Configure()` is called with tracing enabled and 100% sampling
- ✅ `ForceFlush()` is called before exit to ensure traces reach Jaeger
- ✅ Console output now shows: "Distributed tracing enabled: OTLP → http://localhost:4317 (Jaeger)"

**To verify traces now appear:**
1. Start observability stack: `cd Docker/Observability && docker-compose -f docker-compose-monitoring.yml up -d`
2. Start API: `cd DeepResearch.Api && dotnet run`
3. Call streaming endpoint:
   ```bash
   curl -X POST http://localhost:5000/api/workflows/stream \
     -H "Content-Type: application/json" \
     -d '{"query":"What is machine learning?"}'
   ```
4. Wait 2-3 seconds for traces to export
5. Open Jaeger: http://localhost:16686
6. Service dropdown should now show "DeepResearchAgent"

### Issue: No Traces in Grafana Jaeger Datasource
1. ✅ Verify Grafana can reach Jaeger: Settings → Datasources → Jaeger
2. ✅ Test connection should show "Data source is working"
3. ✅ If not, check network connectivity: `docker network ls | grep deepresearch`
4. ✅ Services should be on same network: `deepresearch-hub`

**Verification:**
```bash
# Confirm Jaeger health
curl http://localhost:16686/api/health

# Expected: {"status":"UP"}
```

### Issue: No Traces in Jaeger at All
1. ✅ Confirm observability stack started successfully:
   ```bash
   cd Docker/Observability
   docker-compose -f docker-compose-monitoring.yml up -d
   docker ps | grep -E "jaeger|prometheus|grafana"
   ```
2. ✅ Confirm API is running and has processed requests:
   - Check DeepResearchAgent console output for "✓ Observability services started"
   - Execute a workflow: Run a research task or API request
3. ✅ Check Jaeger is listening on OTLP port:
   ```bash
   netstat -ano | findstr :4317  # OTLP gRPC
   # OR
   curl http://localhost:4318/  # OTLP HTTP endpoint (alternative)
   ```
4. ✅ Check API OTLP configuration in `DeepResearchAgent/appsettings.json`:
   - Should show: `"Endpoint": "http://localhost:4317"`
   - Should show: `"Tracing": { "Enabled": true }`

### Issue: Port Already in Use
**Error:** `Address already in use` for port 3001, 16686, 9090, 4317, or 5000

**Solution:**
```bash
# Find process using port (e.g., 3001)
netstat -ano | findstr :3001

# Kill process (replace PID with actual process ID)
taskkill /PID <PID> /F

# OR change Docker mapping in docker-compose-monitoring.yml
# Change "3001:3000" to "3002:3000" to use port 3002 instead
```

### Issue: No Metrics in Prometheus
1. ✅ Confirm Prometheus is running and healthy:
   ```bash
   curl http://localhost:9090/-/healthy
   # Expected: Prometheus Server is Ready to Serve Metrics
   ```
2. ✅ Check Prometheus targets are healthy:
   - Prometheus UI: http://localhost:9090/targets
   - Should show "deepresearch-api" and other targets as "UP"
3. ✅ Verify API metrics endpoint is accessible:
   ```bash
   curl http://localhost:5000/metrics | head -20
   # Should show "# HELP deepresearch_*" metrics
   ```
4. ✅ Check Prometheus scrape config includes the API:
   - Edit `Docker/Observability/config/prometheus.yml`
   - Ensure `static_configs` includes `localhost:5000`

### Issue: No Metrics in Prometheus
1. Check `EnableMetrics: true` in config
2. Verify metrics endpoint: `http://localhost:5000/metrics`
3. Check Prometheus scrape config (15s interval)
4. Verify Prometheus is running: `docker ps | findstr prometheus`

### Issue: High Queue Utilization
1. Check health endpoint: `/health`
2. Look for `QueueUtilization%` > 80%
3. Increase `AsyncMetricsQueueSize` in config
4. Check metrics processing is not stalled

---

## 📞 Need Help?

### Documentation
- **Full Details:** `docs/Phase_A_Implementation_Complete.md`
- **Execution Trace:** `docs/MasterWorkflow_StreamStateAsync_ExecutionTrace.md`
- **Summary:** `docs/PHASE_A_SUMMARY.md`

### Key Files
- Configuration Model: `DeepResearchAgent/Observability/ObservabilityConfiguration.cs`
- Activity Scope: `DeepResearchAgent/Observability/ActivityScope.cs`
- Async Collector: `DeepResearchAgent/Observability/AsyncMetricsCollector.cs`
- Health Check: `DeepResearch.Api/HealthChecks/MetricsQueueHealthCheck.cs`

### Configuration Files
- Base: `DeepResearch.Api/appsettings.json`
- Development: `DeepResearch.Api/appsettings.Development.json`
- Production: `DeepResearch.Api/appsettings.Production.json`

---

## ✅ Success Criteria

You're done when:

1. ✅ Build succeeds
2. ✅ All tests pass
3. ✅ API starts successfully
4. ✅ Health check returns healthy
5. ✅ Metrics appear in Prometheus
6. ✅ Traces appear in Grafana Tempo
7. ✅ Configuration toggle works
8. ✅ Benchmarks run successfully

---

**Ready to proceed to Phase B (Core Performance Optimization)?**

Phase B targets the real bottleneck (LLM operations) and will reduce total workflow time from 120s → 38s (68% improvement).

See `docs/MasterWorkflow_StreamStateAsync_ExecutionTrace.md` section "Two-Track Optimization Strategy" for Phase B details.
