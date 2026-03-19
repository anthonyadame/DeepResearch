# Grafana Dashboard Validation Script
# Verifies that dashboard provisioning is configured correctly

Write-Host "`n=== Grafana Dashboard Provisioning Validation ===" -ForegroundColor Cyan

$dashboardPath = "config/grafana/dashboards/masterworkflow-dashboard.json"
$providerPath = "config/grafana/dashboards/dashboard-provider.yml"

# Check if files exist
Write-Host "`n1. Checking file existence..." -ForegroundColor Yellow
if (Test-Path $dashboardPath) {
    Write-Host "  ✓ Dashboard JSON found: $dashboardPath" -ForegroundColor Green
} else {
    Write-Host "  ✗ Dashboard JSON NOT found: $dashboardPath" -ForegroundColor Red
    exit 1
}

if (Test-Path $providerPath) {
    Write-Host "  ✓ Provider config found: $providerPath" -ForegroundColor Green
} else {
    Write-Host "  ✗ Provider config NOT found: $providerPath" -ForegroundColor Red
    exit 1
}

# Validate JSON structure
Write-Host "`n2. Validating dashboard JSON structure..." -ForegroundColor Yellow
try {
    $dashboard = Get-Content $dashboardPath -Raw | ConvertFrom-Json

    if ($dashboard.uid) {
        Write-Host "  ✓ Dashboard has 'uid': $($dashboard.uid)" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Dashboard missing 'uid' property" -ForegroundColor Red
    }

    if ($dashboard.title) {
        Write-Host "  ✓ Dashboard title: $($dashboard.title)" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Dashboard missing 'title' property" -ForegroundColor Red
    }

    if ($dashboard.panels) {
        Write-Host "  ✓ Dashboard has $($dashboard.panels.Count) panels" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Dashboard missing 'panels' array" -ForegroundColor Red
    }

    # Check for incorrect wrapper structure
    if ($dashboard.dashboard) {
        Write-Host "  ✗ WARNING: Dashboard has incorrect 'dashboard' wrapper!" -ForegroundColor Red
        Write-Host "    This will prevent proper provisioning." -ForegroundColor Red
    }

} catch {
    Write-Host "  ✗ Invalid JSON: $_" -ForegroundColor Red
    exit 1
}

# Validate provider config
Write-Host "`n3. Validating provider configuration..." -ForegroundColor Yellow
$providerContent = Get-Content $providerPath -Raw

if ($providerContent -match "folder:\s*'?DeepResearch'?") {
    Write-Host "  ✓ Provider configured for 'DeepResearch' folder" -ForegroundColor Green
} else {
    Write-Host "  ✗ Provider not configured for 'DeepResearch' folder" -ForegroundColor Red
}

if ($providerContent -match "path:\s*/etc/grafana/provisioning/dashboards") {
    Write-Host "  ✓ Provider path is correct" -ForegroundColor Green
} else {
    Write-Host "  ✗ Provider path is incorrect" -ForegroundColor Red
}

if ($providerContent -match "foldersFromFilesStructure:\s*true") {
    Write-Host "  ⚠ WARNING: foldersFromFilesStructure is enabled" -ForegroundColor Yellow
    Write-Host "    This may cause issues with flat dashboard structure" -ForegroundColor Yellow
}

# Summary
Write-Host "`n=== Validation Summary ===" -ForegroundColor Cyan
Write-Host "Dashboard Configuration:" -ForegroundColor White
Write-Host "  • UID: $($dashboard.uid)" -ForegroundColor White
Write-Host "  • Title: $($dashboard.title)" -ForegroundColor White
Write-Host "  • Panels: $($dashboard.panels.Count)" -ForegroundColor White
Write-Host "`nExpected Grafana Navigation Path:" -ForegroundColor White
Write-Host "  Dashboards → DeepResearch → $($dashboard.title)" -ForegroundColor Cyan

Write-Host "`nNext Steps:" -ForegroundColor Yellow
Write-Host "  1. Start the observability stack: .\start-observability.ps1" -ForegroundColor White
Write-Host "  2. Access Grafana: http://localhost:3001" -ForegroundColor White
Write-Host "  3. Login with admin/admin" -ForegroundColor White
Write-Host "  4. Navigate to: Dashboards → DeepResearch" -ForegroundColor White
Write-Host ""
