# Test VERL Hydra Config Generation
# Validates that config generator produces valid Hydra YAML

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  VERL Hydra Config Generation Test" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$testsPassed = 0
$testsFailed = 0

# Test 1: Check Hydra template exists
Write-Host "[1/6] Checking Hydra template file..." -NoNewline
$templateCheck = docker exec research-lightning-server test -f /app/hydra-config-template.yaml
if ($LASTEXITCODE -eq 0) {
    Write-Host " ✓" -ForegroundColor Green
    Write-Host "  Template exists" -ForegroundColor Gray
    $testsPassed++
} else {
    Write-Host " ✗" -ForegroundColor Red
    Write-Host "  Template not found" -ForegroundColor Red
    $testsFailed++
}
Write-Host ""

# Test 2: Test config generator import
Write-Host "[2/6] Testing config generator import..." -NoNewline
$importTest = docker exec research-lightning-server python -c @"
try:
    from verl_manager import VERLTrainingManager
    from config import config
    manager = VERLTrainingManager(config.verl)
    print('SUCCESS')
except Exception as e:
    print(f'FAILED: {e}')
"@ 2>&1

if ($importTest -match "SUCCESS") {
    Write-Host " ✓" -ForegroundColor Green
    Write-Host "  VERLTrainingManager imported successfully" -ForegroundColor Gray
    $testsPassed++
} else {
    Write-Host " ✗" -ForegroundColor Red
    Write-Host "  Import failed: $importTest" -ForegroundColor Red
    $testsFailed++
}
Write-Host ""

# Test 3: Generate minimal config
Write-Host "[3/6] Generating minimal Hydra config..." -NoNewline
$configGenTest = docker exec research-lightning-server python -c @"
from verl_manager import VERLTrainingManager
from config import config

manager = VERLTrainingManager(config.verl)

# Minimal training request
test_request = {
    'project_name': 'test_project',
    'train_dataset': '/app/test_data.jsonl',
    'model_path': 'Qwen/Qwen2.5-0.5B-Instruct',
    'learning_rate': 1e-5,
    'batch_size': 4,
    'n_rollouts': 16,
    'num_epochs': 1
}

try:
    config_path = manager.generate_hydra_config('test_job_001', test_request)
    print(f'SUCCESS: {config_path}')
except Exception as e:
    print(f'FAILED: {e}')
"@ 2>&1

if ($configGenTest -match "SUCCESS") {
    Write-Host " ✓" -ForegroundColor Green
    $configPath = ($configGenTest -split "SUCCESS: ")[-1].Trim()
    Write-Host "  Config generated: $configPath" -ForegroundColor Gray
    $testsPassed++
} else {
    Write-Host " ✗" -ForegroundColor Red
    Write-Host "  Generation failed: $configGenTest" -ForegroundColor Red
    $testsFailed++
}
Write-Host ""

# Test 4: Validate YAML syntax
Write-Host "[4/6] Validating YAML syntax..." -NoNewline
$yamlTest = docker exec research-lightning-server python -c @"
import yaml
from pathlib import Path

config_path = Path('/app/verl_jobs/test_job_001/config.yaml')
if not config_path.exists():
    print('FAILED: Config file not found')
else:
    try:
        with open(config_path, 'r') as f:
            config = yaml.safe_load(f)
        print('SUCCESS: Valid YAML')
    except Exception as e:
        print(f'FAILED: {e}')
"@ 2>&1

if ($yamlTest -match "SUCCESS") {
    Write-Host " ✓" -ForegroundColor Green
    Write-Host "  YAML syntax is valid" -ForegroundColor Gray
    $testsPassed++
} else {
    Write-Host " ✗" -ForegroundColor Red
    Write-Host "  Invalid YAML: $yamlTest" -ForegroundColor Red
    $testsFailed++
}
Write-Host ""

