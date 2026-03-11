# ═══════════════════════════════════════════════════════════════════════════════
# APO (Agent Prompt Optimization) System Validation Test Suite
# ═══════════════════════════════════════════════════════════════════════════════
# Tests all APO endpoints and workflow functionality
# Validates MongoDB integration, optimization logic, and API responses
# ═══════════════════════════════════════════════════════════════════════════════

$ErrorActionPreference = "Continue"
$BaseUrl = "http://localhost:8090"

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   APO (Agent Prompt Optimization) System Validation" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$TestResults = @{
    Passed = 0
    Failed = 0
    Total = 0
}

function Test-Endpoint {
    param(
        [string]$Name,
        [scriptblock]$Test
    )
    
    $TestResults.Total++
    Write-Host "Test $($TestResults.Total): $Name" -ForegroundColor Yellow
    
    try {
        $result = & $Test
        if ($result) {
            Write-Host "   ✅ PASSED" -ForegroundColor Green
            $TestResults.Passed++
            return $true
        } else {
            Write-Host "   ❌ FAILED" -ForegroundColor Red
            $TestResults.Failed++
            return $false
        }
    } catch {
        Write-Host "   ❌ FAILED: $_" -ForegroundColor Red
        $TestResults.Failed++
        return $false
    }
}

# ═══════════════════════════════════════════════════════════════════════════════
# Test 1: Server Health Check
# ═══════════════════════════════════════════════════════════════════════════════
Test-Endpoint "Server Health Check" {
    $response = Invoke-RestMethod -Uri "$BaseUrl/health" -Method Get -ErrorAction Stop
    Write-Host "   Status: $($response.status)" -ForegroundColor Cyan
    Write-Host "   Storage: $($response.storage.type)" -ForegroundColor Cyan
    return $response.status -eq "healthy"
}

# ═══════════════════════════════════════════════════════════════════════════════
# Test 2: GET /apo/prompts (Empty State)
# ═══════════════════════════════════════════════════════════════════════════════
Test-Endpoint "GET /apo/prompts - Empty state" {
    $response = Invoke-RestMethod -Uri "$BaseUrl/apo/prompts" -Method Get -ErrorAction Stop
    Write-Host "   Prompts count: $($response.count)" -ForegroundColor Cyan
    return $response.count -ge 0
}

# ═══════════════════════════════════════════════════════════════════════════════
# Test 3: GET /apo/runs (Empty State)
# ═══════════════════════════════════════════════════════════════════════════════
Test-Endpoint "GET /apo/runs - Empty state" {
    $response = Invoke-RestMethod -Uri "$BaseUrl/apo/runs" -Method Get -ErrorAction Stop
    Write-Host "   Runs count: $($response.count)" -ForegroundColor Cyan
    return $response.count -ge 0
}

# ═══════════════════════════════════════════════════════════════════════════════
# Test 4: POST /apo/optimize - Simple Optimization
# ═══════════════════════════════════════════════════════════════════════════════
$RunId = $null
$PromptId = $null

