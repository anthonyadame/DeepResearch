# Quick Fix and Redeploy Script
# Fixes Qwen3.5 compatibility issue by switching to Qwen2.5-7B
#
# This script can be run from any directory - it calculates paths automatically

# Calculate paths relative to script location
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$VllmDir = Split-Path -Parent $ScriptDir  # Parent of Scripts/ = Docker/LLM/vLLM/
$ComposeFile = Join-Path $VllmDir "docker-compose.multi-model.yml"
$DownloadScript = Join-Path $ScriptDir "download-models.ps1"

Write-Host "=== vLLM Multi-Model Quick Fix and Redeploy ===" -ForegroundColor Cyan
Write-Host "Working directory: $VllmDir" -ForegroundColor Gray
Write-Host ""

# Step 1: Stop existing containers
Write-Host "Step 1: Stopping existing containers..." -ForegroundColor Yellow
Push-Location $VllmDir
docker-compose -f $ComposeFile down
Pop-Location

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Containers stopped" -ForegroundColor Green
} else {
    Write-Host "⚠ No containers were running" -ForegroundColor Yellow
}

Write-Host ""

# Step 2: Pull latest vLLM image
Write-Host "Step 2: Pulling latest vLLM image..." -ForegroundColor Yellow
docker pull vllm/vllm-openai:latest

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ vLLM image updated" -ForegroundColor Green
} else {
    Write-Host "✗ Failed to pull image" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Step 3: Check if models need to be downloaded
Write-Host "Step 3: Checking model cache..." -ForegroundColor Yellow
$cacheExists = docker volume inspect vllm-cache 2>$null

if ($cacheExists) {
    Write-Host "⚠ Model cache exists" -ForegroundColor Yellow
    Write-Host "  The cache may contain Qwen3.5-9B (incompatible)" -ForegroundColor Yellow
    Write-Host ""
    $response = Read-Host "Delete cache and re-download Qwen2.5-7B? (y/N)"
    
    if ($response -eq 'y' -or $response -eq 'Y') {
        Write-Host "  Removing old cache..." -ForegroundColor Gray
        docker volume rm vllm-cache
        docker volume create vllm-cache
        
        Write-Host "  Downloading Qwen2.5-7B-Instruct (~15GB)..." -ForegroundColor Cyan
        Write-Host "  This will take 10-15 minutes..." -ForegroundColor Gray
        Write-Host ""
        
        .\download-models.ps1 -Qwen
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Qwen2.5-7B downloaded" -ForegroundColor Green
        } else {
            Write-Host "✗ Download failed" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "  Using existing cache (may cause issues if Qwen3.5 is cached)" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Cache doesn't exist, creating..." -ForegroundColor Gray
    docker volume create vllm-cache
    
    Write-Host "  Downloading models..." -ForegroundColor Cyan
    .\download-models.ps1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Models downloaded" -ForegroundColor Green
    } else {
        Write-Host "✗ Download failed" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""

# Step 4: Start stack
Write-Host "Step 4: Starting multi-model stack..." -ForegroundColor Yellow
Push-Location $VllmDir
docker-compose -f $ComposeFile up -d
Pop-Location

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Stack started" -ForegroundColor Green
} else {
    Write-Host "✗ Failed to start stack" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Step 5: Monitor startup
Write-Host "Step 5: Waiting for models to load..." -ForegroundColor Yellow
Write-Host "  This will take 2-3 minutes..." -ForegroundColor Gray
Write-Host ""

Write-Host "  Qwen2.5-7B loading status:" -ForegroundColor Cyan
for ($i = 1; $i -le 30; $i++) {
    Start-Sleep -Seconds 5
    $qwenHealth = docker exec lightning-vllm-qwen curl -s http://localhost:8000/health 2>$null
    
    if ($qwenHealth -match "ok") {
        Write-Host "  ✓ Qwen2.5-7B is ready! ($i x 5s = $($i*5)s)" -ForegroundColor Green
        break
    } else {
        Write-Host "  ⏳ Still loading... ($($i*5)s elapsed)" -ForegroundColor Gray
    }
    
    if ($i -eq 30) {
        Write-Host "  ✗ Qwen2.5-7B failed to start after 150s" -ForegroundColor Red
        Write-Host "  Check logs: docker logs lightning-vllm-qwen" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "  Mistral-7B loading status:" -ForegroundColor Cyan
for ($i = 1; $i -le 24; $i++) {
    Start-Sleep -Seconds 5
    $mistralHealth = docker exec lightning-vllm-mistral curl -s http://localhost:8000/health 2>$null
    
    if ($mistralHealth -match "ok") {
        Write-Host "  ✓ Mistral-7B is ready! ($i x 5s = $($i*5)s)" -ForegroundColor Green
        break
    } else {
        Write-Host "  ⏳ Still loading... ($($i*5)s elapsed)" -ForegroundColor Gray
    }
    
    if ($i -eq 24) {
        Write-Host "  ✗ Mistral-7B failed to start after 120s" -ForegroundColor Red
        Write-Host "  Check logs: docker logs lightning-vllm-mistral" -ForegroundColor Yellow
    }
}

Write-Host ""

# Step 6: Verify deployment
Write-Host "Step 6: Verifying deployment..." -ForegroundColor Yellow

Write-Host "  Testing Qwen2.5-7B..." -ForegroundColor Gray
$qwenTest = curl -s http://localhost:8001/health 2>$null
if ($qwenTest -match "ok") {
    Write-Host "  ✓ Qwen2.5-7B endpoint working" -ForegroundColor Green
} else {
    Write-Host "  ✗ Qwen2.5-7B endpoint failed" -ForegroundColor Red
}

Write-Host "  Testing Mistral-7B..." -ForegroundColor Gray
$mistralTest = curl -s http://localhost:8002/health 2>$null
if ($mistralTest -match "ok") {
    Write-Host "  ✓ Mistral-7B endpoint working" -ForegroundColor Green
} else {
    Write-Host "  ✗ Mistral-7B endpoint failed" -ForegroundColor Red
}

Write-Host "  Testing LiteLLM proxy..." -ForegroundColor Gray
$litellmTest = curl -s http://localhost:4000/health -H "Authorization: Bearer sk-1234" 2>$null
if ($litellmTest -match "healthy") {
    Write-Host "  ✓ LiteLLM proxy working" -ForegroundColor Green
} else {
    Write-Host "  ✗ LiteLLM proxy failed" -ForegroundColor Red
}

Write-Host ""

# Summary
Write-Host "=== Deployment Summary ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Model Configuration:" -ForegroundColor White
Write-Host "  Qwen2.5-7B-Instruct → Port 8001 (GPU 0)" -ForegroundColor Gray
Write-Host "  Mistral-7B-Instruct → Port 8002 (GPU 1)" -ForegroundColor Gray
Write-Host "  LiteLLM Proxy      → Port 4000" -ForegroundColor Gray
Write-Host ""
Write-Host "Health Endpoints:" -ForegroundColor White
Write-Host "  curl http://localhost:8001/health" -ForegroundColor Gray
Write-Host "  curl http://localhost:8002/health" -ForegroundColor Gray
Write-Host '  curl http://localhost:4000/health -H "Authorization: Bearer sk-1234"' -ForegroundColor Gray
Write-Host ""
Write-Host "Test Inference:" -ForegroundColor White
Write-Host '  curl http://localhost:4000/v1/chat/completions `' -ForegroundColor Gray
Write-Host '    -H "Content-Type: application/json" `' -ForegroundColor Gray
Write-Host '    -H "Authorization: Bearer sk-1234" `' -ForegroundColor Gray
Write-Host '    -d ''{"model":"qwen2.5-7b","messages":[{"role":"user","content":"Hello!"}]}''' -ForegroundColor Gray
Write-Host ""
Write-Host "Monitor Containers:" -ForegroundColor White
Write-Host "  docker-compose -f docker-compose.multi-model.yml logs -f" -ForegroundColor Gray
Write-Host ""
Write-Host "Check GPU Usage:" -ForegroundColor White
Write-Host "  nvidia-smi -l 1" -ForegroundColor Gray
Write-Host ""
Write-Host "Documentation:" -ForegroundColor White
Write-Host "  VLLM_MODEL_COMPATIBILITY_FIX.md - Explains the Qwen3.5 → Qwen2.5 change" -ForegroundColor Gray
Write-Host "  QUICK_START_MULTI_MODEL.md      - Full deployment guide" -ForegroundColor Gray
Write-Host "  LITELLM_AUTH_FIX.md             - LiteLLM authentication details" -ForegroundColor Gray
Write-Host ""
Write-Host "✅ Deployment complete!" -ForegroundColor Green
Write-Host ""
