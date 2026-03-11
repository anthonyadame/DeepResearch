# Compare Qwen3.5-2B vs Qwen3.5-4B
# Tests the same prompt on both models for direct comparison

param(
    [string]$Prompt = "Explain quantum computing in simple terms"
)

Write-Host "=== Qwen 3.5-2B vs 3.5-4B Comparison Test ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Prompt: $Prompt" -ForegroundColor White
Write-Host ""

# Check if LiteLLM proxy and models are available
Write-Host "Checking model availability..." -ForegroundColor Yellow

try {
    $modelsResponse = Invoke-RestMethod -Uri "http://127.0.0.1:4000/v1/models" `
      -Method Get `
      -Headers @{"Authorization" = "Bearer sk-1234"} `
      -TimeoutSec 15 `
      -ErrorAction Stop

    $availableModels = $modelsResponse.data | ForEach-Object { $_.id }

    if ($availableModels -notcontains "qwen3.5-2b") {
        Write-Host "✗ Model 'qwen3.5-2b' not available in LiteLLM" -ForegroundColor Red
        Write-Host "  Available models: $($availableModels -join ', ')" -ForegroundColor Gray
        Write-Host "  Start with: docker-compose -f docker-compose.multi-model.yml --profile qwen35 up -d" -ForegroundColor Yellow
        exit 1
    }

    if ($availableModels -notcontains "qwen3.5-4b") {
        Write-Host "✗ Model 'qwen3.5-4b' not available in LiteLLM" -ForegroundColor Red
        Write-Host "  Available models: $($availableModels -join ', ')" -ForegroundColor Gray
        Write-Host "  Start with: docker-compose -f docker-compose.multi-model.yml --profile qwen35 up -d" -ForegroundColor Yellow
        exit 1
    }

    Write-Host "✓ Both models are available" -ForegroundColor Green
    Write-Host "  - qwen3.5-2b (Qwen/Qwen3.5-2B)" -ForegroundColor Gray
    Write-Host "  - qwen3.5-4b (Qwen/Qwen3.5-4B)" -ForegroundColor Gray
} catch {
    Write-Host "✗ LiteLLM proxy not responding on port 4000" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  Start with: docker-compose -f docker-compose.multi-model.yml --profile qwen35 up -d" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Test Qwen3.5-2B
Write-Host "┌─────────────────────────────────────┐" -ForegroundColor Cyan
Write-Host "│  Testing Qwen3.5-2B (2B params)    │" -ForegroundColor Cyan
Write-Host "└─────────────────────────────────────┘" -ForegroundColor Cyan
Write-Host ""

$start2b = Get-Date

$payload2b = @{
    model = "qwen3.5-2b"
    messages = @(
        @{
            role = "user"
            content = $Prompt
        }
    )
    max_tokens = 500
}

try {
    $response2b = Invoke-RestMethod -Uri "http://127.0.0.1:4000/v1/chat/completions" `
      -Method  Post `
      -ContentType "application/json" `
      -Headers @{"Authorization" = "Bearer sk-1234"} `
      -Body ($payload2b | ConvertTo-Json -Depth 10) `
      -ErrorAction Stop
} catch {
    Write-Host "✗ Failed to get response from Qwen3.5-2B" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    $response2b = $null
}

$elapsed2b = ((Get-Date) - $start2b).TotalMilliseconds

if ($response2b.choices) {
    Write-Host "Response:" -ForegroundColor Green
    Write-Host $response2b.choices[0].message.content -ForegroundColor White
    Write-Host ""
    Write-Host "Stats:" -ForegroundColor Gray
    Write-Host "  Prompt tokens: $($response2b.usage.prompt_tokens)" -ForegroundColor Gray
    Write-Host "  Completion tokens: $($response2b.usage.completion_tokens)" -ForegroundColor Gray
    Write-Host "  Total tokens: $($response2b.usage.total_tokens)" -ForegroundColor Gray
    Write-Host "  Latency: $([math]::Round($elapsed2b, 0))ms" -ForegroundColor Gray
    if ($response2b.usage.completion_tokens -gt 0 -and $elapsed2b -gt 0) {
        $tokensPerSec2b = [math]::Round($response2b.usage.completion_tokens / ($elapsed2b / 1000), 1)
        Write-Host "  Throughput: $tokensPerSec2b tokens/sec" -ForegroundColor Gray
    }
} else {
    Write-Host "✗ Failed to get response from Qwen3.5-2B" -ForegroundColor Red
    Write-Host "  Error: $($response2b.error.message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "─────────────────────────────────────────" -ForegroundColor Gray
Write-Host ""

# Test Qwen3.5-4B
Write-Host "┌─────────────────────────────────────┐" -ForegroundColor Cyan
Write-Host "│  Testing Qwen3.5-4B (4B params)    │" -ForegroundColor Cyan
Write-Host "└─────────────────────────────────────┘" -ForegroundColor Cyan
Write-Host ""

$start4b = Get-Date

$payload4b = @{
    model = "qwen3.5-4b"
    messages = @(
        @{
            role = "user"
            content = $Prompt
        }
    )
    max_tokens = 500
}

try {
    $response4b = Invoke-RestMethod -Uri "http://127.0.0.1:4000/v1/chat/completions" `
      -Method Post `
      -ContentType "application/json" `
      -Headers @{"Authorization" = "Bearer sk-1234"} `
      -Body ($payload4b | ConvertTo-Json -Depth 10) `
      -ErrorAction Stop
} catch {
    Write-Host "✗ Failed to get response from Qwen3.5-4B" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    $response4b = $null
}

$elapsed4b = ((Get-Date) - $start4b).TotalMilliseconds

if ($response4b.choices) {
    Write-Host "Response:" -ForegroundColor Green
    Write-Host $response4b.choices[0].message.content -ForegroundColor White
    Write-Host ""
    Write-Host "Stats:" -ForegroundColor Gray
    Write-Host "  Prompt tokens: $($response4b.usage.prompt_tokens)" -ForegroundColor Gray
    Write-Host "  Completion tokens: $($response4b.usage.completion_tokens)" -ForegroundColor Gray
    Write-Host "  Total tokens: $($response4b.usage.total_tokens)" -ForegroundColor Gray
    Write-Host "  Latency: $([math]::Round($elapsed4b, 0))ms" -ForegroundColor Gray
    if ($response4b.usage.completion_tokens -gt 0 -and $elapsed4b -gt 0) {
        $tokensPerSec4b = [math]::Round($response4b.usage.completion_tokens / ($elapsed4b / 1000), 1)
        Write-Host "  Throughput: $tokensPerSec4b tokens/sec" -ForegroundColor Gray
    }
} else {
    Write-Host "✗ Failed to get response from Qwen3.5-4B" -ForegroundColor Red
    Write-Host "  Error: $($response4b.error.message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "═════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "           COMPARISON SUMMARY             " -ForegroundColor Cyan
Write-Host "═════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

if ($response2b.choices -and $response4b.choices) {
    Write-Host "Model Comparison:" -ForegroundColor Yellow
    Write-Host ""
    
    # Token usage
    Write-Host "Token Usage:" -ForegroundColor White
    Write-Host "  2B: $($response2b.usage.total_tokens) tokens" -ForegroundColor Gray
    Write-Host "  4B: $($response4b.usage.total_tokens) tokens" -ForegroundColor Gray
    $tokenDiff = $response4b.usage.total_tokens - $response2b.usage.total_tokens
    if ($tokenDiff -gt 0) {
        Write-Host "  Difference: +$tokenDiff tokens (4B used more)" -ForegroundColor Gray
    } elseif ($tokenDiff -lt 0) {
        Write-Host "  Difference: $tokenDiff tokens (2B used more)" -ForegroundColor Gray
    } else {
        Write-Host "  Difference: Same token count" -ForegroundColor Gray
    }
    Write-Host ""
    
    # Latency
    Write-Host "Latency:" -ForegroundColor White
    Write-Host "  2B: $([math]::Round($elapsed2b, 0))ms" -ForegroundColor Gray
    Write-Host "  4B: $([math]::Round($elapsed4b, 0))ms" -ForegroundColor Gray
    $latencyDiff = $elapsed4b - $elapsed2b
    if ($latencyDiff -gt 0) {
        Write-Host "  Difference: +$([math]::Round($latencyDiff, 0))ms (4B slower)" -ForegroundColor Gray
    } else {
        Write-Host "  Difference: $([math]::Round($latencyDiff, 0))ms (2B slower)" -ForegroundColor Gray
    }
    Write-Host ""
    
    # Throughput
    if ($response2b.usage.completion_tokens -gt 0 -and $response4b.usage.completion_tokens -gt 0) {
        $throughput2b = [math]::Round($response2b.usage.completion_tokens / ($elapsed2b / 1000), 1)
        $throughput4b = [math]::Round($response4b.usage.completion_tokens / ($elapsed4b / 1000), 1)
        
        Write-Host "Throughput:" -ForegroundColor White
        Write-Host "  2B: $throughput2b tokens/sec" -ForegroundColor Gray
        Write-Host "  4B: $throughput4b tokens/sec" -ForegroundColor Gray
        
        $throughputDiff = $throughput2b - $throughput4b
        if ($throughput2b -gt $throughput4b) {
            $percent = [math]::Round((($throughput2b - $throughput4b) / $throughput4b) * 100, 1)
            Write-Host "  Winner: 2B is $percent% faster" -ForegroundColor Green
        } else {
            $percent = [math]::Round((($throughput4b - $throughput2b) / $throughput2b) * 100, 1)
            Write-Host "  Winner: 4B is $percent% faster" -ForegroundColor Green
        }
    }
    
    Write-Host ""
    Write-Host "Quality Assessment:" -ForegroundColor White
    Write-Host "  Review the responses above to compare quality" -ForegroundColor Gray
    Write-Host "  Consider: detail, accuracy, coherence, usefulness" -ForegroundColor Gray
    
} else {
    Write-Host "⚠ Could not generate comparison (one or both requests failed)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "═════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "To test with a different prompt:" -ForegroundColor Yellow
Write-Host '  .\compare-qwen.ps1 -Prompt "Your question here"' -ForegroundColor White
Write-Host ""
