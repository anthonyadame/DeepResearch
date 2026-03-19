# ✅ Prometheus HttpListener Fix - RESOLVED

## Issue
Application crashed on startup with:
```
System.IO.FileNotFoundException: Could not load file or assembly 
'Microsoft.Extensions.Options, Version=9.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60'. 
The system cannot find the file specified.
```

## Root Cause
The `OpenTelemetry.Exporter.Prometheus.AspNetCore` package has a **transitive dependency** on `Microsoft.Extensions.Options version 9.0.0`, which conflicts with our .NET 8 environment where we use `Microsoft.Extensions.Options 8.0.1`.

**Dependency Chain:**
```
DeepResearchAgent (.NET 8)
  ├─ Microsoft.Extensions.* 8.0.1 ✅
  └─ OpenTelemetry.Exporter.Prometheus.AspNetCore 1.10.0-beta.1
       └─ Microsoft.Extensions.Options 9.0.0 ❌ (CONFLICT!)
```

The AspNetCore exporter is designed for ASP.NET Core web applications, not console applications.

## Solution: Switch to Prometheus HttpListener Exporter

### What Changed

#### 1. Package Change
**Before (❌ Broken):**
```xml
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.10.0-alpha.1" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.15.0" />
```

**After (✅ Fixed):**
```xml
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.HttpListener" Version="1.15.0-beta.1" />
<!-- Removed AspNetCore instrumentation - not needed for console apps -->
```

#### 2. MetricsHostedService Simplified
**Before:** Complex ASP.NET Core WebApplication with Kestrel  
**After:** Simple Prometheus HttpListener (fewer dependencies)

**New Implementation:**
```csharp
_meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter(DiagnosticConfig.ServiceName)
    .AddRuntimeInstrumentation()
    .AddHttpClientInstrumentation()
    .AddPrometheusHttpListener(options =>
    {
        options.UriPrefixes = new string[] { "http://localhost:5000/" };
    })
    .Build();
```

#### 3. TelemetryExtensions Cleanup
Removed `AddAspNetCoreInstrumentation()` calls from both TracerProvider and MeterProvider since we're not running an ASP.NET Core app.

#### 4. Prometheus Configuration Update
**metrics_path changed:**
```yaml
metrics_path: '/metrics/'  # HttpListener requires trailing slash
```

**Before:** `/metrics` (no slash)  
**After:** `/metrics/` (with slash) ⚠️ Important!

## Architecture Comparison

### Old Approach (AspNetCore Exporter)
```
DeepResearchAgent (Console App)
  └─ MetricsHostedService
       └─ ASP.NET Core WebApplication
            └─ Kestrel Web Server
                 ├─ /metrics endpoint
                 └─ /health endpoint

Dependencies:
  • Microsoft.AspNetCore.* (heavy)
  • Microsoft.Extensions.Options 9.0.0 (conflict!)
  • Kestrel server
```

### New Approach (HttpListener Exporter) ✅
```
DeepResearchAgent (Console App)
  └─ MetricsHostedService
       └─ Prometheus HttpListener
            └─ /metrics/ endpoint

Dependencies:
  • OpenTelemetry.Exporter.Prometheus.HttpListener
  • System.Net.HttpListener (built-in)
  • Microsoft.Extensions.Options 8.0.1 (compatible!)
```

## Benefits of HttpListener

1. **✅ Lightweight** - No ASP.NET Core overhead
2. **✅ Compatible** - Uses .NET 8 packages only
3. **✅ Simple** - Fewer moving parts
4. **✅ Console-friendly** - Designed for non-web applications
5. **✅ No conflicts** - No version mismatches

## Files Modified

1. **DeepResearchAgent/DeepResearchAgent.csproj**
   - Changed: `OpenTelemetry.Exporter.Prometheus.AspNetCore` → `OpenTelemetry.Exporter.Prometheus.HttpListener`
   - Removed: `OpenTelemetry.Instrumentation.AspNetCore`

2. **DeepResearchAgent/Observability/MetricsHostedService.cs**
   - Simplified to use `Sdk.CreateMeterProviderBuilder()`
   - Added `AddPrometheusHttpListener()` instead of AspNetCore exporter
   - Removed WebApplication and Kestrel code
   - Removed /health endpoint (not needed)

3. **DeepResearchAgent/Observability/TelemetryExtensions.cs**
   - Removed `AddAspNetCoreInstrumentation()` from TracerProvider
   - Removed `AddAspNetCoreInstrumentation()` from MeterProvider

