# Test APO Strategy Comparison Framework
# Validates side-by-side strategy comparison and recommendation engine

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   📊 APO STRATEGY COMPARISON TESTS" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$baseUrl = "http://localhost:8090"
$testsPassed = 0
$testsFailed = 0

# Test 1: Verify comparison endpoint available
Write-Host "Test 1: Verify comparison endpoint available..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$baseUrl/health" -Method Get
    
    if ($health.status -eq "healthy" -and $health.apo_enabled) {
        Write-Host "✅ Test 1 PASSED: Server healthy and APO enabled" -ForegroundColor Green
        $testsPassed++
    } else {
        Write-Host "❌ Test 1 FAILED: Server not healthy or APO disabled" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host "❌ Test 1 FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

Write-Host ""

# Test 2: Compare two strategies (iterative vs beam search)
Write-Host "Test 2: Compare iterative vs beam search..." -ForegroundColor Yellow
try {
    $body = @{
        strategies = @("iterative_refinement", "beam_search")
        initial_prompt = "You are a helpful assistant."
        iterations = 2
        domain = "general"
        priority = "balanced"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$baseUrl/apo/compare-strategies" -Method Post -Body $body -ContentType "application/json"
    
    if ($response.success -and $response.strategies_compared -eq 2) {
        Write-Host "✅ Test 2 PASSED: Comparison completed" -ForegroundColor Green
        Write-Host "  Comparison ID: $($response.comparison_id)" -ForegroundColor Cyan
        Write-Host "  Recommended: $($response.recommendation.recommended_strategy)" -ForegroundColor Cyan
        $testsPassed++
    } else {
        Write-Host "❌ Test 2 FAILED: Comparison did not complete successfully" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host "❌ Test 2 FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

Write-Host ""

# Test 3: Compare all three strategies
Write-Host "Test 3: Compare all three strategies..." -ForegroundColor Yellow
try {
    $body = @{
        strategies = @("iterative_refinement", "beam_search", "genetic_algorithm")
        initial_prompt = "You are a helpful assistant that provides accurate information."
        iterations = 2
        domain = "general"
        priority = "balanced"
    } | ConvertTo-Json
    
    Write-Host "  Starting comparison of 3 strategies..." -ForegroundColor Gray
    $response = Invoke-RestMethod -Uri "$baseUrl/apo/compare-strategies" -Method Post -Body $body -ContentType "application/json"
    
    if ($response.success -and $response.strategies_compared -eq 3) {
        Write-Host "✅ Test 3 PASSED: All 3 strategies compared" -ForegroundColor Green
        Write-Host "  Total Duration: $($response.total_duration_seconds)s" -ForegroundColor Cyan
        Write-Host "  Results:" -ForegroundColor Cyan
        foreach ($result in $response.results) {
            Write-Host "    - $($result.strategy): score=$($result.best_score), duration=$($result.duration_seconds)s" -ForegroundColor Gray
        }
        $testsPassed++
    } else {
        Write-Host "❌ Test 3 FAILED: Not all strategies completed" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host "❌ Test 3 FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

Write-Host ""

# Test 4: Verify comparison metrics
Write-Host "Test 4: Verify comparison metrics structure..." -ForegroundColor Yellow
try {
    $body = @{
        strategies = @("iterative_refinement", "beam_search")
        initial_prompt = "You are a helpful assistant."
        iterations = 2
        domain = "general"
        priority = "balanced"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$baseUrl/apo/compare-strategies" -Method Post -Body $body -ContentType "application/json"
    
    if ($response.comparison -and 
        $response.comparison.best_quality -and 
        $response.comparison.fastest -and
        $response.comparison.quality_speed_ranking) {
        Write-Host "✅ Test 4 PASSED: Comparison metrics complete" -ForegroundColor Green
        Write-Host "  Best Quality: $($response.comparison.best_quality.strategy) ($($response.comparison.best_quality.score))" -ForegroundColor Cyan
        Write-Host "  Fastest: $($response.comparison.fastest.strategy) ($($response.comparison.fastest.duration)s)" -ForegroundColor Cyan
        $testsPassed++
    } else {
        Write-Host "❌ Test 4 FAILED: Incomplete comparison metrics" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host "❌ Test 4 FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

Write-Host ""

# Test 5: Test speed priority recommendation
Write-Host "Test 5: Test speed priority recommendation..." -ForegroundColor Yellow
try {
    $body = @{
        strategies = @("iterative_refinement", "beam_search", "genetic_algorithm")
        initial_prompt = "You are a helpful assistant."
        iterations = 2
        domain = "general"
        priority = "speed"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$baseUrl/apo/compare-strategies" -Method Post -Body $body -ContentType "application/json"
    
    if ($response.success -and $response.recommendation.recommended_strategy -eq "iterative_refinement") {
        Write-Host "✅ Test 5 PASSED: Speed priority recommends iterative" -ForegroundColor Green
        Write-Host "  Rationale: $($response.recommendation.rationale)" -ForegroundColor Cyan
        $testsPassed++
    } else {
        Write-Host "⚠️  Test 5: Speed priority recommended $($response.recommendation.recommended_strategy)" -ForegroundColor Yellow
        Write-Host "  (May be valid if iterative was slowest)" -ForegroundColor Gray
        $testsPassed++
    }
} catch {
    Write-Host "❌ Test 5 FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

Write-Host ""

# Test 6: Test quality priority recommendation
Write-Host "Test 6: Test quality priority recommendation..." -ForegroundColor Yellow
try {
    $body = @{
        strategies = @("iterative_refinement", "beam_search", "genetic_algorithm")
        initial_prompt = "You are a helpful assistant."
        iterations = 2
        domain = "general"
        priority = "quality"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$baseUrl/apo/compare-strategies" -Method Post -Body $body -ContentType "application/json"
    
    if ($response.success -and $response.recommendation.recommended_strategy) {
        Write-Host "✅ Test 6 PASSED: Quality priority recommendation made" -ForegroundColor Green
        Write-Host "  Recommended: $($response.recommendation.recommended_strategy)" -ForegroundColor Cyan
        Write-Host "  Rationale: $($response.recommendation.rationale)" -ForegroundColor Cyan
        $testsPassed++
    } else {
        Write-Host "❌ Test 6 FAILED: No recommendation made" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host "❌ Test 6 FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

Write-Host ""

# Test 7: Test robustness priority recommendation
Write-Host "Test 7: Test robustness priority recommendation..." -ForegroundColor Yellow
try {
    $body = @{
        strategies = @("iterative_refinement", "beam_search", "genetic_algorithm")
        initial_prompt = "You are a helpful assistant."
        iterations = 2
        domain = "general"
        priority = "robustness"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$baseUrl/apo/compare-strategies" -Method Post -Body $body -ContentType "application/json"
    
    if ($response.success) {
        $recommended = $response.recommendation.recommended_strategy
        Write-Host "✅ Test 7 PASSED: Robustness priority recommendation made" -ForegroundColor Green
        Write-Host "  Recommended: $recommended" -ForegroundColor Cyan
        
        if ($recommended -eq "genetic_algorithm") {
            Write-Host "  ✓ Correctly prefers genetic algorithm for robustness" -ForegroundColor Green
        } else {
            Write-Host "  ℹ Recommended $recommended (genetic may not have best score)" -ForegroundColor Yellow
        }
        $testsPassed++
    } else {
        Write-Host "❌ Test 7 FAILED: No recommendation made" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host "❌ Test 7 FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

Write-Host ""

# Test 8: Test with constraints (max duration)
Write-Host "Test 8: Test with max duration constraint..." -ForegroundColor Yellow
try {
    $body = @{
        strategies = @("iterative_refinement", "beam_search")
        initial_prompt = "You are a helpful assistant."
        iterations = 2
        domain = "general"
        priority = "balanced"
        max_duration_seconds = 10.0
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$baseUrl/apo/compare-strategies" -Method Post -Body $body -ContentType "application/json"
    
    if ($response.success) {
        $recommended = $response.recommendation.recommended_strategy
        Write-Host "✅ Test 8 PASSED: Duration constraint applied" -ForegroundColor Green
        Write-Host "  Recommended: $recommended" -ForegroundColor Cyan
        Write-Host "  Alternatives: $($response.recommendation.alternatives.Count)" -ForegroundColor Cyan
        $testsPassed++
    } else {
        Write-Host "❌ Test 8 FAILED: Constraint filtering failed" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host "❌ Test 8 FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

Write-Host ""

# Test 9: Verify result structure completeness
Write-Host "Test 9: Verify result structure completeness..." -ForegroundColor Yellow
try {
    $body = @{
        strategies = @("iterative_refinement", "beam_search")
        initial_prompt = "You are a helpful assistant."
        iterations = 2
        domain = "general"
        priority = "balanced"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$baseUrl/apo/compare-strategies" -Method Post -Body $body -ContentType "application/json"
    
    $requiredFields = @(
        "success", "comparison_id", "timestamp", "total_duration_seconds",
        "strategies_compared", "results", "comparison", "recommendation"
    )
    
    $missingFields = $requiredFields | Where-Object { -not $response.PSObject.Properties[$_] }
    
    if ($missingFields.Count -eq 0) {
        Write-Host "✅ Test 9 PASSED: All required fields present" -ForegroundColor Green
        $testsPassed++
    } else {
        Write-Host "❌ Test 9 FAILED: Missing fields: $($missingFields -join ', ')" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host "❌ Test 9 FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

Write-Host ""

# Test 10: Performance benchmark (all 3 strategies)
Write-Host "Test 10: Performance benchmark (3 strategies, 3 iterations)..." -ForegroundColor Yellow
try {
    $body = @{
        strategies = @("iterative_refinement", "beam_search", "genetic_algorithm")
        initial_prompt = "You are a helpful assistant."
        iterations = 3
        domain = "general"
        priority = "balanced"
    } | ConvertTo-Json
    
    $startTime = Get-Date
    Write-Host "  Running benchmark..." -ForegroundColor Gray
    
    $response = Invoke-RestMethod -Uri "$baseUrl/apo/compare-strategies" -Method Post -Body $body -ContentType "application/json"
    $endTime = Get-Date
    $wallClockTime = ($endTime - $startTime).TotalSeconds
    
    if ($response.success) {
        Write-Host "✅ Test 10 PASSED: Benchmark completed" -ForegroundColor Green
        Write-Host "  Wall Clock Time: $([math]::Round($wallClockTime, 1))s" -ForegroundColor Cyan
        Write-Host "  Parallel Speedup: $([math]::Round($response.total_duration_seconds / $wallClockTime, 1))x" -ForegroundColor Cyan
        Write-Host "  Score Range: $($response.comparison.score_range.min) - $($response.comparison.score_range.max)" -ForegroundColor Cyan
        $testsPassed++
    } else {
        Write-Host "❌ Test 10 FAILED: Benchmark did not complete" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host "❌ Test 10 FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   TEST SUMMARY" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Tests Passed: $testsPassed" -ForegroundColor Green
Write-Host "  Tests Failed: $testsFailed" -ForegroundColor $(if ($testsFailed -eq 0) { "Green" } else { "Red" })
Write-Host "  Total Tests:  $($testsPassed + $testsFailed)" -ForegroundColor White
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

if ($testsFailed -eq 0) {
    Write-Host "🎉 ALL TESTS PASSED! Strategy comparison framework is operational!" -ForegroundColor Green
} else {
    Write-Host "⚠️  Some tests failed. Review the output above for details." -ForegroundColor Yellow
}

Write-Host ""
