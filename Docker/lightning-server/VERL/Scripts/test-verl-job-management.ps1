# Test VERL Job Management
# Validates job creation, subprocess management, and API endpoints

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  VERL Job Management Test" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$testsPassed = 0
$testsFailed = 0
$baseUrl = "http://localhost:8090"

# Test 1: Check server health
Write-Host "[1/8] Checking Lightning Server health..." -NoNewline
try {
    $healthResponse = Invoke-RestMethod -Uri "$baseUrl/health" -Method Get -ErrorAction Stop
    if ($healthResponse.status -eq "healthy" -or $healthResponse.status -eq "degraded") {
        Write-Host " ✓" -ForegroundColor Green
        Write-Host "  Server status: $($healthResponse.status)" -ForegroundColor Gray
        $testsPassed++
    } else {
        Write-Host " ✗" -ForegroundColor Red
        Write-Host "  Unexpected status: $($healthResponse.status)" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host " ✗" -ForegroundColor Red
    Write-Host "  Server not responding: $_" -ForegroundColor Red
    $testsFailed++
}
Write-Host ""

# Test 2: Check VERL manager initialization
Write-Host "[2/8] Checking VERL manager in container..." -NoNewline
$verlCheck = docker exec research-lightning-server python -c @"
from verl_manager import VERLTrainingManager
from config import config
try:
    manager = VERLTrainingManager(config.verl, None)
    print('SUCCESS: VERL manager initialized')
except Exception as e:
    print(f'FAILED: {e}')
"@ 2>&1

if ($verlCheck -match "SUCCESS") {
    Write-Host " ✓" -ForegroundColor Green
    Write-Host "  VERL manager can be instantiated" -ForegroundColor Gray
    $testsPassed++
} else {
    Write-Host " ✗" -ForegroundColor Red
    Write-Host "  Error: $verlCheck" -ForegroundColor Red
    $testsFailed++
}
Write-Host ""

# Test 3: Test job creation (Python directly)
Write-Host "[3/8] Testing job creation..." -NoNewline
$jobCreateTest = docker exec research-lightning-server python -c @"
import asyncio
from verl_manager import VERLTrainingManager
from config import config

async def test_create_job():
    manager = VERLTrainingManager(config.verl, None)
    
    test_request = {
        'project_name': 'test_api',
        'train_dataset': '/app/test_data.jsonl',
        'model_path': 'Qwen/Qwen2.5-0.5B-Instruct',
        'learning_rate': 1e-5,
        'batch_size': 2,
        'n_rollouts': 4,
        'num_epochs': 1
    }
    
    try:
        job_id = await manager.create_training_job(test_request)
        print(f'SUCCESS: {job_id}')
    except Exception as e:
        print(f'FAILED: {e}')

asyncio.run(test_create_job())
"@ 2>&1

if ($jobCreateTest -match "SUCCESS: verl_") {
    Write-Host " ✓" -ForegroundColor Green
    $testJobId = ($jobCreateTest -split "SUCCESS: ")[-1].Trim()
    Write-Host "  Job created: $testJobId" -ForegroundColor Gray
    $testsPassed++
} else {
    Write-Host " ✗" -ForegroundColor Red
    Write-Host "  Error: $jobCreateTest" -ForegroundColor Red
    $testsFailed++
}
Write-Host ""

# Test 4: Check Hydra config was generated
Write-Host "[4/8] Verifying Hydra config generated..." -NoNewline
if ($testJobId) {
    $configExists = docker exec research-lightning-server test -f "/app/verl_jobs/$testJobId/config.yaml"
    if ($LASTEXITCODE -eq 0) {
        Write-Host " ✓" -ForegroundColor Green
        Write-Host "  Config file exists at /app/verl_jobs/$testJobId/config.yaml" -ForegroundColor Gray
        $testsPassed++
    } else {
        Write-Host " ✗" -ForegroundColor Red
        Write-Host "  Config file not found" -ForegroundColor Red
        $testsFailed++
    }
} else {
    Write-Host " ⊘" -ForegroundColor Yellow
    Write-Host "  Skipped (no job_id from previous test)" -ForegroundColor Yellow
}
Write-Host ""

# Test 5: Test list jobs method
Write-Host "[5/8] Testing list jobs..." -NoNewline
$listJobsTest = docker exec research-lightning-server python -c @"
import asyncio
from verl_manager import VERLTrainingManager
from config import config

async def test_list_jobs():
    manager = VERLTrainingManager(config.verl, None)
    try:
        result = await manager.list_jobs(limit=5)
        if result.get('success'):
            print(f'SUCCESS: Found {result.get("count", 0)} jobs')
        else:
            print(f'FAILED: {result.get("error", "Unknown error")}')
    except Exception as e:
        print(f'FAILED: {e}')

asyncio.run(test_list_jobs())
"@ 2>&1

if ($listJobsTest -match "SUCCESS") {
    Write-Host " ✓" -ForegroundColor Green
    $jobCount = ($listJobsTest -split "Found ")[-1] -replace " jobs.*", ""
    Write-Host "  Found $jobCount job(s)" -ForegroundColor Gray
    $testsPassed++
} else {
    Write-Host " ✗" -ForegroundColor Red
    Write-Host "  Error: $listJobsTest" -ForegroundColor Red
    $testsFailed++
}
Write-Host ""

# Test 6: Test get job status
Write-Host "[6/8] Testing get job status..." -NoNewline
if ($testJobId) {
    $statusTest = docker exec research-lightning-server python -c @"
import asyncio
from verl_manager import VERLTrainingManager
from config import config

async def test_get_status():
    manager = VERLTrainingManager(config.verl, None)
    try:
        result = await manager.get_job_status('$testJobId')
        if result.get('success'):
            status = result.get('job', {}).get('status', 'unknown')
            print(f'SUCCESS: Status={status}')
        else:
            print(f'FAILED: {result.get("error", "Unknown error")}')
    except Exception as e:
        print(f'FAILED: {e}')

asyncio.run(test_get_status())
"@ 2>&1

    if ($statusTest -match "SUCCESS") {
        Write-Host " ✓" -ForegroundColor Green
        $jobStatus = ($statusTest -split "Status=")[-1].Trim()
        Write-Host "  Job status: $jobStatus" -ForegroundColor Gray
        $testsPassed++
    } else {
        Write-Host " ✗" -ForegroundColor Red
        Write-Host "  Error: $statusTest" -ForegroundColor Red
        $testsFailed++
    }
} else {
    Write-Host " ⊘" -ForegroundColor Yellow
    Write-Host "  Skipped (no job_id)" -ForegroundColor Yellow
}
Write-Host ""

# Test 7: Check API endpoint availability (if server.py updated)
Write-Host "[7/8] Checking VERL API endpoints..." -NoNewline
try {
    # Try to access VERL jobs list endpoint
    $apiResponse = Invoke-RestMethod -Uri "$baseUrl/verl/jobs?limit=5" -Method Get -ErrorAction Stop
    
    if ($apiResponse.success -eq $true -or $apiResponse.jobs) {
        Write-Host " ✓" -ForegroundColor Green
        $jobCount = if ($apiResponse.count) { $apiResponse.count } else { $apiResponse.jobs.Count }
        Write-Host "  API endpoint working, found $jobCount job(s)" -ForegroundColor Gray
        $testsPassed++
    } else {
        Write-Host " ✗" -ForegroundColor Red
        Write-Host "  Unexpected response: $($apiResponse | ConvertTo-Json -Depth 2)" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    if ($_.Exception.Message -match "503" -or $_.Exception.Message -match "VERL not enabled") {
        Write-Host " ⊘" -ForegroundColor Yellow
        Write-Host "  VERL not enabled in config (expected if verl.enabled=false)" -ForegroundColor Yellow
    } else {
        Write-Host " ✗" -ForegroundColor Red
        Write-Host "  API error: $_" -ForegroundColor Red
        $testsFailed++
    }
}
Write-Host ""

# Test 8: Verify job directories created
Write-Host "[8/8] Verifying job directory structure..." -NoNewline
if ($testJobId) {
    $dirCheck = docker exec research-lightning-server bash -c "ls -la /app/verl_jobs/$testJobId 2>&1"
    if ($LASTEXITCODE -eq 0 -and $dirCheck -match "config.yaml") {
        Write-Host " ✓" -ForegroundColor Green
        Write-Host "  Job directory created with config.yaml" -ForegroundColor Gray
        $testsPassed++
    } else {
        Write-Host " ✗" -ForegroundColor Red
        Write-Host "  Directory check failed" -ForegroundColor Red
        $testsFailed++
    }
} else {
    Write-Host " ⊘" -ForegroundColor Yellow
    Write-Host "  Skipped (no job_id)" -ForegroundColor Yellow
}
Write-Host ""

# Summary
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  TEST SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "Tests Passed: $testsPassed / 8" -ForegroundColor $(if ($testsPassed -ge 6) { "Green" } else { "Yellow" })
Write-Host "Tests Failed: $testsFailed / 8" -ForegroundColor $(if ($testsFailed -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($testsPassed -ge 6) {
    Write-Host "✅ Job management implementation working!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Copy updated files to container" -ForegroundColor Gray
    Write-Host "  2. Restart Lightning Server" -ForegroundColor Gray
    Write-Host "  3. Test full training workflow" -ForegroundColor Gray
    Write-Host "  4. Test API endpoints via HTTP" -ForegroundColor Gray
} else {
    Write-Host "⚠️  Some tests failed. Review errors above." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Cyan
    Write-Host "  1. Ensure verl_manager.py is updated in container" -ForegroundColor Gray
    Write-Host "  2. Check MongoDB is running (for job persistence)" -ForegroundColor Gray
    Write-Host "  3. Verify server.py has VERL endpoints" -ForegroundColor Gray
}
Write-Host ""

if ($testJobId) {
    Write-Host "Test job created: $testJobId" -ForegroundColor Cyan
    Write-Host "View config: docker exec research-lightning-server cat /app/verl_jobs/$testJobId/config.yaml" -ForegroundColor Gray
}
Write-Host ""
