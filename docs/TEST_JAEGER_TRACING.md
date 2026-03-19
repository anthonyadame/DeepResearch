# Test Plan: Verify Jaeger Tracing Works

## Prerequisites
- ✅ Docker and Docker Compose installed
- ✅ .NET 8 SDK installed
- ✅ PowerShell or Bash terminal
- ✅ All code changes built successfully (`dotnet build` returns 0 errors)

## Test Procedure

### Phase 1: Infrastructure Setup (5 minutes)

#### Test 1.1: Start Observability Stack
```bash
cd Docker/Observability
docker-compose -f docker-compose-monitoring.yml up -d
```

**Verify:**
```bash
docker ps | findstr deepresearch
```

**Expected Output:**
```
deepresearch-jaeger
deepresearch-prometheus
deepresearch-grafana
deepresearch-alertmanager
```

**Check Jaeger Health:**
```bash
curl http://localhost:16686/api/health
```

**Expected:**
```json
{"status":"UP"}
```

---

### Phase 2: API Startup (2 minutes)

#### Test 2.1: Start DeepResearch API
```bash
cd DeepResearch.Api
dotnet run
```

**Expected Console Output:**
```
✓ Observability services started
  • Metrics endpoint: http://localhost:5000/metrics/
  • Distributed tracing enabled: OTLP → http://localhost:4317 (Jaeger)
  • Trace sampling: 100% (all requests traced)
  • Note: Traces appear in Jaeger after StreamStateAsync is called
```

**Verify Health Endpoint:**
```bash
curl http://localhost:5000/health
```

**Expected:**
```json
{
  "status": "Healthy",
  "checks": {
    "metrics_queue": {
      "status": "Healthy",
      ...
    }
  }
}
```

---

### Phase 3: Generate Traces (2 minutes)

#### Test 3.1: Call Streaming Workflow Endpoint
```bash
# PowerShell
$body = @{ query = "What are the latest developments in AI?" } | ConvertTo-Json
Invoke-WebRequest -Uri "http://localhost:5000/api/workflows/stream" `
  -Method POST `
  -ContentType "application/json" `
  -Body $body

# Or curl (any shell)
curl -X POST http://localhost:5000/api/workflows/stream \
  -H "Content-Type: application/json" \
  -d '{"query":"What are the latest developments in AI?"}'
```

**Expected Behavior:**
- Stream of JSON responses showing workflow steps
- No errors in API console
- Request completes within 30-120 seconds (depending on system)

**Console Output Example:**
```
{"status":"connected","timestamp":"2024-01-15T10:30:00.000Z"}
{"step":"1","status":"clarifying user intent"}
{"step":"1","status":"completed","message":"query is sufficiently detailed"}
{"step":"2","status":"writing research brief"}
...
```

#### Test 3.2: Wait for Trace Export
**Wait:** 2-3 seconds for OTLP batching and export

**Verify Jaeger Received Data:**
```bash
docker logs deepresearch-jaeger | grep -i "received\|export" | tail -5
```

---

### Phase 4: Verify Jaeger UI (2 minutes)

#### Test 4.1: Access Jaeger
Open in web browser: **http://localhost:16686**

**Expected:** Jaeger UI loads with "Search" interface

#### Test 4.2: Select DeepResearchAgent Service
1. Click **Service** dropdown (top left)
2. Look for **"DeepResearchAgent"** in the list
   - ✅ **CRITICAL TEST POINT:** If this appears, tracing is working!
   - ❌ If it doesn't appear, review troubleshooting section below

#### Test 4.3: Find Traces
1. Operation dropdown → Select **"MasterWorkflow.StreamStateAsync"**
2. Click **"Find Traces"** button
3. Should see 1+ traces in the results

#### Test 4.4: View Trace Detail
1. Click on any trace result
2. **Flame graph** should display showing:
   - `MasterWorkflow.StreamStateAsync` (root span)
   - `Step1.Clarify` (child span)
   - Additional step spans
   - Timing information for each

**Expected Flame Graph Structure:**
```
MasterWorkflow.StreamStateAsync (duration: ~50-200ms)
├── Step1.Clarify (~10-50ms)
├── Step2.WriteResearchBrief (~500-2000ms)
├── Step3.WriteDraftReport (~500-2000ms)
├── Step4.SupervisorLoop (~5000-30000ms)
└── Step5.FinalReport (~500-2000ms)
```

---

### Phase 5: Verify Grafana Integration (Optional, 2 minutes)

#### Test 5.1: Access Grafana
Open in web browser: **http://localhost:3001**

**Login:**
- Username: `admin`
- Password: `admin`

#### Test 5.2: View Dashboard
1. Click **Dashboards** (left sidebar)
2. Navigate to **DeepResearch** folder
3. Select **MasterWorkflow Dashboard**
4. Should see metrics graphs:
   - Workflow duration timeline
   - Step execution times
   - Error rates (should be 0)

#### Test 5.3: Check Prometheus Data Source
1. Click **Explore** (left sidebar)
2. Select **Prometheus** datasource
3. Query: `deepresearch_workflow_step_duration_bucket`
4. Click **Run query**
5. Should see metrics with data points from the workflow execution

---

## Troubleshooting

### Issue: DeepResearchAgent Service Still Not Appearing
**Symptoms:**
- Jaeger service dropdown is empty or missing "DeepResearchAgent"

**Diagnosis:**
```bash
# Check Jaeger logs
docker logs deepresearch-jaeger | grep -i error | tail -10

