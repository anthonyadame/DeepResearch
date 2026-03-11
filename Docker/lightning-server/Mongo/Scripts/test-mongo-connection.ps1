# MongoDB Connection Test Script
# Tests MongoDB replica set deployment and Lightning Server integration

param(
    [switch]$Verbose
)

Write-Host @"
╔═══════════════════════════════════════════════════════════╗
║         MongoDB Connection Test Suite                    ║
╚═══════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

$testsPassed = 0
$testsFailed = 0

# Test 1: Check if MongoDB containers are running
Write-Host "`n[1/7] Checking MongoDB containers..." -ForegroundColor Yellow

try {
    $mongoPrimary = docker ps --filter "name=research-mongo-primary" --format "{{.Status}}"
    $mongoSec1 = docker ps --filter "name=research-mongo-secondary-1" --format "{{.Status}}"
    $mongoSec2 = docker ps --filter "name=research-mongo-secondary-2" --format "{{.Status}}"
    
    if ($mongoPrimary -match "Up.*healthy") {
        Write-Host "  ✓ mongo-primary: Healthy" -ForegroundColor Green
        $testsPassed++
    } else {
        Write-Host "  ✗ mongo-primary: $mongoPrimary" -ForegroundColor Red
        $testsFailed++
    }
    
    if ($mongoSec1 -match "Up.*healthy") {
        Write-Host "  ✓ mongo-secondary-1: Healthy" -ForegroundColor Green
        $testsPassed++
    } else {
        Write-Host "  ✗ mongo-secondary-1: $mongoSec1" -ForegroundColor Red
        $testsFailed++
    }
    
    if ($mongoSec2 -match "Up.*healthy") {
        Write-Host "  ✓ mongo-secondary-2: Healthy" -ForegroundColor Green
        $testsPassed++
    } else {
        Write-Host "  ✗ mongo-secondary-2: $mongoSec2" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host "  ✗ Error checking container status: $_" -ForegroundColor Red
    $testsFailed += 3
}

# Test 2: Test MongoDB ping
Write-Host "`n[2/7] Testing MongoDB ping..." -ForegroundColor Yellow

try {
    $pingResult = docker exec research-mongo-primary mongosh `
        --quiet `
        --host localhost:27017 `
        -u lightning -p lightningpass `
        --authenticationDatabase admin `
        --eval "db.adminCommand('ping').ok"
    
    if ($pingResult -match "1") {
        Write-Host "  ✓ MongoDB is responding to ping" -ForegroundColor Green
        $testsPassed++
    } else {
        Write-Host "  ✗ MongoDB ping failed: $pingResult" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host "  ✗ Error pinging MongoDB: $_" -ForegroundColor Red
    $testsFailed++
}

# Test 3: Check replica set status
Write-Host "`n[3/7] Checking replica set status..." -ForegroundColor Yellow

try {
    $rsStatus = docker exec research-mongo-primary mongosh `
        --quiet `
        --host localhost:27017 `
        -u lightning -p lightningpass `
        --authenticationDatabase admin `
        --eval "JSON.stringify(rs.status())" | ConvertFrom-Json
    
    if ($rsStatus.ok -eq 1) {
        Write-Host "  ✓ Replica set is operational" -ForegroundColor Green
        Write-Host "    Set name: $($rsStatus.set)" -ForegroundColor Gray
        Write-Host "    Members: $($rsStatus.members.Count)" -ForegroundColor Gray
        
        foreach ($member in $rsStatus.members) {
            $stateStr = switch ($member.state) {
                1 { "PRIMARY" }
                2 { "SECONDARY" }
                default { "OTHER ($($member.state))" }
            }
            Write-Host "      - $($member.name): $stateStr" -ForegroundColor Gray
        }
        
        $testsPassed++
    } else {
        Write-Host "  ✗ Replica set status check failed" -ForegroundColor Red
        if ($Verbose) {
            Write-Host "    Response: $($rsStatus | ConvertTo-Json -Depth 3)" -ForegroundColor Gray
        }
        $testsFailed++
    }
} catch {
    Write-Host "  ✗ Error checking replica set: $_" -ForegroundColor Red
    $testsFailed++
}

# Test 4: Test database creation
Write-Host "`n[4/7] Testing database operations..." -ForegroundColor Yellow

try {
    # Create test database and collection
    $createResult = docker exec research-mongo-primary mongosh `
        --quiet `
        --host localhost:27017 `
        -u lightning -p lightningpass `
        --authenticationDatabase admin `
        --eval "
            use lightning;
            db.test_collection.insertOne({test: 'data', timestamp: new Date()});
            db.test_collection.findOne({test: 'data'})
        "
    
    if ($createResult -match "test.*data") {
        Write-Host "  ✓ Database read/write operations successful" -ForegroundColor Green
        $testsPassed++
    } else {
        Write-Host "  ✗ Database operations failed" -ForegroundColor Red
        if ($Verbose) {
            Write-Host "    Result: $createResult" -ForegroundColor Gray
        }
        $testsFailed++
    }
} catch {
    Write-Host "  ✗ Error testing database operations: $_" -ForegroundColor Red
    $testsFailed++
}

# Test 5: Test from Lightning Server container (if running)
Write-Host "`n[5/7] Testing connection from Lightning Server..." -ForegroundColor Yellow

$lightningRunning = docker ps --filter "name=research-lightning-server" --format "{{.Status}}"

if ($lightningRunning) {
    try {
        $connectionTest = docker exec research-lightning-server python -c @"
from pymongo import MongoClient
import os
import sys

try:
    uri = os.environ.get('MONGO_URI', 'mongodb://lightning:lightningpass@mongo-primary:27017,mongo-secondary-1:27017,mongo-secondary-2:27017/?replicaSet=rs0&authSource=admin')
    client = MongoClient(uri, serverSelectionTimeoutMS=5000)
    client.admin.command('ping')
    print('✅ Connection successful')
    sys.exit(0)
except Exception as e:
    print(f'❌ Connection failed: {e}')
    sys.exit(1)
"@
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✓ Lightning Server can connect to MongoDB" -ForegroundColor Green
            Write-Host "    $connectionTest" -ForegroundColor Gray
            $testsPassed++
        } else {
            Write-Host "  ✗ Lightning Server connection failed" -ForegroundColor Red
            Write-Host "    $connectionTest" -ForegroundColor Gray
            $testsFailed++
        }
    } catch {
        Write-Host "  ✗ Error testing from Lightning Server: $_" -ForegroundColor Red
        $testsFailed++
    }
} else {
    Write-Host "  ⓘ Lightning Server not running, skipping test" -ForegroundColor Gray
}

# Test 6: Test MongoLightningStore initialization (if Lightning Server running)
Write-Host "`n[6/7] Testing MongoLightningStore..." -ForegroundColor Yellow

if ($lightningRunning) {
    try {
        $storeTest = docker exec research-lightning-server python -c @"
import asyncio
import sys
import os

async def test():
    try:
        from agentlightning.store.mongo import MongoLightningStore
        
        uri = os.environ.get('MONGO_URI')
        store = MongoLightningStore(uri, 'lightning', 'rs0')
        await store.initialize()
        print('✅ MongoLightningStore initialized')
        
        # Test basic operations
        test_data = {'test_key': 'test_value', 'timestamp': '2026-03-07'}
        await store.set('test_lightning', test_data)
        result = await store.get('test_lightning')
        
        if result and result.get('test_key') == 'test_value':
            print('✅ Read/Write test passed')
            return 0
        else:
            print('❌ Read/Write test failed')
            return 1
    except Exception as e:
        print(f'❌ Error: {e}')
        import traceback
        traceback.print_exc()
        return 1

sys.exit(asyncio.run(test()))
"@
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✓ MongoLightningStore is functional" -ForegroundColor Green
            $storeTest.Split("`n") | ForEach-Object {
                if ($_ -match "✅") {
                    Write-Host "    $_" -ForegroundColor Gray
                }
            }
            $testsPassed++
        } else {
            Write-Host "  ✗ MongoLightningStore test failed" -ForegroundColor Red
            Write-Host "    $storeTest" -ForegroundColor Gray
            $testsFailed++
        }
    } catch {
        Write-Host "  ✗ Error testing MongoLightningStore: $_" -ForegroundColor Red
        $testsFailed++
    }
} else {
    Write-Host "  ⓘ Lightning Server not running, skipping test" -ForegroundColor Gray
}

# Test 7: Test replica set failover (optional, destructive)
Write-Host "`n[7/7] Replica set failover test..." -ForegroundColor Yellow
Write-Host "  ⓘ Skipping failover test (destructive, run manually if needed)" -ForegroundColor Gray
Write-Host "    To test failover: docker stop research-mongo-primary" -ForegroundColor Gray

# Summary
Write-Host "`n" -NoNewline
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "                      TEST SUMMARY                         " -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan

$totalTests = $testsPassed + $testsFailed
Write-Host "`nTests Passed: $testsPassed / $totalTests" -ForegroundColor Green
if ($testsFailed -gt 0) {
    Write-Host "Tests Failed: $testsFailed / $totalTests" -ForegroundColor Red
}

if ($testsFailed -eq 0) {
    Write-Host "`n✅ All tests passed! MongoDB is ready for production." -ForegroundColor Green
} else {
    Write-Host "`n⚠️ Some tests failed. Review errors above." -ForegroundColor Yellow
    Write-Host "`nTroubleshooting:" -ForegroundColor Cyan
    Write-Host "  1. Check container logs: docker logs research-mongo-primary" -ForegroundColor Gray
    Write-Host "  2. Verify network: docker network inspect deepresearch-hub" -ForegroundColor Gray
    Write-Host "  3. Check replica set init: docker logs research-mongo-init" -ForegroundColor Gray
}

Write-Host ""
exit $testsFailed
