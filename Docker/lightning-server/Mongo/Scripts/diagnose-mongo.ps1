# MongoDB Diagnostic Script
# Checks replica set initialization status and common issues

Write-Host "MongoDB Diagnostic Report" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════`n" -ForegroundColor Gray

# Check container status
Write-Host "[1/5] Container Status" -ForegroundColor Cyan
$containers = @("research-mongo-primary", "research-mongo-secondary-1", "research-mongo-secondary-2", "research-mongo-init")
foreach ($container in $containers) {
    $status = docker ps -a --filter "name=$container" --format "{{.Status}}"
    if ($status) {
        Write-Host "  $container : $status" -ForegroundColor Gray
    } else {
        Write-Host "  $container : NOT FOUND" -ForegroundColor Red
    }
}

# Check mongo-init-replica logs
Write-Host "`n[2/5] Replica Set Initialization Logs" -ForegroundColor Cyan
Write-Host "Checking mongo-init-replica container..." -ForegroundColor Gray
docker logs research-mongo-init 2>&1 | Select-Object -Last 20

# Check primary node status
Write-Host "`n[3/5] Primary Node Status" -ForegroundColor Cyan
Write-Host "Attempting connection to primary without auth..." -ForegroundColor Gray
docker exec research-mongo-primary mongosh --quiet --eval "db.adminCommand('ping')" 2>&1

# Try to get replica set status without auth
Write-Host "`n[4/5] Replica Set Status (no auth)" -ForegroundColor Cyan
docker exec research-mongo-primary mongosh --quiet --eval "rs.status()" 2>&1 | Select-Object -First 30

# Try with auth
Write-Host "`n[5/5] Replica Set Status (with auth)" -ForegroundColor Cyan
docker exec research-mongo-primary mongosh --quiet -u lightning -p lightningpass --authenticationDatabase admin --eval "rs.status()" 2>&1 | Select-Object -First 30

Write-Host "`n═══════════════════════════════════════════════════" -ForegroundColor Gray
Write-Host "Diagnostic complete. Review logs above." -ForegroundColor Cyan

# Provide recommendations
Write-Host "`nRecommendations:" -ForegroundColor Yellow
Write-Host "  1. If mongo-init-replica shows errors, the replica set failed to initialize" -ForegroundColor Gray
Write-Host "  2. If 'no replset config' appears, run manual initialization:" -ForegroundColor Gray
Write-Host "     docker exec research-mongo-primary mongosh --eval 'rs.initiate()'" -ForegroundColor Gray
Write-Host "  3. Check keyfile permissions: docker exec research-mongo-primary ls -l /data/keyfile" -ForegroundColor Gray
