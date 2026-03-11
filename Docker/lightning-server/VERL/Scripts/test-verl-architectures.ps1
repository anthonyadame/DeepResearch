# DeepResearch Lightning Server - VERL Model Architectures Test Suite
# Tests advanced model configurations for RL training

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   🧪 VERL MODEL ARCHITECTURES TEST SUITE" -ForegroundColor Yellow
Write-Host "   Testing transformer configs and custom reward models" -ForegroundColor White
Write-Host "═══════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$testsPassed = 0
$testsFailed = 0

# Test 1: Validate TransformerConfig Initialization
Write-Host "Test 1: TransformerConfig initialization..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_model_architectures import TransformerConfig
config = TransformerConfig(num_layers=12, num_heads=12, hidden_size=768)
print(f"PASS: head_size={config.head_size}, vocab_size={config.vocab_size}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*head_size=64") {
        Write-Host "   ✅ PASSED: TransformerConfig initialized correctly" -ForegroundColor Green
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

# Test 2: Validate Policy Network Creation
Write-Host "`nTest 2: PolicyNetwork creation..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_model_architectures import create_medium_policy_network, format_parameter_count
policy = create_medium_policy_network()
params = policy.estimate_parameters()
summary = policy.get_architecture_summary()
print(f"PASS: params={format_parameter_count(params)}, layers={summary['num_layers']}, heads={summary['num_heads']}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*params=162.*M.*layers=12.*heads=12") {
        Write-Host "   ✅ PASSED: PolicyNetwork created successfully" -ForegroundColor Green
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

# Test 3: Validate Value Network Creation
Write-Host "`nTest 3: ValueNetwork creation..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_model_architectures import create_medium_policy_network, create_value_network, format_parameter_count
policy = create_medium_policy_network()
value = create_value_network(policy.config)
params = value.estimate_parameters()
summary = value.get_architecture_summary()
print(f"PASS: params={format_parameter_count(params)}, type={summary['type']}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*params=123.*M.*type=ValueNetwork") {
        Write-Host "   ✅ PASSED: ValueNetwork created successfully" -ForegroundColor Green
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

# Test 4: Validate Custom Reward Model Architectures
Write-Host "`nTest 4: CustomRewardModel architectures..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_model_architectures import create_transformer_reward_model, create_mlp_reward_model, format_parameter_count
transformer_reward = create_transformer_reward_model()
mlp_reward = create_mlp_reward_model()
t_params = transformer_reward.estimate_parameters()
m_params = mlp_reward.estimate_parameters()
print(f"PASS: transformer={format_parameter_count(t_params)}, mlp={format_parameter_count(m_params)}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*transformer=42.*M.*mlp=559.*K") {
        Write-Host "   ✅ PASSED: Reward models created successfully" -ForegroundColor Green
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

# Test 5: Validate Configuration Serialization
Write-Host "`nTest 5: Configuration serialization..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_model_architectures import TransformerConfig, RewardModelConfig
import json
transformer_config = TransformerConfig(num_layers=6, num_heads=8, hidden_size=512)
reward_config = RewardModelConfig(input_size=768, hidden_sizes=[512, 256])
t_dict = transformer_config.to_dict()
r_dict = reward_config.to_dict()
print(f"PASS: transformer_keys={len(t_dict)}, reward_keys={len(r_dict)}")
'@

    $result = docker exec research-lightning-server python -c $pythonCode 2>&1

    if ($result -match "PASS.*transformer_keys=12.*reward_keys=8") {
        Write-Host "   ✅ PASSED: Configuration serialization working" -ForegroundColor Green
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

# Test 6: Validate Parameter Validation
Write-Host "`nTest 6: Configuration validation..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_model_architectures import TransformerConfig
try:
    # Should fail: hidden_size not divisible by num_heads
    config = TransformerConfig(num_layers=6, num_heads=7, hidden_size=512)
    print("FAIL: Should have raised ValueError")
except ValueError as e:
    print(f"PASS: Validation caught error: {str(e)[:50]}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*Validation caught error") {
        Write-Host "   ✅ PASSED: Configuration validation working" -ForegroundColor Green
        Write-Host "      $result" -ForegroundColor Gray
        $testsPassed++
    } else {
        Write-Host "   ❌ FAILED: Validation not working: $result" -ForegroundColor Red
        $testsFailed++
    }
}
catch {
    Write-Host "   ❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

# Test 7: Validate Memory Estimation
Write-Host "`nTest 7: Memory estimation..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_model_architectures import create_small_policy_network, get_model_memory_estimate
policy = create_small_policy_network()
params = policy.estimate_parameters()
memory_fp32 = get_model_memory_estimate(params, 4)
memory_fp16 = get_model_memory_estimate(params, 2)
print(f"PASS: FP32={memory_fp32}, FP16={memory_fp16}")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*FP32=.*MB.*FP16=.*MB") {
        Write-Host "   ✅ PASSED: Memory estimation working" -ForegroundColor Green
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

# Test 8: Validate All Factory Functions
Write-Host "`nTest 8: Factory functions..." -ForegroundColor Cyan
try {
    $pythonCode = @'
from verl_model_architectures import (
    create_small_policy_network,
    create_medium_policy_network,
    create_large_policy_network,
    create_transformer_reward_model,
    create_mlp_reward_model
)
small = create_small_policy_network()
medium = create_medium_policy_network()
large = create_large_policy_network()
t_reward = create_transformer_reward_model()
m_reward = create_mlp_reward_model()
print(f"PASS: Created 5 models successfully")
'@
    
    $result = docker exec research-lightning-server python -c $pythonCode 2>&1
    
    if ($result -match "PASS.*Created 5 models") {
        Write-Host "   ✅ PASSED: All factory functions working" -ForegroundColor Green
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
Write-Host "Total Tests:    8" -ForegroundColor White
Write-Host "Passed:         " -NoNewline
Write-Host "$testsPassed " -NoNewline -ForegroundColor Green
Write-Host "($([math]::Round(($testsPassed/8)*100, 0))%)" -ForegroundColor Gray
Write-Host "Failed:         " -NoNewline
Write-Host "$testsFailed" -ForegroundColor $(if ($testsFailed -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($testsFailed -eq 0) {
    Write-Host "🎉 ALL TESTS PASSED! VERL model architectures validated!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "⚠️  SOME TESTS FAILED. Review output above." -ForegroundColor Yellow
    exit 1
}