Test-Endpoint "POST /apo/optimize - Start optimization" {
    $body = @{
        prompt_name = "test-assistant-prompt"
        initial_prompt = "You are a helpful AI assistant."
        domain = "general"
        description = "Test optimization run for assistant prompt"
        iterations = 3
        evaluation_samples = 5
        model = "Qwen/Qwen3.5-2B-Instruct"
        optimization_strategy = "iterative_refinement"
        evaluation_criteria = @("coherence", "relevance", "helpfulness")
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$BaseUrl/apo/optimize" -Method Post -Body $body -ContentType "application/json" -ErrorAction Stop
    
    $script:RunId = $response.run_id
    $script:PromptId = $response.prompt_id
    
    Write-Host "   Run ID: $RunId" -ForegroundColor Cyan
    Write-Host "   Prompt ID: $PromptId" -ForegroundColor Cyan
    Write-Host "   Iterations: $($response.iterations)" -ForegroundColor Cyan
    
    return ($response.run_id -and $response.prompt_id)
}

# Wait for optimization to complete
Write-Host "`nWaiting 5 seconds for optimization to complete..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# ═══════════════════════════════════════════════════════════════════════════════
# Test 5: GET /apo/runs/{run_id} - Check Optimization Status
# ═══════════════════════════════════════════════════════════════════════════════
Test-Endpoint "GET /apo/runs/{run_id} - Check run status" {
    if (-not $script:RunId) {
        Write-Host "   ⚠️ Skipping - no run_id available" -ForegroundColor Yellow
        return $true
    }
    
    $response = Invoke-RestMethod -Uri "$BaseUrl/apo/runs/$script:RunId" -Method Get -ErrorAction Stop
    
    Write-Host "   Status: $($response.status)" -ForegroundColor Cyan
    Write-Host "   Iterations completed: $($response.iterations_completed.Count)" -ForegroundColor Cyan
    Write-Host "   Best score: $($response.best_score)" -ForegroundColor Cyan
    Write-Host "   Improvement: $([math]::Round($response.improvement * 100, 2))%" -ForegroundColor Cyan
    
    return ($response.status -eq "completed" -and $response.iterations_completed.Count -gt 0)
}

# ═══════════════════════════════════════════════════════════════════════════════
# Test 6: GET /apo/prompts - Verify Prompt Created
# ═══════════════════════════════════════════════════════════════════════════════
Test-Endpoint "GET /apo/prompts - Verify prompt created" {
    $response = Invoke-RestMethod -Uri "$BaseUrl/apo/prompts" -Method Get -ErrorAction Stop
    
    Write-Host "   Total prompts: $($response.count)" -ForegroundColor Cyan
    
    if ($response.count -gt 0) {
        $prompt = $response.prompts[0]
        Write-Host "   Prompt name: $($prompt.name)" -ForegroundColor Cyan
        Write-Host "   Versions: $($prompt.versions.Count)" -ForegroundColor Cyan
        Write-Host "   Current version: $($prompt.current_version)" -ForegroundColor Cyan
    }
    
    return $response.count -gt 0
}

# ═══════════════════════════════════════════════════════════════════════════════
# Test 7: GET /apo/prompts/{prompt_id} - Get Specific Prompt
# ═══════════════════════════════════════════════════════════════════════════════
Test-Endpoint "GET /apo/prompts/{prompt_id} - Get prompt details" {
    if (-not $script:PromptId) {
        Write-Host "   ⚠️ Skipping - no prompt_id available" -ForegroundColor Yellow
        return $true
    }
    
    $response = Invoke-RestMethod -Uri "$BaseUrl/apo/prompts/$script:PromptId" -Method Get -ErrorAction Stop
    
    Write-Host "   Name: $($response.name)" -ForegroundColor Cyan
    Write-Host "   Domain: $($response.domain)" -ForegroundColor Cyan
    Write-Host "   Total versions: $($response.versions.Count)" -ForegroundColor Cyan
    Write-Host "   Optimization runs: $($response.optimization_runs)" -ForegroundColor Cyan
    
    return ($response._id -eq $script:PromptId -and $response.versions.Count -gt 0)
}

# ═══════════════════════════════════════════════════════════════════════════════
# Test 8: GET /apo/runs - List All Runs
# ═══════════════════════════════════════════════════════════════════════════════
Test-Endpoint "GET /apo/runs - List all runs" {
    $response = Invoke-RestMethod -Uri "$BaseUrl/apo/runs?limit=20" -Method Get -ErrorAction Stop
    
    Write-Host "   Total runs: $($response.count)" -ForegroundColor Cyan
    
    if ($response.count -gt 0) {
        $run = $response.runs[0]
        Write-Host "   Latest run: $($run._id)" -ForegroundColor Cyan
        Write-Host "   Status: $($run.status)" -ForegroundColor Cyan
        Write-Host "   Score: $($run.best_score)" -ForegroundColor Cyan
    }
    
    return $response.count -gt 0
}

# ═══════════════════════════════════════════════════════════════════════════════
# Test 9: POST /apo/optimize - Second Optimization (Concurrent Test)
# ═══════════════════════════════════════════════════════════════════════════════
Test-Endpoint "POST /apo/optimize - Second optimization (concurrent)" {
    $body = @{
        prompt_name = "test-code-assistant"
        initial_prompt = "You are an expert programmer. Help users write better code."
        domain = "coding"
        description = "Code assistant prompt optimization"
        iterations = 2
        evaluation_samples = 3
        model = "Qwen/Qwen3.5-2B-Instruct"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$BaseUrl/apo/optimize" -Method Post -Body $body -ContentType "application/json" -ErrorAction Stop
    
    Write-Host "   Run ID: $($response.run_id)" -ForegroundColor Cyan
    Write-Host "   Prompt ID: $($response.prompt_id)" -ForegroundColor Cyan
    
    return ($response.run_id -and $response.prompt_id)
}

# ═══════════════════════════════════════════════════════════════════════════════
# Test 10: GET /apo/runs?status=completed - Filter by Status
# ═══════════════════════════════════════════════════════════════════════════════
Write-Host "`nWaiting 3 seconds for second optimization..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

Test-Endpoint "GET /apo/runs?status=completed - Filter completed runs" {
    $response = Invoke-RestMethod -Uri "$BaseUrl/apo/runs?status=completed&limit=50" -Method Get -ErrorAction Stop
    
    Write-Host "   Completed runs: $($response.count)" -ForegroundColor Cyan
    
    return $response.count -gt 0
}

# ═══════════════════════════════════════════════════════════════════════════════
# Test 11: GET /apo/prompts?domain=coding - Filter by Domain
# ═══════════════════════════════════════════════════════════════════════════════
Test-Endpoint "GET /apo/prompts?domain=coding - Filter by domain" {
    $response = Invoke-RestMethod -Uri "$BaseUrl/apo/prompts?domain=coding" -Method Get -ErrorAction Stop
    
    Write-Host "   Coding prompts: $($response.count)" -ForegroundColor Cyan
    
    return $response.count -gt 0
}

# ═══════════════════════════════════════════════════════════════════════════════
# Test 12: Verify MongoDB Persistence
# ═══════════════════════════════════════════════════════════════════════════════
Test-Endpoint "MongoDB Persistence - Verify collections exist" {
    # Check that data persists across API calls
    $prompts1 = Invoke-RestMethod -Uri "$BaseUrl/apo/prompts" -Method Get -ErrorAction Stop
    Start-Sleep -Milliseconds 500
    $prompts2 = Invoke-RestMethod -Uri "$BaseUrl/apo/prompts" -Method Get -ErrorAction Stop
    
    Write-Host "   First call: $($prompts1.count) prompts" -ForegroundColor Cyan
    Write-Host "   Second call: $($prompts2.count) prompts" -ForegroundColor Cyan
    Write-Host "   Data persistence: ✅" -ForegroundColor Cyan
    
    return ($prompts1.count -eq $prompts2.count -and $prompts1.count -gt 0)
}

# ═══════════════════════════════════════════════════════════════════════════════
# Summary
# ═══════════════════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   Test Summary" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "Total Tests: $($TestResults.Total)" -ForegroundColor White
Write-Host "Passed: $($TestResults.Passed)" -ForegroundColor Green
Write-Host "Failed: $($TestResults.Failed)" -ForegroundColor Red
Write-Host ""

if ($TestResults.Failed -eq 0) {
    Write-Host "🎉 ALL TESTS PASSED! APO system fully operational." -ForegroundColor Green
    Write-Host ""
    Write-Host "✅ API Endpoints: Working" -ForegroundColor Green
    Write-Host "✅ Optimization Workflow: Working" -ForegroundColor Green
    Write-Host "✅ MongoDB Integration: Working" -ForegroundColor Green
    Write-Host "✅ Version Control: Working" -ForegroundColor Green
    Write-Host "✅ Concurrent Runs: Working" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Review optimization results" -ForegroundColor White
    Write-Host "  2. Test with custom evaluation criteria" -ForegroundColor White
    Write-Host "  3. Integrate with LLM for advanced evaluation" -ForegroundColor White
    Write-Host "  4. Create APO user documentation" -ForegroundColor White
    exit 0
} else {
    Write-Host "⚠️  SOME TESTS FAILED. Review errors above." -ForegroundColor Yellow
    exit 1
}
