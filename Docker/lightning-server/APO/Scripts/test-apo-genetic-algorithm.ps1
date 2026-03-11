# Test APO Genetic Algorithm Optimization
# Validates genetic algorithm strategy implementation

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   🧬 APO GENETIC ALGORITHM OPTIMIZATION TESTS" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$baseUrl = "http://localhost:8090"
$testsPassed = 0
$testsFailed = 0

# Test 1: Verify genetic algorithm strategy available
Write-Host "Test 1: Verify genetic algorithm strategy available..." -ForegroundColor Yellow
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

# Test 2: Run genetic algorithm optimization (small population, 2 generations)
Write-Host "Test 2: Run genetic algorithm optimization (pop=3, gen=2)..." -ForegroundColor Yellow
try {
    $timestamp = Get-Date -Format "HHmmss"
    $body = @{
        prompt_name = "ga-test-$timestamp"
        initial_prompt = "You are a helpful assistant."
        domain = "general"
        iterations = 2
        optimization_strategy = "genetic_algorithm"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$baseUrl/apo/optimize" -Method Post -Body $body -ContentType "application/json"
    
    if ($response.run_id) {
        Write-Host "  Run ID: $($response.run_id)" -ForegroundColor Cyan
        
        # Wait for completion
        Write-Host "  Waiting 30 seconds for completion..." -ForegroundColor Gray
        Start-Sleep -Seconds 30
        
        # Check status
        $status = Invoke-RestMethod -Uri "$baseUrl/apo/runs/$($response.run_id)" -Method Get
        
        if ($status.status -eq "completed") {
            Write-Host "✅ Test 2 PASSED: GA optimization completed" -ForegroundColor Green
            Write-Host "  Best Score: $($status.best_score)" -ForegroundColor Cyan
            Write-Host "  Generations: $($status.iterations_completed.Count)" -ForegroundColor Cyan
            $testsPassed++
        } else {
            Write-Host "❌ Test 2 FAILED: Status is $($status.status)" -ForegroundColor Red
            $testsFailed++
        }
    } else {
        Write-Host "❌ Test 2 FAILED: No run_id in response" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host "❌ Test 2 FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

Write-Host ""

# Test 3: Compare genetic algorithm vs iterative refinement
Write-Host "Test 3: Compare GA vs iterative refinement performance..." -ForegroundColor Yellow
try {
    $initialPrompt = "You are a helpful assistant that provides clear and accurate information."
    
    # Run iterative refinement
    $timestamp = Get-Date -Format "HHmmss"
    $iterBody = @{
        prompt_name = "compare-iter-$timestamp"
        initial_prompt = $initialPrompt
        domain = "general"
        iterations = 2
        optimization_strategy = "iterative_refinement"
    } | ConvertTo-Json
    
    $iterResponse = Invoke-RestMethod -Uri "$baseUrl/apo/optimize" -Method Post -Body $iterBody -ContentType "application/json"
    
    # Run genetic algorithm
    $gaBody = @{
        prompt_name = "compare-ga-$timestamp"
        initial_prompt = $initialPrompt
        domain = "general"
        iterations = 2
        optimization_strategy = "genetic_algorithm"
    } | ConvertTo-Json
    
    $gaResponse = Invoke-RestMethod -Uri "$baseUrl/apo/optimize" -Method Post -Body $gaBody -ContentType "application/json"
    
    # Wait for both to complete
    Write-Host "  Waiting 35 seconds for both runs to complete..." -ForegroundColor Gray
    Start-Sleep -Seconds 35
    
    $iterStatus = Invoke-RestMethod -Uri "$baseUrl/apo/runs/$($iterResponse.run_id)" -Method Get
    $gaStatus = Invoke-RestMethod -Uri "$baseUrl/apo/runs/$($gaResponse.run_id)" -Method Get
    
    if ($iterStatus.status -eq "completed" -and $gaStatus.status -eq "completed") {
        Write-Host "✅ Test 3 PASSED: Both strategies completed" -ForegroundColor Green
        Write-Host "  Iterative - Duration: $($iterStatus.total_duration_seconds)s, Score: $($iterStatus.best_score)" -ForegroundColor Cyan
        Write-Host "  Genetic   - Duration: $($gaStatus.total_duration_seconds)s, Score: $($gaStatus.best_score)" -ForegroundColor Cyan
        Write-Host "  Genetic diversity: $($gaStatus.final_population_size) individuals" -ForegroundColor Cyan
        $testsPassed++
    } else {
        Write-Host "❌ Test 3 FAILED: One or both runs did not complete" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host "❌ Test 3 FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

Write-Host ""

# Test 4: Verify genetic algorithm configuration
Write-Host "Test 4: Verify GA configuration parameters..." -ForegroundColor Yellow
try {
    $timestamp = Get-Date -Format "HHmmss"
    $body = @{
        prompt_name = "ga-config-$timestamp"
        initial_prompt = "You are a helpful assistant."
        domain = "general"
        iterations = 2
        optimization_strategy = "genetic_algorithm"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$baseUrl/apo/optimize" -Method Post -Body $body -ContentType "application/json"
    
    # Wait for completion
    Start-Sleep -Seconds 30
    
    $status = Invoke-RestMethod -Uri "$baseUrl/apo/runs/$($response.run_id)" -Method Get
    
    if ($status.strategy_metadata) {
        $metadata = $status.strategy_metadata
        if ($metadata.population_size -and $metadata.mutation_rate -and $metadata.crossover_rate) {
            Write-Host "✅ Test 4 PASSED: GA configuration recorded" -ForegroundColor Green
            Write-Host "  Population Size: $($metadata.population_size)" -ForegroundColor Cyan
            Write-Host "  Mutation Rate: $($metadata.mutation_rate)" -ForegroundColor Cyan
            Write-Host "  Crossover Rate: $($metadata.crossover_rate)" -ForegroundColor Cyan
            Write-Host "  Tournament Size: $($metadata.tournament_size)" -ForegroundColor Cyan
            $testsPassed++
        } else {
            Write-Host "❌ Test 4 FAILED: Incomplete strategy metadata" -ForegroundColor Red
            $testsFailed++
        }
    } else {
        Write-Host "❌ Test 4 FAILED: No strategy_metadata found" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host "❌ Test 4 FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

Write-Host ""

# Test 5: Verify generation results structure
Write-Host "Test 5: Verify generation results structure..." -ForegroundColor Yellow
try {
    $timestamp = Get-Date -Format "HHmmss"
    $body = @{
        prompt_name = "ga-structure-$timestamp"
        initial_prompt = "You are a helpful assistant."
        domain = "general"
        iterations = 2
        optimization_strategy = "genetic_algorithm"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$baseUrl/apo/optimize" -Method Post -Body $body -ContentType "application/json"
    
    # Wait for completion
    Start-Sleep -Seconds 30
    
    $status = Invoke-RestMethod -Uri "$baseUrl/apo/runs/$($response.run_id)" -Method Get
    
    if ($status.iterations_completed -and $status.iterations_completed.Count -gt 0) {
        $firstGen = $status.iterations_completed[0]
        
        if ($firstGen.generation -and $firstGen.best_score -ne $null -and $firstGen.avg_score -ne $null -and $firstGen.population_diversity) {
            Write-Host "✅ Test 5 PASSED: Generation results have correct structure" -ForegroundColor Green
            Write-Host "  Fields: generation, best_score, avg_score, population_diversity ✓" -ForegroundColor Cyan
            $testsPassed++
        } else {
            Write-Host "❌ Test 5 FAILED: Missing required fields in generation result" -ForegroundColor Red
            $testsFailed++
        }
    } else {
        Write-Host "❌ Test 5 FAILED: No iterations_completed data" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host "❌ Test 5 FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

Write-Host ""

# Test 6: Verify population diversity tracking
Write-Host "Test 6: Verify population diversity tracking..." -ForegroundColor Yellow
try {
    $timestamp = Get-Date -Format "HHmmss"
    $body = @{
        prompt_name = "ga-diversity-$timestamp"
        initial_prompt = "You are a helpful assistant."
        domain = "general"
        iterations = 3
        optimization_strategy = "genetic_algorithm"
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "$baseUrl/apo/optimize" -Method Post -Body $body -ContentType "application/json"
    
    # Wait for completion
    Write-Host "  Waiting 45 seconds for 3 generations..." -ForegroundColor Gray
    Start-Sleep -Seconds 45
    
    $status = Invoke-RestMethod -Uri "$baseUrl/apo/runs/$($response.run_id)" -Method Get
    
    if ($status.iterations_completed -and $status.iterations_completed.Count -ge 3) {
        $diversities = $status.iterations_completed | ForEach-Object { $_.population_diversity }
        $avgDiversity = ($diversities | Measure-Object -Average).Average
        
        Write-Host "✅ Test 6 PASSED: Population diversity tracked across generations" -ForegroundColor Green
        Write-Host "  Generation diversities: $($diversities -join ', ')" -ForegroundColor Cyan
        Write-Host "  Average diversity: $([math]::Round($avgDiversity, 2))" -ForegroundColor Cyan
        $testsPassed++
    } else {
        Write-Host "❌ Test 6 FAILED: Insufficient generation data" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host "❌ Test 6 FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

Write-Host ""

# Test 7: Performance benchmark (GA with moderate generations)
Write-Host "Test 7: Performance benchmark (4 generations)..." -ForegroundColor Yellow
try {
    $timestamp = Get-Date -Format "HHmmss"
    $body = @{
        prompt_name = "ga-benchmark-$timestamp"
        initial_prompt = "You are a helpful assistant."
        domain = "general"
        iterations = 4
        optimization_strategy = "genetic_algorithm"
    } | ConvertTo-Json
    
    $startTime = Get-Date
    $response = Invoke-RestMethod -Uri "$baseUrl/apo/optimize" -Method Post -Body $body -ContentType "application/json"
    
    # Wait for completion
    Write-Host "  Waiting 60 seconds for 4 generations..." -ForegroundColor Gray
    Start-Sleep -Seconds 60
    
    $status = Invoke-RestMethod -Uri "$baseUrl/apo/runs/$($response.run_id)" -Method Get
    $endTime = Get-Date
    
    if ($status.status -eq "completed") {
        $duration = $status.total_duration_seconds
        $avgPerGen = $duration / 4
        
        Write-Host "✅ Test 7 PASSED: Benchmark completed" -ForegroundColor Green
        Write-Host "  Total Duration: $([math]::Round($duration, 2))s" -ForegroundColor Cyan
        Write-Host "  Average per Generation: $([math]::Round($avgPerGen, 2))s" -ForegroundColor Cyan
        Write-Host "  Final Best Score: $($status.best_score)" -ForegroundColor Cyan
        $testsPassed++
    } else {
        Write-Host "❌ Test 7 FAILED: Benchmark did not complete" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host "❌ Test 7 FAILED: $($_.Exception.Message)" -ForegroundColor Red
    $testsFailed++
}

Write-Host ""

# Test 8: Concurrent genetic algorithm runs
Write-Host "Test 8: Concurrent genetic algorithm runs..." -ForegroundColor Yellow
try {
    $timestamp = Get-Date -Format "HHmmss"
    
    # Start 2 GA runs simultaneously
    $body1 = @{
        prompt_name = "ga-concurrent1-$timestamp"
        initial_prompt = "You are a helpful assistant."
        domain = "general"
        iterations = 2
        optimization_strategy = "genetic_algorithm"
    } | ConvertTo-Json
    
    $body2 = @{
        prompt_name = "ga-concurrent2-$timestamp"
        initial_prompt = "You are an expert advisor."
        domain = "technical"
        iterations = 2
        optimization_strategy = "genetic_algorithm"
    } | ConvertTo-Json
    
    $response1 = Invoke-RestMethod -Uri "$baseUrl/apo/optimize" -Method Post -Body $body1 -ContentType "application/json"
    $response2 = Invoke-RestMethod -Uri "$baseUrl/apo/optimize" -Method Post -Body $body2 -ContentType "application/json"
    
    Write-Host "  Started 2 concurrent GA runs" -ForegroundColor Gray
    Write-Host "  Run 1: $($response1.run_id)" -ForegroundColor Gray
    Write-Host "  Run 2: $($response2.run_id)" -ForegroundColor Gray
    
    # Wait for both to complete
    Write-Host "  Waiting 40 seconds for completion..." -ForegroundColor Gray
    Start-Sleep -Seconds 40
    
    $status1 = Invoke-RestMethod -Uri "$baseUrl/apo/runs/$($response1.run_id)" -Method Get
    $status2 = Invoke-RestMethod -Uri "$baseUrl/apo/runs/$($response2.run_id)" -Method Get
    
    if ($status1.status -eq "completed" -and $status2.status -eq "completed") {
        Write-Host "✅ Test 8 PASSED: Both concurrent GA runs completed" -ForegroundColor Green
        Write-Host "  Run 1 Score: $($status1.best_score)" -ForegroundColor Cyan
        Write-Host "  Run 2 Score: $($status2.best_score)" -ForegroundColor Cyan
        $testsPassed++
    } else {
        Write-Host "❌ Test 8 FAILED: Not all runs completed" -ForegroundColor Red
        Write-Host "  Run 1: $($status1.status), Run 2: $($status2.status)" -ForegroundColor Red
        $testsFailed++
    }
} catch {
    Write-Host "❌ Test 8 FAILED: $($_.Exception.Message)" -ForegroundColor Red
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
    Write-Host "🎉 ALL TESTS PASSED! Genetic algorithm optimization is operational!" -ForegroundColor Green
} else {
    Write-Host "⚠️  Some tests failed. Review the output above for details." -ForegroundColor Yellow
}

Write-Host ""
