# DeepResearch Observability - Shutdown Script
# Safely stops all observability services

Write-Host "🛑 Stopping DeepResearch Observability Stack..." -ForegroundColor Cyan
Write-Host ""

# Change to the script directory
Set-Location $PSScriptRoot

# Ask if user wants to preserve data
Write-Host "Do you want to preserve data (metrics, traces, dashboards)?" -ForegroundColor Yellow
Write-Host "  [Y] Yes - Keep data (default)" -ForegroundColor Green
Write-Host "  [N] No  - Delete all data" -ForegroundColor Red
Write-Host ""
$response = Read-Host "Choice (Y/N)"

if ($response -eq "N" -or $response -eq "n") {
    Write-Host ""
    Write-Host "⚠️  WARNING: This will delete ALL observability data!" -ForegroundColor Red
    Write-Host "   • All metrics history" -ForegroundColor Yellow
    Write-Host "   • All traces" -ForegroundColor Yellow
    Write-Host "   • Custom dashboards" -ForegroundColor Yellow
    Write-Host "   • Alert history" -ForegroundColor Yellow
    Write-Host ""
    $confirm = Read-Host "Are you sure? (type 'yes' to confirm)"

    if ($confirm -eq "yes") {
        Write-Host ""
        Write-Host "🗑️  Stopping services and removing data..." -ForegroundColor Yellow
        docker-compose -f docker-compose-monitoring.yml down -v

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Services stopped and data deleted." -ForegroundColor Green
        } else {
            Write-Host "❌ Failed to stop services." -ForegroundColor Red
        }
    } else {
        Write-Host "❌ Cancelled. Services are still running." -ForegroundColor Yellow
    }
} else {
    Write-Host ""
    Write-Host "💾 Stopping services (preserving data)..." -ForegroundColor Yellow
    docker-compose -f docker-compose-monitoring.yml down

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Services stopped. Data preserved." -ForegroundColor Green
        Write-Host ""
        Write-Host "📦 Preserved volumes:" -ForegroundColor Cyan
        Write-Host "  • prometheus-data" -ForegroundColor White
        Write-Host "  • grafana-data" -ForegroundColor White
        Write-Host "  • alertmanager-data" -ForegroundColor White
        Write-Host ""
        Write-Host "🔄 To restart with existing data:" -ForegroundColor Cyan
        Write-Host "  .\start-observability.ps1" -ForegroundColor DarkGray
        Write-Host "  or" -ForegroundColor DarkGray
        Write-Host "  docker-compose -f docker-compose-monitoring.yml up -d" -ForegroundColor DarkGray
    } else {
        Write-Host "❌ Failed to stop services." -ForegroundColor Red
    }
}

Write-Host ""
