# Check MongoDB Container Logs
# Quick diagnostic to see why containers are failing
#
# This script can be run from any directory

Write-Host "MongoDB Container Logs Diagnostic" -ForegroundColor Cyan
Write-Host "══════════════════════════════════════════════════`n" -ForegroundColor Gray

$containers = @("research-mongo-primary", "research-mongo-secondary-1", "research-mongo-secondary-2")

foreach ($container in $containers) {
    Write-Host "[$container]" -ForegroundColor Yellow
    $status = docker ps -a --filter "name=$container" --format "{{.Status}}"
    
    if ($status) {
        Write-Host "Status: $status" -ForegroundColor Gray
        Write-Host "`nLast 30 log lines:" -ForegroundColor Cyan
        docker logs $container 2>&1 | Select-Object -Last 30
        Write-Host "`n" -ForegroundColor Gray
    } else {
        Write-Host "Container not found`n" -ForegroundColor Red
    }
}

Write-Host "══════════════════════════════════════════════════" -ForegroundColor Gray
Write-Host "If you see permission errors or script errors above," -ForegroundColor Yellow
Write-Host "the mongo-init.sh script may have Windows line endings." -ForegroundColor Yellow
Write-Host "`nTo fix, run in WSL or Git Bash:" -ForegroundColor Cyan
Write-Host "  dos2unix Docker/lightning-server/Mongo/Scripts/mongo-init.sh" -ForegroundColor Gray
Write-Host "`nOr use PowerShell to convert:" -ForegroundColor Cyan
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$MongoInitPath = Join-Path $ScriptDir "mongo-init.sh"
Write-Host "  `$content = Get-Content '$MongoInitPath' -Raw" -ForegroundColor Gray
Write-Host '  $content -replace "`r`n", "`n" | Set-Content $MongoInitPath -NoNewline' -ForegroundColor Gray
