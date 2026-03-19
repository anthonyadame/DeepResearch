# DeepResearch Observability - Startup Script
# This script starts the entire observability stack and provides access URLs

Write-Host "🚀 Starting DeepResearch Observability Stack..." -ForegroundColor Cyan
Write-Host ""

# Start Docker Compose
Write-Host "📦 Starting Docker containers..." -ForegroundColor Yellow
docker-compose -f docker-compose.observability.yml up -d

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ All services started successfully!" -ForegroundColor Green
    Write-Host ""

    Write-Host "⏳ Waiting for services to be ready (15 seconds)..." -ForegroundColor Yellow
    Start-Sleep -Seconds 15

    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "   DeepResearch Observability Stack - Ready! 🎉   " -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "📊 Access URLs:" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Grafana Dashboards:  " -NoNewline
    Write-Host "http://localhost:3000" -ForegroundColor Yellow
    Write-Host "    → Login: admin / admin" -ForegroundColor DarkGray
    Write-Host "    → Dashboard: Dashboards → DeepResearch → MasterWorkflow Observability" -ForegroundColor DarkGray
    Write-Host ""

    Write-Host "  Jaeger Tracing:      " -NoNewline
    Write-Host "http://localhost:16686" -ForegroundColor Yellow
    Write-Host "    → Service: DeepResearchAgent" -ForegroundColor DarkGray
    Write-Host "    → View distributed traces and spans" -ForegroundColor DarkGray
    Write-Host ""

    Write-Host "  Prometheus Metrics:  " -NoNewline
    Write-Host "http://localhost:9090" -ForegroundColor Yellow
    Write-Host "    → Query metrics with PromQL" -ForegroundColor DarkGray
    Write-Host ""

    Write-Host "  AlertManager:        " -NoNewline
    Write-Host "http://localhost:9093" -ForegroundColor Yellow
    Write-Host "    → View and manage alerts" -ForegroundColor DarkGray
    Write-Host ""

    Write-Host "═══════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "🔍 Key Metrics:" -ForegroundColor Green
    Write-Host "  • Workflow execution rate and duration" -ForegroundColor White
    Write-Host "  • Step-by-step performance (p50, p95, p99)" -ForegroundColor White
    Write-Host "  • LLM request latency and token usage" -ForegroundColor White
    Write-Host "  • Tool invocation success/failure rates" -ForegroundColor White
    Write-Host "  • State cache hit rates" -ForegroundColor White
    Write-Host "  • Error rates and types" -ForegroundColor White
    Write-Host ""

    Write-Host "💡 Next Steps:" -ForegroundColor Green
    Write-Host "  1. Open Grafana: http://localhost:3000" -ForegroundColor White
    Write-Host "  2. Run your application:" -ForegroundColor White
    Write-Host "     dotnet run --project DeepResearchAgent" -ForegroundColor DarkGray
    Write-Host "  3. Watch real-time metrics in the dashboard!" -ForegroundColor White
    Write-Host ""

    Write-Host "📚 Documentation:" -ForegroundColor Green
    Write-Host "  • Full Guide:       OBSERVABILITY.md" -ForegroundColor White
    Write-Host "  • Quick Reference:  OBSERVABILITY-QUICK-START.md" -ForegroundColor White
    Write-Host "  • Summary:          IMPLEMENTATION-SUMMARY.md" -ForegroundColor White
    Write-Host ""

    Write-Host "🛑 To stop all services:" -ForegroundColor Green
    Write-Host "  docker-compose -f docker-compose.observability.yml down" -ForegroundColor DarkGray
    Write-Host ""

    # Optional: Open Grafana in browser
    Write-Host "Press any key to open Grafana in your browser, or Ctrl+C to skip..." -ForegroundColor Cyan
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    Start-Process "http://localhost:3000"

} else {
    Write-Host "❌ Failed to start services. Check Docker and try again." -ForegroundColor Red
    Write-Host ""
    Write-Host "Common issues:" -ForegroundColor Yellow
    Write-Host "  • Docker Desktop not running" -ForegroundColor White
    Write-Host "  • Ports already in use (3000, 9090, 16686)" -ForegroundColor White
    Write-Host ""
    Write-Host "To view logs:" -ForegroundColor Yellow
    Write-Host "  docker-compose -f docker-compose.observability.yml logs" -ForegroundColor DarkGray
}
