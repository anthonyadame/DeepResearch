# Quick PyMongo Verification Script
Write-Host "Verifying PyMongo Installation..." -ForegroundColor Cyan
Write-Host ""

# 1. Check if container is running
Write-Host "[1/4] Checking Lightning Server status..." -ForegroundColor Yellow
$status = docker ps --filter name=research-lightning-server --format "{{.Status}}"
if ($status -match "Up.*healthy") {
    Write-Host "  ✓ Lightning Server is healthy" -ForegroundColor Green
} else {
    Write-Host "  ✗ Lightning Server not healthy: $status" -ForegroundColor Red
    Write-Host "  Starting container..." -ForegroundColor Yellow
    docker compose -f docker-compose.ai.yml up -d lightning-server
    Start-Sleep -Seconds 15
}

# 2. Check PyMongo version
Write-Host ""
Write-Host "[2/4] Checking PyMongo installation..." -ForegroundColor Yellow
try {
    $result = docker exec research-lightning-server python -c "import pymongo; print(pymongo.__version__)" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ PyMongo installed: v$result" -ForegroundColor Green
    } else {
        Write-Host "  ✗ PyMongo not found" -ForegroundColor Red
        Write-Host "    Error: $result" -ForegroundColor Gray
        exit 1
    }
} catch {
    Write-Host "  ✗ Failed to check PyMongo: $_" -ForegroundColor Red
    exit 1
}

# 3. Check Motor (async MongoDB driver)
Write-Host ""
Write-Host "[3/4] Checking Motor installation..." -ForegroundColor Yellow
try {
    $result = docker exec research-lightning-server python -c "import motor; print(motor.version)" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ Motor installed: v$result" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Motor not found" -ForegroundColor Red
        Write-Host "    Error: $result" -ForegroundColor Gray
    }
} catch {
    Write-Host "  ⚠️  Motor check failed: $_" -ForegroundColor Yellow
}

# 4. Test MongoDB connection
Write-Host ""
Write-Host "[4/4] Testing MongoDB connection..." -ForegroundColor Yellow
$testScript = @"
from pymongo import MongoClient
try:
    client = MongoClient('mongodb://lightning:lightningpass@mongo-primary:27017/?authSource=admin')
    client.admin.command('ping')
    print('SUCCESS')
except Exception as e:
    print(f'FAILED: {e}')
"@

$result = docker exec research-lightning-server python -c $testScript 2>&1
if ($result -match "SUCCESS") {
    Write-Host "  ✓ MongoDB connection successful" -ForegroundColor Green
} else {
    Write-Host "  ✗ MongoDB connection failed" -ForegroundColor Red
    Write-Host "    Error: $result" -ForegroundColor Gray
}

Write-Host ""
Write-Host "═══════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Verification Complete!" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════" -ForegroundColor Cyan
