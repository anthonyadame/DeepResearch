# DeepResearch Lightning Server - VERL Distributed Training Test Suite
# Tests multi-GPU training, gradient accumulation, and mixed precision

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   🧪 VERL DISTRIBUTED TRAINING TEST SUITE" -ForegroundColor Yellow
Write-Host "   Testing multi-GPU, gradient accumulation, mixed precision" -ForegroundColor White
Write-Host "═══════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$testsPassed = 0
$testsFailed = 0

# Test 1: Single GPU Configuration
Write-Host "Test 1: Single GPU configuration..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_distributed import create_single_gpu_config
config = create_single_gpu_config()
print(f"PASS: enabled={config.enabled}, world_size={config.world_size}, multiplier={config.effective_batch_size_multiplier}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*enabled=False.*world_size=1.*multiplier=1") {
        Write-Host "   ✅ PASSED: Single GPU config correct" -ForegroundColor Green
        Write-Host "      $result" -ForegroundColor Gray
        $testsPassed++
    } else {
        Write-Host "   ❌ FAILED: Unexpected output: $result" -ForegroundColor Red
        $testsFailed++
    }
}
catch {
    Write-Host "   ❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

# Test 2: Multi-GPU Configuration
Write-Host "`nTest 2: Multi-GPU configuration..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_distributed import create_multi_gpu_config
config = create_multi_gpu_config(num_gpus=4, rank=0)
print(f"PASS: enabled={config.enabled}, world_size={config.world_size}, backend={config.backend.value}, main={config.is_main_process}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*enabled=True.*world_size=4.*backend=nccl.*main=True") {
        Write-Host "   ✅ PASSED: Multi-GPU config correct" -ForegroundColor Green
        Write-Host "      $result" -ForegroundColor Gray
        $testsPassed++
    } else {
        Write-Host "   ❌ FAILED: Unexpected output: $result" -ForegroundColor Red
        $testsFailed++
    }
}
catch {
    Write-Host "   ❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

# Test 3: Gradient Accumulation
Write-Host "`nTest 3: Gradient accumulation logic..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_distributed import DistributedConfig, DistributedTrainingManager
config = DistributedConfig(gradient_accumulation_steps=4)
manager = DistributedTrainingManager(config)
results = []
for step in range(8):
    should_accum = manager.should_accumulate_gradients(step)
    results.append("A" if should_accum else "S")
pattern = "".join(results)
print(f"PASS: pattern={pattern} (A=accumulate, S=sync)")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*pattern=AAASAAAS") {
        Write-Host "   ✅ PASSED: Gradient accumulation pattern correct" -ForegroundColor Green
        Write-Host "      $result" -ForegroundColor Gray
        $testsPassed++
    } else {
        Write-Host "   ❌ FAILED: Unexpected pattern: $result" -ForegroundColor Red
        $testsFailed++
    }
}
catch {
    Write-Host "   ❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

# Test 4: Mixed Precision Configuration
Write-Host "`nTest 4: Mixed precision configuration..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_distributed import create_mixed_precision_config, PrecisionMode
fp16_config = create_mixed_precision_config(precision="fp16")
bf16_config = create_mixed_precision_config(precision="bf16")
print(f"PASS: fp16={fp16_config.precision_mode.value}, bf16={bf16_config.precision_mode.value}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*fp16=fp16.*bf16=bf16") {
        Write-Host "   ✅ PASSED: Mixed precision configs correct" -ForegroundColor Green
        Write-Host "      $result" -ForegroundColor Gray
        $testsPassed++
    } else {
        Write-Host "   ❌ FAILED: Unexpected output: $result" -ForegroundColor Red
        $testsFailed++
    }
}
catch {
    Write-Host "   ❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

# Test 5: Effective Batch Size Calculation
Write-Host "`nTest 5: Effective batch size calculation..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_distributed import calculate_effective_batch_size
# Base=8, 4 GPUs, 4 accumulation steps = 8 * 4 * 4 = 128
effective = calculate_effective_batch_size(base_batch_size=8, world_size=4, gradient_accumulation_steps=4)
print(f"PASS: effective_batch_size={effective}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*effective_batch_size=128") {
        Write-Host "   ✅ PASSED: Effective batch size calculation correct" -ForegroundColor Green
        Write-Host "      $result" -ForegroundColor Gray
        $testsPassed++
    } else {
        Write-Host "   ❌ FAILED: Unexpected output: $result" -ForegroundColor Red
        $testsFailed++
    }
}
catch {
    Write-Host "   ❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

# Test 6: Memory Savings Estimation
Write-Host "`nTest 6: Memory savings estimation..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_distributed import estimate_memory_savings, PrecisionMode
fp16_savings = estimate_memory_savings(PrecisionMode.FP16)
mixed_savings = estimate_memory_savings(PrecisionMode.MIXED)
print(f"PASS: fp16_reduction={fp16_savings['model_size_reduction']}, mixed_reduction={mixed_savings['total_reduction']}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*fp16_reduction=0.5.*mixed_reduction=0.6") {
        Write-Host "   ✅ PASSED: Memory savings estimation correct" -ForegroundColor Green
        Write-Host "      $result" -ForegroundColor Gray
        $testsPassed++
    } else {
        Write-Host "   ❌ FAILED: Unexpected output: $result" -ForegroundColor Red
        $testsFailed++
    }
}
catch {
    Write-Host "   ❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

# Test 7: Distributed Training Manager
Write-Host "`nTest 7: Distributed training manager..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_distributed import create_multi_gpu_config, DistributedTrainingManager
config = create_multi_gpu_config(num_gpus=2, rank=0)
manager = DistributedTrainingManager(config)
initialized = manager.initialize_distributed()
metrics = manager.get_metrics()
print(f"PASS: initialized={initialized}, world_size={config.world_size}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*initialized=True.*world_size=2") {
        Write-Host "   ✅ PASSED: Distributed manager initialized" -ForegroundColor Green
        Write-Host "      $result" -ForegroundColor Gray
        $testsPassed++
    } else {
        Write-Host "   ❌ FAILED: Unexpected output: $result" -ForegroundColor Red
        $testsFailed++
    }
}
catch {
    Write-Host "   ❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

# Test 8: Training Step Tracking
Write-Host "`nTest 8: Training step tracking..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_distributed import DistributedConfig, DistributedTrainingManager
config = DistributedConfig(gradient_accumulation_steps=2, world_size=2)
manager = DistributedTrainingManager(config)
manager.initialize_distributed()
# Run 4 steps with batch size 8
for step in range(4):
    manager.step(step, batch_size=8)
metrics = manager.get_metrics()
# Total samples = 4 steps * 8 batch * 2 GPUs = 64
print(f"PASS: total_steps={metrics['total_steps']}, samples={metrics['total_samples_processed']}, sync_steps={metrics['sync_steps']}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*total_steps=4.*samples=64.*sync_steps=2") {
        Write-Host "   ✅ PASSED: Step tracking correct" -ForegroundColor Green
        Write-Host "      $result" -ForegroundColor Gray
        $testsPassed++
    } else {
        Write-Host "   ❌ FAILED: Unexpected output: $result" -ForegroundColor Red
        $testsFailed++
    }
}
catch {
    Write-Host "   ❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

# Test 9: Gradient Scaler Configuration
Write-Host "`nTest 9: Gradient scaler configuration..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_distributed import create_mixed_precision_config, DistributedTrainingManager
fp16_config = create_mixed_precision_config(precision="fp16")
manager = DistributedTrainingManager(fp16_config)
scaler_config = manager.get_gradient_scaler_config()
print(f"PASS: enabled={scaler_config['enabled']}, init_scale={scaler_config['init_scale']}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*enabled=True.*init_scale=65536") {
        Write-Host "   ✅ PASSED: Gradient scaler config correct" -ForegroundColor Green
        Write-Host "      $result" -ForegroundColor Gray
        $testsPassed++
    } else {
        Write-Host "   ❌ FAILED: Unexpected output: $result" -ForegroundColor Red
        $testsFailed++
    }
}
catch {
    Write-Host "   ❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

# Test 10: Checkpoint Management
Write-Host "`nTest 10: Checkpoint management..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_distributed import create_multi_gpu_config, DistributedTrainingManager
# Test main process (rank 0)
main_config = create_multi_gpu_config(num_gpus=4, rank=0)
main_manager = DistributedTrainingManager(main_config)
main_manager.initialize_distributed()
main_checkpoint = main_manager.save_checkpoint({"epoch": 5})
# Test worker process (rank 1)
worker_config = create_multi_gpu_config(num_gpus=4, rank=1)
worker_manager = DistributedTrainingManager(worker_config)
worker_manager.initialize_distributed()
worker_checkpoint = worker_manager.save_checkpoint({"epoch": 5})
print(f"PASS: main_should_save={main_checkpoint['should_save']}, worker_should_save={worker_checkpoint['should_save']}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*main_should_save=True.*worker_should_save=False") {
        Write-Host "   ✅ PASSED: Checkpoint management correct" -ForegroundColor Green
        Write-Host "      $result" -ForegroundColor Gray
        $testsPassed++
    } else {
        Write-Host "   ❌ FAILED: Unexpected output: $result" -ForegroundColor Red
        $testsFailed++
    }
}
catch {
    Write-Host "   ❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

# Summary
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   TEST SUMMARY" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "Total Tests:    10" -ForegroundColor White
Write-Host "Passed:         " -NoNewline
Write-Host "$testsPassed " -NoNewline -ForegroundColor Green
Write-Host "($([math]::Round(($testsPassed/10)*100, 0))%)" -ForegroundColor Gray
Write-Host "Failed:         " -NoNewline
Write-Host "$testsFailed" -ForegroundColor $(if ($testsFailed -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($testsFailed -eq 0) {
    Write-Host "🎉 ALL TESTS PASSED! VERL distributed training validated!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "⚠️  SOME TESTS FAILED. Review output above." -ForegroundColor Yellow
    exit 1
}
