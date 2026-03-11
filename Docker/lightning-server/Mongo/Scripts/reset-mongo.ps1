# MongoDB Complete Reset Script
# This script completely removes MongoDB data and reinitializes with proper authentication
#
# This script can be run from any directory - it calculates paths automatically

# Calculate paths relative to script location
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$DockerDir = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $ScriptDir))
$ComposeFile = Join-Path $DockerDir "docker-compose.mongo.yml"
$KeyfileDir = Join-Path $DockerDir "mongo-keyfile"
$KeyfilePath = Join-Path $KeyfileDir "mongodb-keyfile"
$GenerateKeyfileScript = Join-Path $ScriptDir "generate-mongo-keyfile.ps1"

Write-Host "MongoDB Complete Reset" -ForegroundColor Cyan
Write-Host "This will DELETE all MongoDB data and reinitialize from scratch`n" -ForegroundColor Yellow
Write-Host "Working from: $DockerDir" -ForegroundColor Gray

$confirmation = Read-Host "Type 'YES' to proceed"
if ($confirmation -ne 'YES') {
    Write-Host "Aborted." -ForegroundColor Gray
    exit 0
}

Write-Host "`n[1/5] Stopping MongoDB containers..." -ForegroundColor Cyan
Push-Location $DockerDir
docker compose -f $ComposeFile down
Pop-Location

Write-Host "`n[2/5] Removing MongoDB volumes..." -ForegroundColor Cyan
$volumes = @(
    "deepresearch-mongo_mongo-primary",
    "deepresearch-mongo_mongo-primary-config",
    "deepresearch-mongo_mongo-secondary-1",
    "deepresearch-mongo_mongo-secondary-1-config",
    "deepresearch-mongo_mongo-secondary-2",
    "deepresearch-mongo_mongo-secondary-2-config"
)

foreach ($vol in $volumes) {
    try {
        docker volume rm $vol 2>$null
        Write-Host "  ✓ Removed $vol" -ForegroundColor Green
    }
    catch {
        Write-Host "  ⓘ Volume $vol does not exist or already removed" -ForegroundColor Gray
    }
}

Write-Host "`n[3/5] Verifying keyfile exists..." -ForegroundColor Cyan
if (-not (Test-Path ".\mongo-keyfile\mongodb-keyfile")) {
    Write-Host "  Keyfile not found. Generating..." -ForegroundColor Yellow
    .\generate-mongo-keyfile.ps1
}

$fileSize = (Get-Item ".\mongo-keyfile\mongodb-keyfile").Length
Write-Host "  ✓ KeyFile exists ($fileSize bytes)" -ForegroundColor Green

Write-Host "`n[4/5] Starting MongoDB containers..." -ForegroundColor Cyan
docker compose -f docker-compose.mongo.yml up -d

Write-Host "`n[5/5] Waiting for replica set initialization..." -ForegroundColor Cyan
Write-Host "  Checking mongo-init-replica container..." -ForegroundColor Gray
Start-Sleep -Seconds 30

# Check init container logs
Write-Host "`n  Init container logs:" -ForegroundColor Gray
docker logs research-mongo-init 2>&1 | Select-Object -Last 15

# Wait a bit more for stabilization
Write-Host "`n  Waiting additional 30 seconds for stabilization..." -ForegroundColor Gray
Start-Sleep -Seconds 30

Write-Host "`n✓ MongoDB reset complete!" -ForegroundColor Green
Write-Host "`nReplica Set Status:" -ForegroundColor Cyan
docker exec research-mongo-primary mongosh --quiet --eval "rs.status()" -u lightning -p lightningpass --authenticationDatabase admin 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✓ Authentication working!" -ForegroundColor Green
    Write-Host "  User: lightning / Password: lightningpass" -ForegroundColor Gray
} else {
    Write-Host "`n⚠️  Authentication may need additional time. Run test-mongo-connection.ps1 in 30 seconds" -ForegroundColor Yellow
}

Write-Host "`nNext Steps:" -ForegroundColor Cyan
Write-Host "  1. Run: .\test-mongo-connection.ps1" -ForegroundColor Gray
Write-Host "  2. Deploy Lightning Server: docker compose -f docker-compose.ai.yml up -d --build" -ForegroundColor Gray
Write-Host "  3. Run full tests: .\end-to-end-test.ps1" -ForegroundColor Gray