# Check if Jaeger OTLP port is listening
netstat -ano | findstr :4317
```

**Solution Checklist:**
- [ ] API output shows "Distributed tracing enabled: OTLP → http://localhost:4317"
- [ ] Called `/api/workflows/stream` endpoint (not just `/health`)
- [ ] Waited 3+ seconds after the call before checking Jaeger
- [ ] Jaeger container is running: `docker ps | grep jaeger`
- [ ] No connection errors in Docker logs

### Issue: Jaeger Shows Errors
**Check Docker Logs:**
```bash
docker logs deepresearch-jaeger
```

**Common Errors:**
- `Port already in use` → Change port in docker-compose.yml
- `Connection refused` → Jaeger container not fully started (wait 30 seconds)

### Issue: API Won't Start
**Check Port 5000:**
```bash
netstat -ano | findstr :5000
```

**If Port is Busy:**
```bash
# Find and kill the process
taskkill /PID <PID> /F

# Or change API port in launchSettings.json
```

### Issue: No Data in Prometheus
**Check Metrics Endpoint:**
```bash
curl http://localhost:5000/metrics | head -20
```

**Expected:**
```
# HELP deepresearch_workflow_steps_total Total number of workflow steps executed
# TYPE deepresearch_workflow_steps_total counter
deepresearch_workflow_steps_total 5
```

---

## Success Criteria

✅ **All tests pass when:**

1. ✅ Observability stack starts without errors
2. ✅ API starts and shows tracing enabled message
3. ✅ Streaming endpoint responds with workflow steps
4. ✅ Jaeger UI shows "DeepResearchAgent" service
5. ✅ Traces appear with MasterWorkflow.StreamStateAsync operation
6. ✅ Flame graph shows workflow step spans
7. ✅ Grafana dashboard displays metrics
8. ✅ Prometheus shows deepresearch_* metrics

## Performance Metrics

After running the test, measure these metrics:

1. **Trace Export Latency:** 
   - Time from workflow end to trace appearing in Jaeger
   - Expected: 2-5 seconds

2. **Trace Data Size:**
   - Size of each trace in Jaeger
   - Expected: 5-20 KB per trace

3. **API Response Time:**
   - Time for `/api/workflows/stream` to complete
   - Expected: 30-120 seconds

---

## Cleanup

After testing, stop services:

```bash
# Stop API (Ctrl+C in API terminal)

# Stop observability stack
cd Docker/Observability
docker-compose -f docker-compose-monitoring.yml down

# Optional: Remove volumes to reset data
docker volume prune -f
```