# Test 5: Load config with OmegaConf (Hydra's config loader)
Write-Host "[5/6] Loading config with OmegaConf..." -NoNewline
$omegaTest = docker exec research-lightning-server python -c @"
from omegaconf import OmegaConf
from pathlib import Path

config_path = Path('/app/verl_jobs/test_job_001/config.yaml')
if not config_path.exists():
    print('FAILED: Config file not found')
else:
    try:
        cfg = OmegaConf.load(str(config_path))
        # Check key fields
        assert cfg.trainer.project_name == 'test_project'
        assert cfg.actor_rollout_ref.model.path == 'Qwen/Qwen2.5-0.5B-Instruct'
        assert cfg.actor_rollout_ref.actor.optim.lr == 1e-5
        assert cfg.data.train_batch_size == 4
        print('SUCCESS: All fields valid')
    except Exception as e:
        print(f'FAILED: {e}')
"@ 2>&1

if ($omegaTest -match "SUCCESS") {
    Write-Host " ✓" -ForegroundColor Green
    Write-Host "  OmegaConf loaded config successfully" -ForegroundColor Gray
    $testsPassed++
} else {
    Write-Host " ✗" -ForegroundColor Red
    Write-Host "  OmegaConf error: $omegaTest" -ForegroundColor Red
    $testsFailed++
}
Write-Host ""

# Test 6: Display generated config summary
Write-Host "[6/6] Generated config summary..." -NoNewline
$summaryTest = docker exec research-lightning-server python -c @"
from omegaconf import OmegaConf
from pathlib import Path

config_path = Path('/app/verl_jobs/test_job_001/config.yaml')
try:
    cfg = OmegaConf.load(str(config_path))
    print('SUCCESS')
    print(f'  Project: {cfg.trainer.project_name}')
    print(f'  Model: {cfg.actor_rollout_ref.model.path}')
    print(f'  Learning Rate: {cfg.actor_rollout_ref.actor.optim.lr}')
    print(f'  Batch Size: {cfg.data.train_batch_size}')
    print(f'  N Rollouts: {cfg.actor_rollout_ref.rollout.rollout_n}')
    print(f'  PPO Epochs: {cfg.actor_rollout_ref.actor.ppo_epochs}')
    print(f'  Clip Ratio: {cfg.actor_rollout_ref.actor.clip_ratio}')
    print(f'  KL Coef: {cfg.algorithm.kl_ctrl.kl_coef}')
except Exception as e:
    print(f'FAILED: {e}')
"@ 2>&1

if ($summaryTest -match "SUCCESS") {
    Write-Host " ✓" -ForegroundColor Green
    $summary = ($summaryTest -split "SUCCESS")[-1]
    Write-Host $summary -ForegroundColor Gray
    $testsPassed++
} else {
    Write-Host " ✗" -ForegroundColor Red
    Write-Host "  Summary failed: $summaryTest" -ForegroundColor Red
    $testsFailed++
}
Write-Host ""

# Summary
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  TEST SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "Tests Passed: $testsPassed / 6" -ForegroundColor $(if ($testsPassed -eq 6) { "Green" } else { "Yellow" })
Write-Host "Tests Failed: $testsFailed / 6" -ForegroundColor $(if ($testsFailed -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($testsPassed -eq 6) {
    Write-Host "✅ All Hydra config generation tests passed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Review generated config at: /app/verl_jobs/test_job_001/config.yaml" -ForegroundColor Gray
    Write-Host "  2. Implement job management methods" -ForegroundColor Gray
    Write-Host "  3. Add Lightning Server API endpoints" -ForegroundColor Gray
} else {
    Write-Host "⚠️  Some tests failed. Review errors above." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Cyan
    Write-Host "  1. Ensure hydra-config-template.yaml is in container" -ForegroundColor Gray
    Write-Host "  2. Check verl_manager.py for syntax errors" -ForegroundColor Gray
    Write-Host "  3. Verify Jinja2 template syntax" -ForegroundColor Gray
}
Write-Host ""
