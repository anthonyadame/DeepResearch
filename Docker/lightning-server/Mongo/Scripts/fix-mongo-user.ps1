# MongoDB User Creation Fix
# Manually creates the lightning user if init script failed

Write-Host "MongoDB User Creation Diagnostic & Fix" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════`n" -ForegroundColor Gray

# Step 1: Check if we can connect without auth
Write-Host "[1/3] Checking localhost exception access..." -ForegroundColor Cyan
$noAuthTest = docker exec research-mongo-primary mongosh --quiet --eval "db.adminCommand('ping')" 2>&1

if ($noAuthTest -match "ok.*1") {
    Write-Host "  ✓ Can connect via localhost exception" -ForegroundColor Green
} else {
    Write-Host "  ✗ Cannot connect even without auth" -ForegroundColor Red
    Write-Host "  Full output: $noAuthTest" -ForegroundColor Gray
}

# Step 2: Check if lightning user exists
Write-Host "`n[2/3] Checking if 'lightning' user exists..." -ForegroundColor Cyan
$userCheck = docker exec research-mongo-primary mongosh admin --quiet --eval "db.getUsers()" 2>&1

if ($userCheck -match "lightning") {
    Write-Host "  ✓ User 'lightning' exists" -ForegroundColor Green
    Write-Host "`n  Testing authentication..." -ForegroundColor Gray
    $authTest = docker exec research-mongo-primary mongosh --quiet -u lightning -p lightningpass --authenticationDatabase admin --eval "db.runCommand({connectionStatus: 1})" 2>&1
    
    if ($authTest -match "authenticatedUsers") {
        Write-Host "  ✓ Authentication successful!" -ForegroundColor Green
        Write-Host "`n  The user exists and authentication works." -ForegroundColor Yellow
        Write-Host "  The test script might be using wrong credentials." -ForegroundColor Yellow
    } else {
        Write-Host "  ✗ User exists but authentication failed" -ForegroundColor Red
        Write-Host "  Output: $authTest" -ForegroundColor Gray
    }
} else {
    Write-Host "  ✗ User 'lightning' does not exist" -ForegroundColor Red
    Write-Host "`n[3/3] Creating 'lightning' user..." -ForegroundColor Cyan
    
    $createUser = docker exec research-mongo-primary mongosh admin --quiet --eval @'
db.createUser({
    user: 'lightning',
    pwd: 'lightningpass',
    roles: [{ role: 'root', db: 'admin' }]
});
print('User created successfully');
'@ 2>&1

    if ($createUser -match "successfully" -or $createUser -match "already exists") {
        Write-Host "  ✓ User created!" -ForegroundColor Green
        
        # Verify
        Write-Host "`n  Verifying authentication..." -ForegroundColor Gray
        $verify = docker exec research-mongo-primary mongosh --quiet -u lightning -p lightningpass --authenticationDatabase admin --eval "print('Authentication OK')" 2>&1
        
        if ($verify -match "Authentication OK") {
            Write-Host "  ✓ Authentication verified!" -ForegroundColor Green
        } else {
            Write-Host "  ✗ Authentication still failing: $verify" -ForegroundColor Red
        }
    } else {
        Write-Host "  ✗ User creation failed" -ForegroundColor Red
        Write-Host "  Output: $createUser" -ForegroundColor Gray
    }
}

# Step 3: Check replica set initialization
Write-Host "`n[3/3] Checking replica set status..." -ForegroundColor Cyan
$rsStatus = docker exec research-mongo-primary mongosh --quiet -u lightning -p lightningpass --authenticationDatabase admin --eval "rs.status()" 2>&1

if ($rsStatus -match '"set" : "rs0"') {
    Write-Host "  ✓ Replica set initialized" -ForegroundColor Green
} elseif ($rsStatus -match "NotYetInitialized") {
    Write-Host "  ⚠️  Replica set not initialized yet" -ForegroundColor Yellow
    Write-Host "`n  Initializing replica set..." -ForegroundColor Cyan
    
    docker exec research-mongo-primary mongosh --quiet -u lightning -p lightningpass --authenticationDatabase admin --eval @'
rs.initiate({
    _id: "rs0",
    members: [
        { _id: 0, host: "mongo-primary:27017", priority: 2 },
        { _id: 1, host: "mongo-secondary-1:27017", priority: 1 },
        { _id: 2, host: "mongo-secondary-2:27017", priority: 1 }
    ]
});
'@
    
    Write-Host "  ✓ Replica set initialization command sent" -ForegroundColor Green
    Write-Host "  Waiting 20 seconds for stabilization..." -ForegroundColor Gray
    Start-Sleep -Seconds 20
} else {
    Write-Host "  ⚠️  Unexpected status: $($rsStatus | Select-Object -First 100)" -ForegroundColor Yellow
}

Write-Host "`n═══════════════════════════════════════════════════" -ForegroundColor Gray
Write-Host "Next step: Run test-mongo-connection.ps1 to verify" -ForegroundColor Cyan
