# Diagnostic and Fix Script for Multi-Model vLLM Deployment
# Checks common issues and provides fixes

Write-Host @"
╔════════════════════════════════════════════════════════════╗
║     vLLM Multi-Model Deployment Diagnostics                ║
╚════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

$issues = @()
$fixes = @()

# 1. Check Docker is running
Write-Host "`n[1/6] Checking Docker..." -ForegroundColor Yellow
try {
    docker version *>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ Docker is running" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Docker is not responding" -ForegroundColor Red
        $issues += "Docker not running"
        $fixes += "Start Docker Desktop"
    }
} catch {
    Write-Host "  ✗ Docker is not installed or not running" -ForegroundColor Red
    $issues += "Docker not available"
    $fixes += "Install Docker Desktop and start it"
}

# 2. Check NVIDIA Docker runtime
Write-Host "`n[2/6] Checking NVIDIA GPU support..." -ForegroundColor Yellow
try {
    $nvidiaCheck = docker run --rm --gpus all nvidia/cuda:12.0.0-base-ubuntu22.04 nvidia-smi 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ NVIDIA Docker runtime is working" -ForegroundColor Green
        # Extract GPU info
        $gpuCount = ($nvidiaCheck | Select-String "GPU\s+\d+:" | Measure-Object).Count
        Write-Host "    Detected GPUs: $gpuCount" -ForegroundColor Gray
    } else {
        Write-Host "  ✗ NVIDIA Docker runtime not available" -ForegroundColor Red
        $issues += "No GPU access"
        $fixes += "Install NVIDIA Container Toolkit: https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/install-guide.html"
    }
} catch {
    Write-Host "  ⚠ Could not verify GPU access" -ForegroundColor Yellow
    Write-Host "    This is required for vLLM containers" -ForegroundColor Gray
}

# 3. Check if models are downloaded
Write-Host "`n[3/6] Checking downloaded models..." -ForegroundColor Yellow

$qwen7bExists = docker run --rm -v vllm-cache:/cache alpine test -d /cache/hub/models--Qwen--Qwen2.5-7B-Instruct 2>$null; $LASTEXITCODE -eq 0
$qwen35Exists = docker run --rm -v vllm-cache:/cache alpine test -d /cache/hub/models--Qwen--Qwen3.5-9B 2>$null; $LASTEXITCODE -eq 0
$mistralExists = docker run --rm -v vllm-cache:/cache alpine test -d /cache/hub/models--mistralai--Mistral-7B-Instruct-v0.3 2>$null; $LASTEXITCODE -eq 0

if ($qwen7bExists) {
    Write-Host "  ✓ Qwen2.5-7B-Instruct downloaded" -ForegroundColor Green
} else {
    Write-Host "  ✗ Qwen2.5-7B-Instruct NOT downloaded" -ForegroundColor Red
    $issues += "Qwen2.5-7B missing"
    $fixes += "Run: .\download-models.ps1 -Qwen"
}

if ($qwen35Exists) {
    Write-Host "  ✓ Qwen3.5-9B downloaded" -ForegroundColor Green
} else {
    Write-Host "  ✗ Qwen3.5-9B NOT downloaded" -ForegroundColor Red
    $issues += "Qwen3.5-9B missing"
    $fixes += "Run: .\download-models.ps1 -Qwen35"
}

if ($mistralExists) {
    Write-Host "  ✓ Mistral-7B-Instruct-v0.3 downloaded" -ForegroundColor Green
} else {
    Write-Host "  ✗ Mistral-7B-Instruct-v0.3 NOT downloaded" -ForegroundColor Red
    $issues += "Mistral-7B missing"
    $fixes += "Run: .\download-models.ps1 -Mistral"
}

# 4. Check running containers
Write-Host "`n[4/6] Checking container status..." -ForegroundColor Yellow
$containers = docker compose -f docker-compose.multi-model.yml ps -a --format json 2>$null | ConvertFrom-Json

