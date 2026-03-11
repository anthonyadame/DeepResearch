# Download vLLM Models for Multi-Model Deployment
# Downloads Qwen3.5-2B and Qwen3.5-4B to Docker volume

param(
    [switch]$Parallel,
    [switch]$Qwen2B,
    [switch]$Qwen4B,
    [switch]$All,
    [switch]$Help
)

function Show-Help {
    Write-Host @"
Download vLLM Models Script
==========================

Downloads Qwen3.5 models (2B, 4B) optimized for vLLM on 24GB VRAM.

Usage:
    .\download-models.ps1                 # Download both Qwen3.5-2B and Qwen3.5-4B (default)
    .\download-models.ps1 -All            # Download both models (same as default)
    .\download-models.ps1 -Parallel       # Download models in parallel (multiple terminals)
    .\download-models.ps1 -Qwen2B         # Download only Qwen3.5-2B
    .\download-models.ps1 -Qwen4B         # Download only Qwen3.5-4B
    .\download-models.ps1 -Help           # Show this help

Examples:
    # Download both models (default)
    .\download-models.ps1

    # Download only Qwen3.5-4B
    .\download-models.ps1 -Qwen4B

Options:
    -All        Download both models (Qwen3.5-2B, Qwen3.5-4B)
    -Parallel   Download models in parallel (requires multiple terminal windows)
    -Qwen2B     Download only Qwen3.5-2B (~5GB)
    -Qwen4B     Download only Qwen3.5-4B (~9GB)
    -Help       Show this help message

Model Sizes (Full Precision for vLLM):
    Qwen3.5-2B:   ~5GB download, ~9GB VRAM
    Qwen3.5-4B:   ~9GB download, ~14GB VRAM
    Total (both): ~14GB download, ~23GB VRAM (fits on 24GB GPU)

Notes:
    - Default (no flags): Downloads Qwen3.5-2B and Qwen3.5-4B (~14GB)
    - Both models run in full precision (BF16) with vLLM
    - Optimized for 24GB VRAM (RTX 3090, RTX 4090, etc.)
    - Estimated time: 5-10 minutes per model (depends on internet speed)
    - Models are cached in Docker volume 'vllm-cache'
    - Pre-downloading avoids long startup times for vLLM containers

For larger models (9B, 27B):
    - Require GGUF quantization with llama.cpp (not included in this script)
    - See Unsloth documentation: https://unsloth.ai/docs/models/qwen3.5
    - 9B: ~6.5GB (4-bit GGUF), 27B: ~17GB (4-bit GGUF)

"@ -ForegroundColor Cyan
}

function Download-Qwen2B {
    Write-Host "`n=== Downloading Qwen3.5-2B (~5GB) ===" -ForegroundColor Cyan
    Write-Host "Model: Qwen/Qwen3.5-2B" -ForegroundColor Gray
    Write-Host "Size: ~5GB" -ForegroundColor Gray
    Write-Host "Est. Time: 5-10 minutes`n" -ForegroundColor Gray

    $startTime = Get-Date

    docker run --rm -v vllm-cache:/cache python:3.11-slim bash -c @'
pip install huggingface_hub && python -c "from huggingface_hub import snapshot_download; snapshot_download('Qwen/Qwen3.5-2B', cache_dir='/cache')"
'@

    if ($LASTEXITCODE -eq 0) {
        $elapsed = (Get-Date) - $startTime
        Write-Host "`n✓ Qwen3.5-2B download complete!" -ForegroundColor Green
        Write-Host "  Time elapsed: $($elapsed.ToString('mm\:ss'))" -ForegroundColor Gray
    } else {
        Write-Host "`n✗ Qwen3.5-2B download failed!" -ForegroundColor Red
        exit 1
    }
}

function Download-Qwen4B {
    Write-Host "`n=== Downloading Qwen3.5-4B (~9GB) ===" -ForegroundColor Cyan
    Write-Host "Model: Qwen/Qwen3.5-4B" -ForegroundColor Gray
    Write-Host "Size: ~9GB" -ForegroundColor Gray
    Write-Host "Est. Time: 5-10 minutes`n" -ForegroundColor Gray

    $startTime = Get-Date

    docker run --rm -v vllm-cache:/cache python:3.11-slim bash -c @'
pip install huggingface_hub && python -c "from huggingface_hub import snapshot_download; snapshot_download('Qwen/Qwen3.5-4B', cache_dir='/cache')"
'@

    if ($LASTEXITCODE -eq 0) {
        $elapsed = (Get-Date) - $startTime
        Write-Host "`n✓ Qwen3.5-4B download complete!" -ForegroundColor Green
        Write-Host "  Time elapsed: $($elapsed.ToString('mm\:ss'))" -ForegroundColor Gray
    } else {
        Write-Host "`n✗ Qwen3.5-4B download failed!" -ForegroundColor Red
        exit 1
    }
}


