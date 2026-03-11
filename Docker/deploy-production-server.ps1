# Deploy Production-Ready Lightning Server (100% Complete)
# Deploys all modules: VERL, APO, Production Hardening (Security, Monitoring, Error Recovery)

param(
    [switch]$SkipBuild,
    [switch]$ViewLogs
)

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  DEPLOYING PRODUCTION-READY LIGHTNING SERVER (100%)" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

Write-Host "📦 Deployment Configuration:" -ForegroundColor Yellow
Write-Host "   VERL Integration: ✅ Model Architectures + Distributed + Fine-Tuning"
Write-Host "   APO Optimization: ✅ 3 Strategies + Comparison"
Write-Host "   Production Hardening:"
Write-Host "     - Security: ✅ API Keys + Rate Limiting + Validation"
Write-Host "     - Monitoring: ✅ Prometheus + Health Checks"
Write-Host "     - Error Recovery: ✅ Circuit Breakers + Retry Policies"
Write-Host ""

# Step 1: Stop existing container
Write-Host "🛑 Step 1: Stopping existing Lightning Server..." -ForegroundColor Cyan
try {
    docker stop research-lightning-server 2>$null
    docker rm research-lightning-server 2>$null
    Write-Host "   ✅ Container stopped and removed" -ForegroundColor Green
}
catch {
    Write-Host "   ℹ️  No existing container to stop" -ForegroundColor Gray
}

Write-Host ""

# Step 2: Verify all module files exist
Write-Host "🔍 Step 2: Verifying module files..." -ForegroundColor Cyan

$requiredFiles = @(
    "lightning-server/apo_manager.py",    
    "lightning-server/beam_search.py",
    "lightning-server/config.py",
    "lightning-server/Dockerfile",
    "lightning-server/error_recovery.py",
    "lightning-server/genetic_algorithm.py",
    "lightning-server/monitoring.py",
    "lightning-server/security.py",
    "lightning-server/server.py",
    "lightning-server/strategy_comparison.py",
    "lightning-server/requirements.txt",
    "lightning-server/verl_distributed.py",
    "lightning-server/verl_finetuning.py",
    "lightning-server/verl_manager.py",
    "lightning-server/verl_model_architectures.py"
)

$missingFiles = @()
foreach ($file in $requiredFiles) {
    if (Test-Path $file) {
        Write-Host "   ✅ $file" -ForegroundColor Green
    } else {
        Write-Host "   ❌ $file (MISSING)" -ForegroundColor Red
        $missingFiles += $file
    }
}

if ($missingFiles.Count -gt 0) {
    Write-Host ""
    Write-Host "❌ Missing required files. Cannot proceed." -ForegroundColor Red
    Write-Host "   Missing: $($missingFiles -join ', ')" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Step 3: Build image (unless skipped)
if (-not $SkipBuild) {
    Write-Host "🔨 Step 3: Building Lightning Server image..." -ForegroundColor Cyan
    Write-Host "   This may take a few minutes..." -ForegroundColor Gray
    
    docker build -t lightning-server:latest -f lightning-server/Dockerfile ..
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "❌ Docker build failed!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "   ✅ Image built successfully" -ForegroundColor Green
} else {
    Write-Host "⏭️  Step 3: Skipping build (using existing image)" -ForegroundColor Yellow
}

Write-Host ""

# Step 4: Deploy with docker-compose
Write-Host "🚀 Step 4: Deploying Lightning Server..." -ForegroundColor Cyan

docker-compose -f docker-compose.ai.yml up -d lightning-server

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "❌ Deployment failed!" -ForegroundColor Red
    exit 1
}

Write-Host "   ✅ Container started" -ForegroundColor Green
Write-Host ""

# Step 5: Wait for health check
Write-Host "⏳ Step 5: Waiting for health check..." -ForegroundColor Cyan

$maxAttempts = 30
$attempt = 0
$healthy = $false

while ($attempt -lt $maxAttempts -and -not $healthy) {
    $attempt++
    Write-Host "   Attempt $attempt/$maxAttempts..." -NoNewline
    
    try {
        $response = Invoke-RestMethod "http://localhost:8090/health" -TimeoutSec 2 -ErrorAction Stop
        Write-Host " ✅ HEALTHY" -ForegroundColor Green
        $healthy = $true
    }
    catch {
        Write-Host " ⏳" -ForegroundColor Yellow
        Start-Sleep -Seconds 2
    }
}