if ($containers) {
    foreach ($container in $containers) {
        $name = $container.Name
        $state = $container.State
        
        if ($state -eq "running") {
            Write-Host "  ✓ $name is running" -ForegroundColor Green
        } elseif ($state -eq "exited") {
            Write-Host "  ✗ $name exited (failed)" -ForegroundColor Red
            $issues += "$name failed to start"
            $fixes += "Check logs: docker logs $name"
        } else {
            Write-Host "  ⚠ $name is $state" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "  ⓘ No containers running" -ForegroundColor Gray
}

# 5. Check ports
Write-Host "`n[5/6] Checking port availability..." -ForegroundColor Yellow
$ports = @(8001, 8002, 8003, 4000, 4001)
foreach ($port in $ports) {
    $inUse = Test-NetConnection -ComputerName localhost -Port $port -WarningAction SilentlyContinue -InformationLevel Quiet 2>$null
    if ($inUse) {
        Write-Host "  ⓘ Port $port is in use" -ForegroundColor Gray
    } else {
        Write-Host "  ○ Port $port is available" -ForegroundColor Gray
    }
}

# 6. Check Docker Compose syntax
Write-Host "`n[6/6] Validating docker-compose.yml..." -ForegroundColor Yellow
$composeCheck = docker compose -f docker-compose.multi-model.yml config 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✓ docker-compose.yml syntax is valid" -ForegroundColor Green
} else {
    Write-Host "  ✗ docker-compose.yml has syntax errors" -ForegroundColor Red
    Write-Host "    $composeCheck" -ForegroundColor Gray
    $issues += "Invalid docker-compose.yml"
    $fixes += "Review syntax errors above"
}

# Summary
Write-Host "`n" -NoNewline
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "                      SUMMARY                               " -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan

if ($issues.Count -eq 0) {
    Write-Host "`n✓ All checks passed! Ready to deploy." -ForegroundColor Green
    Write-Host "`nTo start all containers:" -ForegroundColor White
    Write-Host "  cd $VllmDir" -ForegroundColor Yellow
    Write-Host "  docker compose -f docker-compose.multi-model.yml --profile all up -d" -ForegroundColor Yellow
} else {
    Write-Host "`n⚠ Found $($issues.Count) issue(s):" -ForegroundColor Yellow
    for ($i = 0; $i -lt $issues.Count; $i++) {
        Write-Host "  $($i+1). $($issues[$i])" -ForegroundColor Red
        Write-Host "     → Fix: $($fixes[$i])" -ForegroundColor Green
    }
    
    Write-Host "`n📋 Quick Fix Commands:" -ForegroundColor Cyan
    
    # Provide quick fix based on missing models
    if (-not $qwen7bExists -and -not $qwen35Exists -and -not $mistralExists) {
        Write-Host "  # Download all models (~47GB, 30-45 min)" -ForegroundColor Gray
        Write-Host "  .\download-models.ps1 -All`n" -ForegroundColor Yellow
    } elseif (-not $qwen7bExists) {
        Write-Host "  # Download Qwen2.5-7B (~15GB)" -ForegroundColor Gray
        Write-Host "  .\download-models.ps1 -Qwen`n" -ForegroundColor Yellow
    } elseif (-not $qwen35Exists) {
        Write-Host "  # Download Qwen3.5-9B (~18GB)" -ForegroundColor Gray
        Write-Host "  .\download-models.ps1 -Qwen35`n" -ForegroundColor Yellow
    } elseif (-not $mistralExists) {
        Write-Host "  # Download Mistral-7B (~14GB)" -ForegroundColor Gray
        Write-Host "  .\download-models.ps1 -Mistral`n" -ForegroundColor Yellow
    }
}

Write-Host "`n📖 Documentation:" -ForegroundColor Cyan
Write-Host "  - Quick Start: QUICK_START_MULTI_MODEL.md" -ForegroundColor Gray
Write-Host "  - Deployment: DEPLOYMENT_SCRIPTS.md" -ForegroundColor Gray
Write-Host "  - Qwen3.5 Fix: QWEN35_VALIDATION_AND_FIX.md" -ForegroundColor Gray

Write-Host ""
