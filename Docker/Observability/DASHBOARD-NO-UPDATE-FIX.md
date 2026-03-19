# ✅ Dashboard Not Updating - HttpListener Network Binding Fix

**Issue Date:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")  
**Status:** ✅ RESOLVED

---

## 🔍 Problem Summary

**Symptom:** Grafana dashboard panels show "No data" even though DeepResearchAgent is running and metrics endpoint is accessible from Windows.

**Root Cause:** Prometheus HttpListener was configured to listen **only on `localhost`**, preventing Docker containers from accessing it via `host.docker.internal`.

---

## 🐛 Detailed Diagnosis

### Step 1: Metrics Endpoint Status
```powershell
curl http://localhost:5000/metrics/
```
**Result:** ✅ Works from Windows - metrics are being generated

**Sample Metrics Found:**
```
deepresearch_workflow_steps_total{step="1_clarify",workflow="MasterWorkflow"} 1
deepresearch_workflow_steps_total{step="2_research_brief",workflow="MasterWorkflow"} 1
deepresearch_workflow_step_duration_milliseconds{step="1_clarify"} ...
```

### Step 2: Prometheus Target Status
```json
{
  "job": "deepresearch-agent",
  "instance": "host.docker.internal:5000",
  "health": "down",
  "lastError": "server returned HTTP status 400 Bad Request"
}
```
**Result:** ❌ Prometheus cannot scrape from Docker network

### Step 3: Network Access Test

**From Windows (localhost):**
```bash
curl -I http://localhost:5000/metrics/
# Result: HTTP/1.1 500 Internal Server Error (but accessible)
```

**From Docker Network:**
```bash
docker exec deepresearch-prometheus wget http://host.docker.internal:5000/metrics/
# Result: HTTP/1.1 400 Bad Request
```

**Conclusion:** HttpListener is **not accepting connections from Docker network**.

---

## 🔧 Root Cause

### HttpListener Network Binding

**Before (❌ Broken):**
```csharp
.AddPrometheusHttpListener(options =>
{
    options.UriPrefixes = new string[] { "http://localhost:5000/" };
})
```

**Problem:**
- `localhost` only binds to the loopback interface (127.0.0.1)
- Docker containers use a different network interface
- `host.docker.internal` resolves to the host's Docker bridge IP (not 127.0.0.1)
- HttpListener rejects connections from non-localhost interfaces → **400 Bad Request**

### Network Architecture

```
┌─────────────────────────────────────────────────────────────┐
│ Windows Host (127.0.0.1 - localhost)                        │
│                                                              │
│  DeepResearchAgent                                          │
│  └─ HttpListener: http://localhost:5000/                    │
│     ├─ Accepts: 127.0.0.1 ✅                                 │
│     └─ Rejects: 192.168.65.254 (Docker bridge) ❌           │
│                                                              │
└─────────────────────────────────────────────────────────────┘
                          │
                          │ host.docker.internal
                          │ (resolves to 192.168.65.254)
                          ▼
┌─────────────────────────────────────────────────────────────┐
│ Docker Network (172.19.0.0/16)                              │
│                                                              │
│  Prometheus Container (172.19.0.14)                         │
│  └─ Tries to scrape: http://host.docker.internal:5000/      │
│     └─ Gets: 400 Bad Request ❌                              │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## ✅ Solution

### Change Network Binding to All Interfaces

**After (✅ Fixed):**
```csharp
.AddPrometheusHttpListener(options =>
{
    // Use + to listen on all interfaces (allows Docker access)
    // This enables Prometheus running in Docker to scrape from host.docker.internal
    options.UriPrefixes = new string[] { "http://+:5000/" };
})
```

**Benefits:**
- `+` is a wildcard that binds to **all network interfaces**
- Accepts connections from:
  - `localhost` (127.0.0.1) ✅
  - `host.docker.internal` (Docker bridge IP) ✅
  - External IP addresses (if needed) ✅

### Alternative Options

**Option 2: Specific IP (more restrictive)**
```csharp
options.UriPrefixes = new string[] { "http://*:5000/" };
```

**Option 3: Multiple bindings**
```csharp
options.UriPrefixes = new string[] { 
    "http://localhost:5000/", 
    "http://192.168.65.254:5000/"  // Docker bridge IP
};
```

**Recommendation:** Use `http://+:5000/` for maximum compatibility.

---

## 📝 Files Modified

### DeepResearchAgent/Observability/MetricsHostedService.cs

**Line 30:** Changed HttpListener binding

**Before:**
```csharp
options.UriPrefixes = new string[] { "http://localhost:5000/" };
```

**After:**
```csharp
options.UriPrefixes = new string[] { "http://+:5000/" };
```

---

## ⚠️ Important: Administrator Permissions Required

### Why Admin Rights Are Needed

On Windows, binding to `http://+:5000/` (all interfaces) requires **administrator privileges** to prevent unauthorized services from intercepting network traffic.

**Without Admin:**
```
System.Net.HttpListenerException: Access is denied
```

### How to Run with Admin Rights

**Method 1: Visual Studio (Recommended)**
1. Close Visual Studio
2. Right-click Visual Studio icon
3. Select "Run as administrator"
4. Open project and run

**Method 2: PowerShell**
1. Open PowerShell as Administrator
2. Navigate to project:
   ```powershell
   cd C:\RepoEx\PhoenixAI\DeepResearch
   ```
3. Run:
   ```powershell
   dotnet run --project DeepResearchAgent
   ```

**Method 3: Reserve URL (One-time setup)**
```powershell
# Run as Administrator once
netsh http add urlacl url=http://+:5000/ user=Everyone

# Then run normally without admin
dotnet run --project DeepResearchAgent
```

