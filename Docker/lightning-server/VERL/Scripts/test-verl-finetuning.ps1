# DeepResearch Lightning Server - VERL Fine-Tuning Test Suite
# Tests LoRA, Adapters, and parameter-efficient training

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   🧪 VERL FINE-TUNING TEST SUITE" -ForegroundColor Yellow
Write-Host "   Testing LoRA, Adapters, parameter-efficient training" -ForegroundColor White
Write-Host "═══════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$testsPassed = 0
$testsFailed = 0

# Test 1: LoRA Configuration
Write-Host "Test 1: LoRA configuration..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_finetuning import create_lora_config_medium
config = create_lora_config_medium()
print(f"PASS: r={config.r}, alpha={config.lora_alpha}, scaling={config.scaling}, targets={len(config.target_modules)}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*r=8.*alpha=16.*scaling=2.0.*targets=4") {
        Write-Host "   ✅ PASSED: LoRA config correct" -ForegroundColor Green
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

# Test 2: Adapter Configuration
Write-Host "`nTest 2: Adapter configuration..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_finetuning import create_adapter_config_medium
config = create_adapter_config_medium()
params = config.estimate_params_per_adapter(768)
print(f"PASS: size={config.adapter_size}, layers={len(config.insert_after_layer)}, params_per_adapter={params}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*size=128.*layers=3.*params_per_adapter=196608") {
        Write-Host "   ✅ PASSED: Adapter config correct" -ForegroundColor Green
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

# Test 3: LoRA Layer Creation
Write-Host "`nTest 3: LoRA layer creation..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_finetuning import LoRALayer
layer = LoRALayer(in_features=768, out_features=768, r=8, lora_alpha=16)
info = layer.get_info()
params = layer.get_parameter_count()
print(f"PASS: rank={info['rank']}, scaling={info['scaling']}, params={params}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*rank=8.*scaling=2.0.*params=12288") {
        Write-Host "   ✅ PASSED: LoRA layer created" -ForegroundColor Green
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

# Test 4: Adapter Layer Creation
Write-Host "`nTest 4: Adapter layer creation..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_finetuning import AdapterLayer
layer = AdapterLayer(hidden_size=768, adapter_size=128, activation="relu")
info = layer.get_info()
params = layer.get_parameter_count()
print(f"PASS: hidden={info['hidden_size']}, adapter={info['adapter_size']}, params={params}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*hidden=768.*adapter=128.*params=197504") {
        Write-Host "   ✅ PASSED: Adapter layer created" -ForegroundColor Green
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

# Test 5: FineTuningManager - LoRA Application
Write-Host "`nTest 5: LoRA application..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_finetuning import FineTuningManager, create_lora_config_medium
model_info = {"num_parameters": 162000000}
lora_config = create_lora_config_medium()
manager = FineTuningManager(model_info, lora_config=lora_config)
lora_params = manager.apply_lora(num_layers=12, hidden_size=768)
print(f"PASS: lora_params={lora_params}, modules={len(manager.lora_layers)}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*lora_params=589824.*modules=48") {
        Write-Host "   ✅ PASSED: LoRA applied successfully" -ForegroundColor Green
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

# Test 6: FineTuningManager - Adapter Application
Write-Host "`nTest 6: Adapter application..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_finetuning import FineTuningManager, create_adapter_config_medium
model_info = {"num_parameters": 162000000}
adapter_config = create_adapter_config_medium()
manager = FineTuningManager(model_info, adapter_config=adapter_config)
adapter_params = manager.apply_adapters(num_layers=12, hidden_size=768)
print(f"PASS: adapter_params={adapter_params}, modules={len(manager.adapter_layers)}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*adapter_params=592512.*modules=3") {
        Write-Host "   ✅ PASSED: Adapters applied successfully" -ForegroundColor Green
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

# Test 7: Parameter Statistics
Write-Host "`nTest 7: Parameter statistics..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_finetuning import FineTuningManager, create_lora_config_small, create_adapter_config_small
model_info = {"num_parameters": 162000000}
lora_config = create_lora_config_small()
adapter_config = create_adapter_config_small()
manager = FineTuningManager(model_info, lora_config, adapter_config)
manager.apply_lora(num_layers=12, hidden_size=768)
manager.apply_adapters(num_layers=12, hidden_size=768)
stats = manager.get_parameter_stats()
print(f"PASS: trainable_pct={stats.trainable_percentage:.2f}, lora={stats.lora_params}, adapters={stats.adapter_params}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*trainable_pct=0\.[0-9]+.*lora=147456.*adapters=198272") {
        Write-Host "   ✅ PASSED: Parameter stats correct" -ForegroundColor Green
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

# Test 8: Trainable Percentage < 1%
Write-Host "`nTest 8: Parameter-efficient training (< 1%)..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_finetuning import FineTuningManager, create_lora_config_medium, create_adapter_config_small
model_info = {"num_parameters": 162000000}
lora_config = create_lora_config_medium()
adapter_config = create_adapter_config_small()
manager = FineTuningManager(model_info, lora_config, adapter_config)
manager.apply_lora(num_layers=12, hidden_size=768)
manager.apply_adapters(num_layers=12, hidden_size=768)
stats = manager.get_parameter_stats()
is_efficient = stats.trainable_percentage < 1.0
print(f"PASS: trainable_pct={stats.trainable_percentage:.3f}, efficient={is_efficient}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*trainable_pct=0\.[0-9]+.*efficient=True") {
        Write-Host "   ✅ PASSED: Parameter-efficient (< 1% trainable)" -ForegroundColor Green
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

# Test 9: LoRA Scaling Calculation
Write-Host "`nTest 9: LoRA scaling calculation..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_finetuning import LoRAConfig
configs = [
    LoRAConfig(r=4, lora_alpha=8),
    LoRAConfig(r=8, lora_alpha=16),
    LoRAConfig(r=16, lora_alpha=32),
]
scalings = [c.scaling for c in configs]
all_2_0 = all(abs(s - 2.0) < 0.001 for s in scalings)
print(f"PASS: scalings={scalings}, all_equal_2.0={all_2_0}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*scalings=\[2.0, 2.0, 2.0\].*all_equal_2.0=True") {
        Write-Host "   ✅ PASSED: LoRA scaling calculation correct" -ForegroundColor Green
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

# Test 10: Complete Fine-Tuning Workflow
Write-Host "`nTest 10: Complete fine-tuning workflow..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_finetuning import FineTuningManager, create_lora_config_medium, create_adapter_config_medium
# 162M parameter base model
model_info = {"num_parameters": 162000000}
lora_config = create_lora_config_medium()
adapter_config = create_adapter_config_medium()
# Create manager
manager = FineTuningManager(model_info, lora_config, adapter_config)
# Apply LoRA and adapters
lora_params = manager.apply_lora(num_layers=12, hidden_size=768)
adapter_params = manager.apply_adapters(num_layers=12, hidden_size=768)
# Freeze base model
frozen = manager.freeze_base_model()
# Get summary
summary = manager.get_summary()
trainable_pct = summary["trainable_percentage"]
total_added = lora_params + adapter_params
print(f"PASS: total_added={total_added}, trainable_pct={trainable_pct:.3f}, lora_modules={summary['lora_modules']}, adapter_modules={summary['adapter_modules']}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*total_added=1182336.*trainable_pct=0\.[0-9]+.*lora_modules=48.*adapter_modules=3") {
        Write-Host "   ✅ PASSED: Complete workflow successful" -ForegroundColor Green
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
    Write-Host "🎉 ALL TESTS PASSED! VERL fine-tuning validated!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Key Achievement: Parameter-efficient training with < 1% trainable params!" -ForegroundColor Cyan
    exit 0
} else {
    Write-Host "⚠️  SOME TESTS FAILED. Review output above." -ForegroundColor Yellow
    exit 1
}
