# Metrics Verification Test Script
# Run this after starting DeepResearchAgent to verify all new metrics are registered

Write-Host "🧪 DeepResearch Metrics Verification" -ForegroundColor Cyan
Write-Host "====================================`n" -ForegroundColor Cyan

# Check if DeepResearchAgent is running
Write-Host "1️⃣  Checking if DeepResearchAgent is running..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5000/metrics/" -UseBasicParsing -TimeoutSec 5
    Write-Host "   ✅ DeepResearchAgent is running on port 5000`n" -ForegroundColor Green
} catch {
    Write-Host "   ❌ DeepResearchAgent is NOT running!" -ForegroundColor Red
    Write-Host "   💡 Start it with: cd DeepResearchAgent; dotnet run`n" -ForegroundColor Yellow
    exit 1
}

# Parse metrics
$metricsContent = $response.Content

# Define expected new metrics
$expectedMetrics = @(
    @{Name="deepresearch_workflow_active"; Type="gauge"; Description="Active workflow tracking"},
    @{Name="deepresearch_state_cache_hits_total"; Type="counter"; Description="Cache hit tracking"},
    @{Name="deepresearch_state_cache_misses_total"; Type="counter"; Description="Cache miss tracking"},
    @{Name="deepresearch_llm_errors_total"; Type="counter"; Description="LLM error tracking"},
    @{Name="deepresearch_tools_errors_total"; Type="counter"; Description="Tool error tracking"}
)

Write-Host "2️⃣  Verifying new metrics registration..." -ForegroundColor Yellow
$allFound = $true
foreach ($metric in $expectedMetrics) {
    if ($metricsContent -match $metric.Name) {
        Write-Host "   ✅ $($metric.Name)" -ForegroundColor Green -NoNewline
        Write-Host " - $($metric.Description)" -ForegroundColor Gray
    } else {
        Write-Host "   ❌ $($metric.Name) NOT FOUND!" -ForegroundColor Red
        $allFound = $false
    }
}

if ($allFound) {
    Write-Host "`n   🎉 All 5 new metrics successfully registered!`n" -ForegroundColor Green
} else {
    Write-Host "`n   ⚠️  Some metrics are missing. Check instrumentation code.`n" -ForegroundColor Yellow
}

# Check Prometheus scraping
Write-Host "3️⃣  Checking Prometheus target status..." -ForegroundColor Yellow
try {
    $promResponse = Invoke-WebRequest -Uri "http://localhost:9090/api/v1/targets" -UseBasicParsing -TimeoutSec 5
    $targetsData = $promResponse.Content | ConvertFrom-Json

    $deepResearchTarget = $targetsData.data.activeTargets | Where-Object { $_.labels.job -eq "deepresearch-agent" }

    if ($deepResearchTarget -and $deepResearchTarget.health -eq "up") {
        Write-Host "   ✅ Prometheus is scraping DeepResearchAgent successfully" -ForegroundColor Green
        Write-Host "   📍 Target: $($deepResearchTarget.scrapeUrl)`n" -ForegroundColor Gray
    } else {
        Write-Host "   ⚠️  Prometheus target is DOWN or not found" -ForegroundColor Yellow
        Write-Host "   💡 Check Prometheus at: http://localhost:9090/targets`n" -ForegroundColor Gray
    }
} catch {
    Write-Host "   ❌ Cannot reach Prometheus on port 9090" -ForegroundColor Red
    Write-Host "   💡 Start observability stack: docker-compose up -d`n" -ForegroundColor Yellow
}

# Query specific metrics from Prometheus
Write-Host "4️⃣  Querying metrics from Prometheus..." -ForegroundColor Yellow
$metricsToQuery = @(
    "deepresearch_workflow_active",
    "deepresearch_state_cache_hits_total",
    "deepresearch_llm_errors_total"
)

foreach ($metricName in $metricsToQuery) {
    try {
        $queryUrl = "http://localhost:9090/api/v1/query?query=$metricName"
        $queryResponse = Invoke-WebRequest -Uri $queryUrl -UseBasicParsing -TimeoutSec 5
        $queryData = $queryResponse.Content | ConvertFrom-Json

        if ($queryData.data.result.Count -gt 0) {
            $value = $queryData.data.result[0].value[1]
            Write-Host "   ✅ $metricName = $value" -ForegroundColor Green
        } else {
            Write-Host "   ⚠️  $metricName - No data yet (run workflow to generate)" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "   ❌ Error querying $metricName" -ForegroundColor Red
    }
}

# Check Grafana dashboard
Write-Host "`n5️⃣  Checking Grafana dashboard..." -ForegroundColor Yellow
try {
    $grafanaResponse = Invoke-WebRequest -Uri "http://localhost:3001/api/health" -UseBasicParsing -TimeoutSec 5
    Write-Host "   ✅ Grafana is running" -ForegroundColor Green
    Write-Host "   🔗 Dashboard: http://localhost:3001/d/deepresearch-masterworkflow`n" -ForegroundColor Cyan
} catch {
    Write-Host "   ❌ Cannot reach Grafana on port 3001" -ForegroundColor Red
    Write-Host "   💡 Check Docker: docker ps | findstr grafana`n" -ForegroundColor Yellow
}

# Summary
Write-Host "====================================`n" -ForegroundColor Cyan
Write-Host "📊 Metrics Status Summary" -ForegroundColor Cyan
Write-Host "====================================`n" -ForegroundColor Cyan

$existingMetrics = @("workflow_steps_total", "workflow_step_duration", "workflow_total_duration", "workflow_errors_total")
$newMetrics = 5
$totalMetrics = $existingMetrics.Count + $newMetrics

Write-Host "   Existing Metrics:  $($existingMetrics.Count)" -ForegroundColor Gray
Write-Host "   New Metrics:       $newMetrics" -ForegroundColor Green
Write-Host "   Total Active:      $totalMetrics" -ForegroundColor Cyan
Write-Host "   Coverage:          47% (9/19 defined metrics)`n" -ForegroundColor Yellow

Write-Host "✅ Next Steps:" -ForegroundColor Green
Write-Host "   1. Run a workflow to generate real metric data" -ForegroundColor White
Write-Host "   2. Check Grafana dashboard for updated panels" -ForegroundColor White
Write-Host "   3. Verify Panel 8 (Cache Hit Rate) shows percentage" -ForegroundColor White
Write-Host "   4. Check Panel 10 (Active Workflows) and Panel 11 (Errors)`n" -ForegroundColor White

Write-Host "📚 Documentation:" -ForegroundColor Cyan
Write-Host "   - METRICS-INSTRUMENTATION-SUMMARY.md" -ForegroundColor Gray
Write-Host "   - DIAGNOSTIC-CONFIG-IMPROVEMENTS.md" -ForegroundColor Gray
Write-Host "   - IMPLEMENTATION-COMPLETE.md`n" -ForegroundColor Gray