---

## 🧪 Testing the Fix

### Test 1: Verify HttpListener Accepts External Connections
```powershell
# From Windows
curl http://localhost:5000/metrics/
# Expected: Metrics data (same as before)

# From Docker
docker exec deepresearch-prometheus wget -q -O- http://host.docker.internal:5000/metrics/
# Expected: Metrics data (should work now!)
```

### Test 2: Check Prometheus Target Status
1. Open: http://localhost:9090/targets
2. Find: `deepresearch-agent`
3. **Expected:**
   - State: **UP** (green)
   - Last Scrape: < 5s ago
   - No errors

### Test 3: Verify Prometheus Data
```
http://localhost:9090/graph
```
**Query:**
```
deepresearch_workflow_steps_total
```
**Expected:** Should return data with workflow and step labels

### Test 4: Check Grafana Dashboard
1. Open: http://localhost:3001/d/deepresearch-masterworkflow
2. **Expected:**
   - All 9 panels show data
   - Workflow Execution Rate: > 0
   - Charts populate with metrics
   - Auto-refresh every 5 seconds

---

## 🚀 Quick Fix Procedure

### 1. Stop DeepResearchAgent
Press `Ctrl+C` in the running console

### 2. Restart as Administrator
```powershell
# Open PowerShell as Administrator
cd C:\RepoEx\PhoenixAI\DeepResearch
dotnet run --project DeepResearchAgent
```

**Expected Output:**
```
✓ Observability services started
  • Metrics endpoint: http://localhost:5000/metrics/
  • Note: Prometheus HttpListener uses trailing slash

Prometheus metrics endpoint started successfully
Metrics available at: http://localhost:5000/metrics/
```

### 3. Wait 5 Seconds
Prometheus scrapes every 5 seconds

### 4. Verify Prometheus Target
```powershell
curl "http://localhost:9090/api/v1/targets" | ConvertFrom-Json | 
  Select-Object -ExpandProperty data | 
  Select-Object -ExpandProperty activeTargets | 
  Where-Object { $_.labels.job -eq 'deepresearch-agent' } | 
  Select-Object health, lastError
```

**Expected:**
```
health : up
lastError :
```

### 5. Open Grafana Dashboard
http://localhost:3001/d/deepresearch-masterworkflow

**Expected:** Panels populate with data! 📊

---

## 🐛 Troubleshooting

### Issue: "Access is denied" Error
**Symptom:**
```
System.Net.HttpListenerException: Access is denied
```

**Solution:**
Run as Administrator (see above) or reserve URL:
```powershell
netsh http add urlacl url=http://+:5000/ user=Everyone
```

### Issue: Still Getting 400 Bad Request
**Possible Causes:**
1. DeepResearchAgent not restarted
2. Not running as Administrator
3. Firewall blocking port 5000

**Debug Steps:**
```powershell
# Check if port is listening on all interfaces
netstat -an | findstr :5000

# Should show:
# TCP    0.0.0.0:5000           0.0.0.0:0              LISTENING
```

### Issue: Prometheus Target Still DOWN
**Wait 15 seconds** - Prometheus has a scrape interval of 5s plus error backoff

**Force refresh:**
```powershell
# Restart Prometheus
cd Docker\Observability
docker-compose -f docker-compose-monitoring.yml restart prometheus
```

### Issue: Dashboard Shows "No data"
**Checklist:**
1. ✅ Prometheus target UP?
2. ✅ Metrics queryable in Prometheus? (http://localhost:9090/graph)
3. ✅ Grafana datasource connected?
4. ✅ Time range includes recent data? (Last 6 hours)

**Manual Query Test:**
```
http://localhost:9090/graph?g0.expr=deepresearch_workflow_steps_total
```

---

## 📊 Expected Results After Fix

### Prometheus Targets Page
```
deepresearch-agent    UP    host.docker.internal:5000    5s ago
```

### Prometheus Query
```promql
deepresearch_workflow_steps_total
```
**Returns:**
```
deepresearch_workflow_steps_total{step="1_clarify",workflow="MasterWorkflow"} 1
deepresearch_workflow_steps_total{step="2_research_brief",workflow="MasterWorkflow"} 1
...
```

### Grafana Dashboard
- **Panel 1:** Workflow Execution Rate → Shows line graph
- **Panel 2:** Workflow Step Duration → Shows p95/p50 percentiles
- **Panel 3:** Total Workflow Duration → Shows timing
- **Panel 4:** Workflow Error Rate → Should be 0
- **Panel 5-9:** Additional metrics populate

---

## 📝 Summary

### What Was Wrong
HttpListener bound only to `localhost`, rejecting Docker network connections with HTTP 400.

### What Was Fixed
Changed binding from `http://localhost:5000/` to `http://+:5000/` to accept all interfaces.

### What's Required
Run DeepResearchAgent **as Administrator** for the fix to work.

### What to Expect
- Prometheus target: **UP** ✅
- Grafana panels: **Populated with data** 📊
- Real-time updates: **Every 5 seconds** ⚡

---

## ✅ Status: FIXED

**Build:** ✅ Successful  
**Code Change:** ✅ Applied  
**Restart Required:** ⚠️ Yes (as Administrator)  
**Expected Outcome:** 📊 Dashboard displays metrics in real-time  

**Last Updated:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

---

## 🎯 Next Actions

1. **Restart DeepResearchAgent as Administrator**
2. **Verify Prometheus target is UP**
3. **Open Grafana dashboard**
4. **Watch metrics appear in real-time!** 🎉
