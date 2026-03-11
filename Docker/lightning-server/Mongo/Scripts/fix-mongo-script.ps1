# Fix Line Endings in mongo-init.sh
# Converts CRLF (Windows) to LF (Unix) for bash compatibility
#
# This script can be run from any directory - it finds mongo-init.sh automatically

Write-Host "Fixing line endings in mongo-init.sh..." -ForegroundColor Cyan

# Calculate script location
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$MongoInitPath = Join-Path $ScriptDir "mongo-init.sh"

Write-Host "Target file: $MongoInitPath" -ForegroundColor Gray

if (Test-Path $MongoInitPath) {
    # Read content
    $content = Get-Content $MongoInitPath -Raw

    # Convert CRLF to LF
    $unixContent = $content -replace "`r`n", "`n"

    # Write back without BOM and with LF line endings
    [System.IO.File]::WriteAllText((Resolve-Path $MongoInitPath), $unixContent, [System.Text.UTF8Encoding]::new($false))

    Write-Host "✓ Line endings converted to Unix (LF)" -ForegroundColor Green
    Write-Host "✓ File saved as UTF-8 without BOM" -ForegroundColor Green
} else {
    Write-Host "✗ mongo-init.sh not found at: $MongoInitPath" -ForegroundColor Red
    exit 1
}

Write-Host "`nNow redeploy MongoDB:" -ForegroundColor Cyan
Write-Host "  .\reset-mongo.ps1" -ForegroundColor Gray