# Main script logic
if ($Help) {
    Show-Help
    exit 0
}

Write-Host @"
╔════════════════════════════════════════════════════════════╗
║         vLLM Multi-Model Download Script                  ║
║         Qwen3.5 Series: 2B, 4B (vLLM Optimized)           ║
╚════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

$totalStartTime = Get-Date

# Check Docker is running
Write-Host "`nChecking Docker..." -ForegroundColor Yellow
try {
    docker version *>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker not running"
    }
    Write-Host "✓ Docker is running" -ForegroundColor Green
} catch {
    Write-Host "✗ Docker is not running or not installed!" -ForegroundColor Red
    Write-Host "  Please start Docker Desktop and try again." -ForegroundColor Yellow
    exit 1
}

# Download logic based on parameters
if ($Parallel) {
    Write-Host "`n⚠ Parallel mode: You must run this script in MULTIPLE terminals!" -ForegroundColor Yellow
    Write-Host "  Terminal 1: .\download-models.ps1 -Qwen2B" -ForegroundColor Gray
    Write-Host "  Terminal 2: .\download-models.ps1 -Qwen4B`n" -ForegroundColor Gray
    exit 0
} elseif ($Qwen2B -and -not $Qwen4B) {
    # Download only Qwen3.5-2B
    Download-Qwen2B
} elseif ($Qwen4B -and -not $Qwen2B) {
    # Download only Qwen3.5-4B
    Download-Qwen4B
} else {
    # Download both models (default)
    Write-Host "`nDownloading both models (Qwen3.5-2B + Qwen3.5-4B)..." -ForegroundColor Yellow
    Write-Host "Total size: ~14GB (5GB + 9GB)" -ForegroundColor Gray
    Write-Host "VRAM usage: ~23GB (both models fit on 24GB GPU)" -ForegroundColor Gray
    Write-Host "Estimated total time: 10-20 minutes`n" -ForegroundColor Gray

    Download-Qwen2B
    Download-Qwen4B
}

$totalElapsed = (Get-Date) - $totalStartTime
Write-Host @"

╔════════════════════════════════════════════════════════════╗
║                 Download Complete! ✓                       ║
╚════════════════════════════════════════════════════════════╝

Total time elapsed: $($totalElapsed.ToString('hh\:mm\:ss'))
Models cached in Docker volume: vllm-cache

Hardware Requirements (24GB VRAM):
  - Qwen3.5-2B:  ~9GB VRAM  (fast inference)
  - Qwen3.5-4B:  ~14GB VRAM (balanced quality)
  - Both models: ~23GB VRAM total (fits on RTX 3090/4090)

Next steps:
  1. Start multi-model stack:
     cd Docker\LLM\vLLM
     docker-compose -f docker-compose.multi-model.yml --profile qwen35 up -d

  2. Verify deployment:
     curl http://localhost:8001/health   # Qwen3.5-2B
     curl http://localhost:8002/health   # Qwen3.5-4B
     curl http://localhost:4000/v1/models # LiteLLM (all models)

  3. Monitor GPU usage:
     nvidia-smi -l 1

  4. Test models:
     cd Docker\LLM\liteLLM\Scripts
     .\compare-qwen.ps1

Available models in this setup:
  - qwen3.5-2b:  Fast, lightweight (9GB VRAM)
  - qwen3.5-4b:  Balanced quality (14GB VRAM)

Note: For larger models (9B, 27B), use GGUF with llama.cpp:
  - See: https://unsloth.ai/docs/models/qwen3.5
  - Qwen3.5-9B:  ~6.5GB (4-bit GGUF)
  - Qwen3.5-27B: ~17GB (4-bit GGUF)

For more information, see:
  - QUICK_START_MULTI_MODEL.md
  - DEPLOYMENT_SCRIPTS.md

"@ -ForegroundColor Green
