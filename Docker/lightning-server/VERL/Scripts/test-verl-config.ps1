# Test VERL Configuration
# Validates VERL configuration loading and structure

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  VERL Configuration Validation" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$testsPassed = 0
$testsFailed = 0

# Test 1: Check if VERL config file exists
Write-Host "[1/6] Checking VERL config file..." -ForegroundColor Yellow
$configExists = docker exec research-lightning-server test -f /app/verl-config.yaml 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✓ verl-config.yaml exists" -ForegroundColor Green
    $testsPassed++
} else {
    Write-Host "  ✗ verl-config.yaml not found" -ForegroundColor Red
    $testsFailed++
}

# Test 2: Validate YAML syntax
Write-Host ""
Write-Host "[2/6] Validating YAML syntax..." -ForegroundColor Yellow
$yamlTest = docker exec research-lightning-server python -c @"
import yaml
try:
    with open('/app/verl-config.yaml', 'r') as f:
        config = yaml.safe_load(f)
        if config:
            print('VALID')
        else:
            print('EMPTY')
except Exception as e:
    print(f'ERROR: {e}')
"@ 2>&1

if ($yamlTest -match "VALID") {
    Write-Host "  ✓ YAML syntax is valid" -ForegroundColor Green
    $testsPassed++
} else {
    Write-Host "  ✗ YAML syntax error: $yamlTest" -ForegroundColor Red
    $testsFailed++
}

# Test 3: Check required sections
Write-Host ""
Write-Host "[3/6] Checking required configuration sections..." -ForegroundColor Yellow
$sectionsTest = docker exec research-lightning-server python -c @"
import yaml
with open('/app/verl-config.yaml', 'r') as f:
    config = yaml.safe_load(f)
    sections = ['training', 'models', 'hardware', 'storage', 'monitoring']
    missing = [s for s in sections if s not in config]
    if missing:
        print(f'MISSING: {missing}')
    else:
        print('ALL_PRESENT')
"@ 2>&1

if ($sectionsTest -match "ALL_PRESENT") {
    Write-Host "  ✓ All required sections present" -ForegroundColor Green
    $testsPassed++
} else {
    Write-Host "  ✗ Missing sections: $sectionsTest" -ForegroundColor Red
    $testsFailed++
}

# Test 4: Test Python config loading
Write-Host ""
Write-Host "[4/6] Testing Python configuration loading..." -ForegroundColor Yellow
$configLoadTest = docker exec research-lightning-server python -c @"
from config import config
try:
    training_conf = config.verl.get_training_config()
    if 'ppo_learning_rate' in training_conf:
        print(f'SUCCESS: LR={training_conf["ppo_learning_rate"]}')
    else:
        print('FAILED: Missing parameters')
except Exception as e:
    print(f'ERROR: {e}')
"@ 2>&1

if ($configLoadTest -match "SUCCESS") {
    Write-Host "  ✓ Config.py loads VERL config successfully" -ForegroundColor Green
    Write-Host "    $configLoadTest" -ForegroundColor Gray
    $testsPassed++
} else {
    Write-Host "  ✗ Config loading failed: $configLoadTest" -ForegroundColor Red
    $testsFailed++
}

# Test 5: Test configuration sections
Write-Host ""
Write-Host "[5/6] Testing configuration section access..." -ForegroundColor Yellow
$sectionTest = docker exec research-lightning-server python -c @"
from config import config
try:
    model_conf = config.verl.get_model_config()
    hardware_conf = config.verl.get_hardware_config()
    storage_conf = config.verl.get_storage_config()
    
    sections_ok = all([
        'actor' in model_conf,
        'n_gpus_per_node' in hardware_conf,
        'checkpoints_dir' in storage_conf
    ])
    
    if sections_ok:
        print('SUCCESS: All sections accessible')
    else:
        print('FAILED: Missing section data')
except Exception as e:
    print(f'ERROR: {e}')
"@ 2>&1

if ($sectionTest -match "SUCCESS") {
    Write-Host "  ✓ All configuration sections accessible" -ForegroundColor Green
    $testsPassed++
} else {
    Write-Host "  ✗ Section access failed: $sectionTest" -ForegroundColor Red
    $testsFailed++
}

# Test 6: Display configuration summary
Write-Host ""
Write-Host "[6/6] Configuration summary..." -ForegroundColor Yellow
$summaryTest = docker exec research-lightning-server python -c @"
from config import config
import json

try:
    training = config.verl.get_training_config()
    models = config.verl.get_model_config()
    hardware = config.verl.get_hardware_config()
    
    summary = {
        'learning_rate': training.get('ppo_learning_rate', 'N/A'),
        'batch_size': training.get('train_batch_size', 'N/A'),
        'n_rollouts': training.get('n_rollouts', 'N/A'),
        'actor_model': models.get('actor', {}).get('name', 'N/A'),
        'n_gpus': hardware.get('n_gpus_per_node', 'N/A'),
        'ray_enabled': hardware.get('ray_enabled', 'N/A')
    }
    
    for key, value in summary.items():
        print(f'{key}: {value}')
        
    print('CONFIG_OK')
except Exception as e:
    print(f'ERROR: {e}')
"@ 2>&1

if ($summaryTest -match "CONFIG_OK") {
    Write-Host "  ✓ Configuration summary:" -ForegroundColor Green
    $summaryTest -split "`n" | Where-Object { $_ -notmatch "CONFIG_OK" } | ForEach-Object {
        Write-Host "    $_" -ForegroundColor Gray
    }
    $testsPassed++
} else {
    Write-Host "  ✗ Summary generation failed: $summaryTest" -ForegroundColor Red
    $testsFailed++
}

# Test Results
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  TEST SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "Tests Passed: $testsPassed / 6" -ForegroundColor $(if ($testsPassed -eq 6) { "Green" } else { "Yellow" })
Write-Host "Tests Failed: $testsFailed / 6" -ForegroundColor $(if ($testsFailed -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($testsFailed -eq 0) {
    Write-Host "✅ All configuration tests passed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Implement verl_manager.py with real VERL API" -ForegroundColor White
    Write-Host "  2. Add /verl/train endpoint to server.py" -ForegroundColor White
    Write-Host "  3. Test trainer initialization" -ForegroundColor White
} else {
    Write-Host "⚠️  Some configuration tests failed. Review errors above." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Cyan
    Write-Host "  1. Ensure verl-config.yaml is in Docker/lightning-server/" -ForegroundColor White
    Write-Host "  2. Rebuild Lightning Server: docker compose -f docker-compose.ai.yml build lightning-server" -ForegroundColor White
    Write-Host "  3. Restart container: docker compose -f docker-compose.ai.yml up -d lightning-server" -ForegroundColor White
}

Write-Host ""
