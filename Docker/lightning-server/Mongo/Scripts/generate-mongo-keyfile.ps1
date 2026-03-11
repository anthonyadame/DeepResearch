# Generate MongoDB KeyFile for Replica Set Internal Authentication
# This script creates a secure keyFile required for replica set members to authenticate with each other

Write-Host "Generating MongoDB KeyFile..." -ForegroundColor Cyan

# Create keyfile directory if it doesn't exist
$keyfileDir = ".\mongo-keyfile"
if (-not (Test-Path $keyfileDir)) {
    New-Item -ItemType Directory -Path $keyfileDir | Out-Null
    Write-Host "✓ Created directory: $keyfileDir" -ForegroundColor Green
}

# Generate random key (768 bytes -> ~1024 bytes after base64 encoding)
# MongoDB maximum keyfile size is 1024 bytes

# Remove old keyfile if it exists (may be locked by Docker)
if (Test-Path $keyfilePath) {
    Write-Host "  Removing old keyfile..." -ForegroundColor Gray
    try {
        Remove-Item -Path $keyfilePath -Force -ErrorAction Stop
        Write-Host "  ✓ Old keyfile removed" -ForegroundColor Green
    }
    catch {
        Write-Host "  ⚠️  Could not remove old keyfile. Stop MongoDB containers first:" -ForegroundColor Yellow
        Write-Host "     docker compose -f docker-compose.mongo.yml down" -ForegroundColor Gray
        exit 1
    }
}

# Generate secure random bytes
# MongoDB maximum keyfile size is 1024 bytes
# Base64 encoding increases size by ~33%, so generate 768 bytes
$bytes = New-Object byte[] 768
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$base64Key = [Convert]::ToBase64String($bytes)

# Write to file with error handling
try {
    $base64Key | Out-File -FilePath $keyfilePath -Encoding ASCII -NoNewline -Force -ErrorAction Stop
}
catch {
    Write-Host "✗ Failed to write keyfile: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  Make sure MongoDB containers are stopped:" -ForegroundColor Yellow
    Write-Host "  docker compose -f docker-compose.mongo.yml down" -ForegroundColor Gray
    exit 1
}

Write-Host "✓ KeyFile generated: $keyfilePath" -ForegroundColor Green

# Verify file was created and get size
if (Test-Path $keyfilePath) {
    $fileSize = (Get-Item $keyfilePath).Length
    Write-Host "  Size: $fileSize bytes (MongoDB max: 1024)" -ForegroundColor Gray

    if ($fileSize -gt 1024) {
        Write-Host "  ⚠️  WARNING: KeyFile exceeds MongoDB's 1024-byte limit!" -ForegroundColor Red
        Write-Host "  This will cause authentication errors." -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "✗ KeyFile was not created successfully" -ForegroundColor Red
    exit 1
}

# Note: Docker will handle permissions (400) when mounting
Write-Host "`n✓ KeyFile ready for MongoDB replica set" -ForegroundColor Green
Write-Host "  This file will be mounted into all MongoDB containers" -ForegroundColor Gray
Write-Host "  Internal authentication between replica set members enabled" -ForegroundColor Gray

Write-Host "`n⚠️  IMPORTANT: Keep this file secure!" -ForegroundColor Yellow
Write-Host "  - Do NOT commit to Git (already in .gitignore)" -ForegroundColor Gray
Write-Host "  - Do NOT share publicly" -ForegroundColor Gray
Write-Host "  - Use same keyFile for all replica set members" -ForegroundColor Gray
