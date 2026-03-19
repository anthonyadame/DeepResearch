# 🔍 Grafana Dashboard Verification Checklist

## Current Status: ✅ All Services Running

### Service Health Check
```
✅ deepresearch-grafana          (healthy) - Port 3001
✅ deepresearch-prometheus       (healthy) - Port 9090
✅ deepresearch-jaeger           (healthy) - Port 16686
✅ deepresearch-alertmanager     (healthy) - Port 9093
⚠️ deepresearch-otel-collector  (unhealthy - optional)
```

## Step-by-Step Verification

### 1. Access Grafana Web UI
- [ ] Open browser to: http://localhost:3001
- [ ] Login with:
  - Username: `admin`
  - Password: `admin`
- [ ] Change password when prompted (or skip)

### 2. Verify Dashboard Provisioning
- [ ] Click on "Dashboards" in the left sidebar (or the hamburger menu)
- [ ] Look for folder named: **"DeepResearch"**
- [ ] Click into the DeepResearch folder
- [ ] Verify dashboard appears: **"DeepResearch MasterWorkflow Observability"**

### 3. Open the Dashboard
- [ ] Click on "DeepResearch MasterWorkflow Observability"
- [ ] Dashboard should load with 9 empty panels:
  1. Workflow Execution Rate
  2. Workflow Step Duration (p95)
  3. Total Workflow Duration
  4. Workflow Error Rate
  5. LLM Request Duration
  6. LLM Tokens Used
  7. Tool Invocation Rate
  8. State Cache Hit Rate
  9. Current Method Execution

### 4. Verify Data Sources
- [ ] Go to: Configuration (⚙️) → Data sources
- [ ] Verify **Prometheus** datasource exists and is working
- [ ] Verify **Jaeger** datasource exists and is working

### 5. Test with Running Application
- [ ] Start DeepResearchAgent:
  ```powershell
  cd C:\RepoEx\PhoenixAI\DeepResearch
  dotnet run --project DeepResearchAgent
  ```
- [ ] Run a workflow query
- [ ] Return to Grafana dashboard
- [ ] Verify metrics start appearing in the panels
- [ ] Check "Current Method Execution" table for live data

### 6. Verify Distributed Tracing
- [ ] Open Jaeger UI: http://localhost:16686
- [ ] Select Service: "DeepResearchAgent"
- [ ] Search for traces
- [ ] Click on a trace to see workflow execution details

### 7. Verify Prometheus Metrics
- [ ] Open Prometheus: http://localhost:9090
- [ ] Go to "Graph" tab
- [ ] Enter query: `deepresearch_workflow_steps_total`
- [ ] Click "Execute"
- [ ] Verify metrics are being collected

## Troubleshooting

### Dashboard Not Appearing
1. Check Grafana logs:
   ```powershell
   docker logs deepresearch-grafana
   ```
2. Look for "finished to provision dashboards" message
3. Check for errors related to dashboard files

### No Metrics Showing
1. Verify DeepResearchAgent is running
2. Check Prometheus targets: http://localhost:9090/targets
3. Ensure DeepResearchAgent exposes `/metrics` endpoint
4. Verify `appsettings.json` has correct OpenTelemetry configuration

### Datasource Connection Errors
1. Ensure all services are healthy:
   ```powershell
   docker-compose -f docker-compose-monitoring.yml ps
   ```
2. Check network connectivity between containers
3. Verify datasource URLs in Grafana:
   - Prometheus: `http://prometheus:9090`
   - Jaeger: `http://jaeger:16686`

## Quick Test Commands

### View all dashboards via API
```powershell
Invoke-RestMethod -Uri "http://localhost:3001/api/search" -Headers @{Authorization="Basic YWRtaW46YWRtaW4="}
```

### Check Prometheus metrics
```powershell
Invoke-RestMethod -Uri "http://localhost:9090/api/v1/query?query=up"
```

### Check Jaeger health
```powershell
Invoke-RestMethod -Uri "http://localhost:16686/api/health"
```

## Expected Results

### ✅ Success Indicators
- Dashboard appears under "Dashboards → DeepResearch"
- All 9 panels are visible (may be empty until app runs)
- No error messages in Grafana
- Datasources show "working" status
- Prometheus shows deepresearch metrics

### ❌ Failure Indicators
- Dashboard not found in UI
- "Dashboard not found" error
- Datasource connection errors
- No metrics in Prometheus
- Empty traces in Jaeger

## Next Steps After Verification
1. ✅ Confirm dashboard is accessible
2. Run a test workflow to generate metrics
3. Configure AlertManager notifications (optional)
4. Customize dashboard panels as needed
5. Set up alerting rules

---
**Date:** $(Get-Date -Format "yyyy-MM-dd HH:mm")
**Stack Status:** Running
**Dashboard UID:** deepresearch-masterworkflow
**Access URL:** http://localhost:3001/d/deepresearch-masterworkflow