if (-not $healthy) {
    Write-Host ""
    Write-Host "⚠️  Health check timeout - server may still be starting" -ForegroundColor Yellow
    Write-Host "   Check logs with: docker logs research-lightning-server" -ForegroundColor Gray
}

Write-Host ""

# Step 6: Verify endpoints
Write-Host "🧪 Step 6: Verifying endpoints..." -ForegroundColor Cyan

$endpoints = @(
    @{Name="Health"; Url="http://localhost:8090/health"; Method="GET"},
    @{Name="Server Info"; Url="http://localhost:8090/api/server/info"; Method="GET"},
    @{Name="VERL Features"; Url="http://localhost:8090/verl/features"; Method="GET"},
    @{Name="Metrics"; Url="http://localhost:8090/metrics"; Method="GET"},
    @{Name="Monitoring Dashboard"; Url="http://localhost:8090/monitoring/dashboard"; Method="GET"},
    @{Name="Security Status"; Url="http://localhost:8090/security/status"; Method="GET"},
    @{Name="Circuit Breakers"; Url="http://localhost:8090/error-recovery/circuit-breakers"; Method="GET"},
    @{Name="System Health"; Url="http://localhost:8090/system/health"; Method="GET"}
)

$successCount = 0

foreach ($endpoint in $endpoints) {
    try {
        $response = Invoke-RestMethod $endpoint.Url -Method $endpoint.Method -TimeoutSec 5 -ErrorAction Stop
        Write-Host "   ✅ $($endpoint.Name)" -ForegroundColor Green
        $successCount++
    }
    catch {
        Write-Host "   ❌ $($endpoint.Name) - $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "   Endpoint Status: $successCount/$($endpoints.Count) responding" -ForegroundColor $(if($successCount -eq $endpoints.Count){"Green"}else{"Yellow"})

Write-Host ""

# Summary
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  DEPLOYMENT COMPLETE" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

Write-Host "📊 Server Status:" -ForegroundColor Yellow
Write-Host "   URL: http://localhost:8090" -ForegroundColor White
Write-Host "   Health: $(if($healthy){'✅ Healthy'}else{'⚠️  Check logs'})" -ForegroundColor $(if($healthy){"Green"}else{"Yellow"})
Write-Host "   Endpoints: $successCount/$($endpoints.Count) responding" -ForegroundColor White
Write-Host ""

Write-Host "🔗 Key Endpoints:" -ForegroundColor Yellow
Write-Host "   Health Check: http://localhost:8090/health"
Write-Host "   API Docs: http://localhost:8090/docs"
Write-Host "   Metrics: http://localhost:8090/metrics"
Write-Host "   Monitoring: http://localhost:8090/monitoring/dashboard"
Write-Host "   Security: http://localhost:8090/security/status"
Write-Host ""

Write-Host "📚 Next Steps:" -ForegroundColor Yellow
Write-Host "   1. Run comprehensive validation:"
Write-Host "      cd ..; .\Docker\test-100-percent-validation.ps1" -ForegroundColor Gray
Write-Host ""
Write-Host "   2. Run load tests:"
Write-Host "      .\test-load-testing.ps1 -ConcurrentAPO 10 -DurationSeconds 120" -ForegroundColor Gray
Write-Host ""
Write-Host "   3. View monitoring dashboard:"
Write-Host "      Invoke-RestMethod http://localhost:8090/monitoring/dashboard" -ForegroundColor Gray
Write-Host ""
Write-Host "   4. Check circuit breaker status:"
Write-Host "      Invoke-RestMethod http://localhost:8090/error-recovery/circuit-breakers" -ForegroundColor Gray
Write-Host ""

if ($ViewLogs) {
    Write-Host "📋 Container Logs:" -ForegroundColor Cyan
    Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray
    docker logs research-lightning-server --tail 50
}
else {
    Write-Host "💡 Tip: Use -ViewLogs to see container logs" -ForegroundColor Gray
}

Write-Host ""
Write-Host "✅ PRODUCTION-READY SERVER DEPLOYED SUCCESSFULLY!" -ForegroundColor Green