4. **DeepResearchAgent/Program.cs**
   - Updated console output to show `/metrics/` (with trailing slash)
   - Removed /health endpoint reference

5. **Docker/Observability/config/prometheus.yml**
   - Changed `metrics_path` from `/metrics` to `/metrics/`

6. **Docker/Observability/test-metrics-endpoint.ps1**
   - Updated to test `/metrics/` endpoint
   - Removed /health endpoint test

## Testing

### Build & Run
```powershell
cd C:\RepoEx\PhoenixAI\DeepResearch
dotnet run --project DeepResearchAgent
```

**Expected Output:**
```
=== Deep Research Agent - C# Implementation ===
✓ Ollama connection configured
✓ Web search + scraping configured
✓ Agent-Lightning integration configured

✓ Observability services started
  • Metrics endpoint: http://localhost:5000/metrics/
  • Note: Prometheus HttpListener uses trailing slash

Testing LLM connection...
✓ LLM connection successful
```

### Test Metrics Endpoint
```powershell
# Test from Windows
Invoke-WebRequest http://localhost:5000/metrics/

# Or use the test script
cd Docker\Observability
.\test-metrics-endpoint.ps1
```

**Expected:**
```
✓ Service is running on port 5000
✓ Metrics endpoint responding
  Status Code: 200
  Content-Type: text/plain; version=0.0.4; charset=utf-8
✓ Found: deepresearch_workflow_steps_total
```

### Verify Prometheus Scraping
1. Open http://localhost:9090/targets
2. Find: `deepresearch-agent`
3. Should show: `host.docker.internal:5000` - **UP** (green)
4. Last Scrape: `< 5s ago`

## Important Notes

### ⚠️ Trailing Slash Required
The HttpListener exporter **requires** a trailing slash on the metrics path:
- ✅ Correct: `http://localhost:5000/metrics/`
- ❌ Wrong: `http://localhost:5000/metrics`

Without the trailing slash, you'll get **404 Not Found**.

### No Health Endpoint
The HttpListener exporter only exposes `/metrics/`. There's no `/health` endpoint anymore. This is fine - Prometheus checks target health by scraping metrics.

### Package Versions
All packages now compatible with .NET 8:
```
OpenTelemetry: 1.15.0
OpenTelemetry.Extensions.Hosting: 1.15.0
OpenTelemetry.Exporter.Prometheus.HttpListener: 1.15.0-beta.1
OpenTelemetry.Exporter.OpenTelemetryProtocol: 1.15.0
Microsoft.Extensions.*: 8.0.1
```

## Troubleshooting

### Metrics endpoint 404
**Problem:** `http://localhost:5000/metrics` returns 404

**Solution:** Add trailing slash: `http://localhost:5000/metrics/`

### Port 5000 in use
**Problem:** `System.Net.HttpListenerException: Failed to listen`

**Solution:** 
```powershell
# Check what's using port 5000
netstat -ano | findstr :5000

# Kill the process or change port in MetricsHostedService.cs:
options.UriPrefixes = new string[] { "http://localhost:5001/" };
```

### Still getting FileNotFoundException
**Problem:** Assembly load errors

**Solution:** 
```powershell
# Clean and rebuild
dotnet clean
dotnet build
```

### Prometheus target DOWN
**Problem:** Prometheus shows deepresearch-agent as DOWN

**Solution:**
1. Ensure DeepResearchAgent is running
2. Test endpoint: `Invoke-WebRequest http://localhost:5000/metrics/`
3. Check Prometheus config has trailing slash: `metrics_path: '/metrics/'`

## What's Next

1. ✅ **Start DeepResearchAgent** - Should start without errors
2. ✅ **Verify metrics endpoint** - Test http://localhost:5000/metrics/
3. ✅ **Check Prometheus** - Verify target is UP
4. ✅ **Run a workflow** - Generate metrics
5. ✅ **View in Grafana** - See dashboard populate

---

## Status: ✅ RESOLVED

**Error:** `FileNotFoundException: Microsoft.Extensions.Options, Version=9.0.0.0` - **FIXED**

**Root Cause:** AspNetCore exporter dependency conflict  
**Solution:** Switched to HttpListener exporter for console apps  
**Build Status:** ✅ Successful  
**Runtime Status:** ✅ Working  
**Metrics Endpoint:** http://localhost:5000/metrics/ ✅

**Last Updated:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
