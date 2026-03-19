# Test DeepResearchAgent Metrics Endpoint
# Verifies that Prometheus HttpListener exposes metrics

Write-Host "`n=== DeepResearchAgent Metrics Endpoint Test ===" -ForegroundColor Cyan

# Check if port 5000 is available
Write-Host "`n1. Checking if port 5000 is available..." -ForegroundColor Yellow
$portInUse = Test-NetConnection -ComputerName localhost -Port 5000 -InformationLevel Quiet -WarningAction SilentlyContinue
if ($portInUse) {
    Write-Host "  ✓ Service is already running on port 5000" -ForegroundColor Green
} else {
    Write-Host "  ℹ Port 5000 is available (service not yet started)" -ForegroundColor White
}

# Test metrics endpoint (note the trailing slash for HttpListener)
Write-Host "`n2. Testing Prometheus metrics endpoint..." -ForegroundColor Yellow
try {
    $metrics = Invoke-WebRequest -Uri "http://localhost:5000/metrics/" -UseBasicParsing -TimeoutSec 5
    Write-Host "  ✓ Metrics endpoint responding" -ForegroundColor Green
    Write-Host "    Status Code: $($metrics.StatusCode)" -ForegroundColor White
    Write-Host "    Content-Type: $($metrics.Headers['Content-Type'])" -ForegroundColor White

    # Check for DeepResearch-specific metrics
    $content = $metrics.Content
    $deepresearchMetrics = @(
        "deepresearch_workflow_steps_total",
        "deepresearch_workflow_step_duration",
        "deepresearch_workflow_total_duration",
        "deepresearch_workflow_errors_total"
    )

    Write-Host "`n3. Checking for DeepResearch metrics..." -ForegroundColor Yellow
    foreach ($metric in $deepresearchMetrics) {
        if ($content -match $metric) {
            Write-Host "  ✓ Found: $metric" -ForegroundColor Green
        } else {
            Write-Host "  ✗ Missing: $metric (may appear after workflow execution)" -ForegroundColor Yellow
        }
    }

    # Display sample metrics
    Write-Host "`n4. Sample metrics output (first 500 chars):" -ForegroundColor Yellow
    Write-Host "  $($content.Substring(0, [Math]::Min(500, $content.Length)))" -ForegroundColor White

} catch {
    Write-Host "  ✗ Metrics endpoint not responding" -ForegroundColor Red
    Write-Host "    Error: $_" -ForegroundColor Red
    Write-Host "`n  Make sure DeepResearchAgent is running!" -ForegroundColor Yellow
}

Write-Host "`n=== Test Summary ===" -ForegroundColor Cyan
Write-Host "To start DeepResearchAgent and expose metrics:" -ForegroundColor White
Write-Host "  cd C:\RepoEx\PhoenixAI\DeepResearch" -ForegroundColor Gray
Write-Host "  dotnet run --project DeepResearchAgent" -ForegroundColor Gray
Write-Host "`nMetrics will be available at:" -ForegroundColor White
Write-Host "  • Metrics: http://localhost:5000/metrics/ (note trailing slash)" -ForegroundColor Cyan
Write-Host "`nPrometheus will scrape metrics every 5 seconds from:" -ForegroundColor White
Write-Host "  host.docker.internal:5000/metrics/" -ForegroundColor Cyan
Write-Host ""
