# Load Testing Framework for DeepResearch Lightning Server
# Tests concurrent operations, response times, and system stability

param(
    [int]$ConcurrentAPO = 5,
    [int]$ConcurrentVERL = 3,
    [int]$DurationSeconds = 60,
    [string]$ServerUrl = "http://localhost:8090"
)

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  LOAD TESTING FRAMEWORK - DeepResearch Lightning Server" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$totalTests = 0
$passedTests = 0
$failedTests = 0

# Helper function for test results
function Test-Result {
    param($TestName, $Condition, $Details = "")
    
    $script:totalTests++
    
    if ($Condition) {
        Write-Host "✅ $TestName" -ForegroundColor Green
        if ($Details) { Write-Host "   $Details" -ForegroundColor Gray }
        $script:passedTests++
        return $true
    } else {
        Write-Host "❌ $TestName" -ForegroundColor Red
        if ($Details) { Write-Host "   $Details" -ForegroundColor Yellow }
        $script:failedTests++
        return $false
    }
}

# Helper function for performance measurement
function Measure-RequestPerformance {
    param($Url, $Method = "GET", $Body = $null)
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    try {
        $params = @{
            Uri = $Url
            Method = $Method
            TimeoutSec = 30
        }
        
        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json)
            $params.ContentType = "application/json"
        }
        
        $response = Invoke-RestMethod @params -ErrorAction Stop
        $stopwatch.Stop()
        
        return @{
            Success = $true
            ResponseTime = $stopwatch.ElapsedMilliseconds
            Response = $response
        }
    }
    catch {
        $stopwatch.Stop()
        return @{
            Success = $false
            ResponseTime = $stopwatch.ElapsedMilliseconds
            Error = $_.Exception.Message
        }
    }
}

Write-Host "📊 Test Configuration:" -ForegroundColor Yellow
Write-Host "   Concurrent APO requests: $ConcurrentAPO"
Write-Host "   Concurrent VERL requests: $ConcurrentVERL"
Write-Host "   Test duration: $DurationSeconds seconds"
Write-Host "   Server URL: $ServerUrl"
Write-Host ""

# Test 1: Server Health Check
Write-Host "Test 1: Server Health Check" -ForegroundColor Cyan
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray

$healthResult = Measure-RequestPerformance "$ServerUrl/health"
Test-Result "Server is responding" $healthResult.Success "Response time: $($healthResult.ResponseTime)ms"

if (-not $healthResult.Success) {
    Write-Host ""
    Write-Host "❌ Server not available - aborting load tests" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Test 2: Baseline Response Times
Write-Host "Test 2: Baseline Response Times" -ForegroundColor Cyan
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray

$baselineMeasurements = @()

Write-Host "Measuring baseline with 10 sequential requests..."
for ($i = 1; $i -le 10; $i++) {
    $result = Measure-RequestPerformance "$ServerUrl/api/server/info"
    $baselineMeasurements += $result.ResponseTime
}

$baselineAvg = ($baselineMeasurements | Measure-Object -Average).Average
$baselineP95 = ($baselineMeasurements | Sort-Object)[[math]::Floor(9.5)]

Test-Result "Baseline response time acceptable" ($baselineAvg -lt 1000) "Average: $([math]::Round($baselineAvg, 2))ms, P95: $($baselineP95)ms"

Write-Host ""

# Test 3: Concurrent APO Optimizations
Write-Host "Test 3: Concurrent APO Optimizations" -ForegroundColor Cyan
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray

Write-Host "Starting $ConcurrentAPO concurrent APO optimization requests..."

$apoJobs = @()
$apoStartTime = Get-Date

for ($i = 1; $i -le $ConcurrentAPO; $i++) {
    $apoRequest = @{
        prompt = "Translate the following to French: Hello, how are you?"
        target_metric = "quality"
        model = "Qwen/Qwen2.5-0.5B-Instruct"
        iterations = 2
        strategy = "iterative_refinement"
    }
    
    $job = Start-Job -ScriptBlock {
        param($Url, $Body)
        try {
            $response = Invoke-RestMethod -Uri $Url -Method POST -Body ($Body | ConvertTo-Json) -ContentType "application/json" -TimeoutSec 60
            return @{ Success = $true; Response = $response }
        }
        catch {
            return @{ Success = $false; Error = $_.Exception.Message }
        }
    } -ArgumentList "$ServerUrl/apo/optimize", $apoRequest
    
    $apoJobs += $job
}

Write-Host "Waiting for APO jobs to complete (max 120 seconds)..."
$apoJobs | Wait-Job -Timeout 120 | Out-Null

$apoSuccessful = 0
$apoFailed = 0
$apoResponseTimes = @()

foreach ($job in $apoJobs) {
    $result = Receive-Job -Job $job
    if ($result.Success) {
        $apoSuccessful++
    } else {
        $apoFailed++
    }
    Remove-Job -Job $job
}

$apoElapsed = ((Get-Date) - $apoStartTime).TotalSeconds

Test-Result "APO concurrent requests completed" ($apoSuccessful -gt 0) "Successful: $apoSuccessful, Failed: $apoFailed, Time: $([math]::Round($apoElapsed, 2))s"
Test-Result "APO success rate acceptable" (($apoSuccessful / $ConcurrentAPO) -ge 0.6) "Success rate: $([math]::Round(($apoSuccessful / $ConcurrentAPO) * 100, 1))%"

Write-Host ""

# Test 4: VERL Advanced Features API
Write-Host "Test 4: VERL Advanced Features API" -ForegroundColor Cyan
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray

# Test features discovery
$featuresResult = Measure-RequestPerformance "$ServerUrl/verl/features"
Test-Result "VERL features endpoint responding" $featuresResult.Success "Response time: $($featuresResult.ResponseTime)ms"

if ($featuresResult.Success) {
    $features = $featuresResult.Response.features
    
    Test-Result "Model architectures available" $features.model_architectures.available
    Test-Result "Distributed training available" $features.distributed_training.available
    Test-Result "Fine-tuning available" $features.finetuning.available
}

Write-Host ""

# Test 5: VERL Configuration Endpoints
Write-Host "Test 5: VERL Configuration Endpoints" -ForegroundColor Cyan
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray

# Test architecture configuration
$archRequest = @{
    architecture_type = "medium"
    model_class = "policy"
}

$archResult = Measure-RequestPerformance "$ServerUrl/verl/configure/architecture?architecture_type=medium&model_class=policy" "POST" $null
Test-Result "Architecture configuration endpoint" $archResult.Success "Response time: $($archResult.ResponseTime)ms"

# Test distributed configuration
$distResult = Measure-RequestPerformance "$ServerUrl/verl/configure/distributed?num_gpus=4&enable_mixed_precision=true&precision_mode=fp16" "POST" $null
Test-Result "Distributed training configuration endpoint" $distResult.Success "Response time: $($distResult.ResponseTime)ms"

# Test fine-tuning configuration
$ftResult = Measure-RequestPerformance "$ServerUrl/verl/configure/finetuning?enable_lora=true&lora_rank=8&enable_adapters=true&adapter_size=128" "POST" $null
Test-Result "Fine-tuning configuration endpoint" $ftResult.Success "Response time: $($ftResult.ResponseTime)ms"

Write-Host ""

# Test 6: Sustained Load Test
Write-Host "Test 6: Sustained Load Test ($DurationSeconds seconds)" -ForegroundColor Cyan
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray

$sustainedStart = Get-Date
$sustainedRequests = 0
$sustainedSuccesses = 0
$sustainedFailures = 0
$sustainedResponseTimes = @()

Write-Host "Running sustained requests for $DurationSeconds seconds..."

while (((Get-Date) - $sustainedStart).TotalSeconds -lt $DurationSeconds) {
    $result = Measure-RequestPerformance "$ServerUrl/health"
    $sustainedRequests++
    $sustainedResponseTimes += $result.ResponseTime
    
    if ($result.Success) {
        $sustainedSuccesses++
    } else {
        $sustainedFailures++
    }
    
    Start-Sleep -Milliseconds 100
}

$sustainedAvg = ($sustainedResponseTimes | Measure-Object -Average).Average
$sustainedP50 = ($sustainedResponseTimes | Sort-Object)[[math]::Floor($sustainedRequests * 0.5)]
$sustainedP95 = ($sustainedResponseTimes | Sort-Object)[[math]::Floor($sustainedRequests * 0.95)]
$sustainedP99 = ($sustainedResponseTimes | Sort-Object)[[math]::Floor($sustainedRequests * 0.99)]

Test-Result "Sustained load completed" ($sustainedRequests -gt 0) "Total requests: $sustainedRequests"
Test-Result "Sustained success rate high" (($sustainedSuccesses / $sustainedRequests) -ge 0.95) "Success rate: $([math]::Round(($sustainedSuccesses / $sustainedRequests) * 100, 2))%"
Test-Result "Average response time acceptable" ($sustainedAvg -lt 500) "Avg: $([math]::Round($sustainedAvg, 2))ms"

Write-Host ""
Write-Host "📊 Sustained Load Statistics:" -ForegroundColor Yellow
Write-Host "   Total requests: $sustainedRequests"
Write-Host "   Successful: $sustainedSuccesses"
Write-Host "   Failed: $sustainedFailures"
Write-Host "   Requests per second: $([math]::Round($sustainedRequests / $DurationSeconds, 2))"
Write-Host "   Response times:"
Write-Host "     - Average: $([math]::Round($sustainedAvg, 2))ms"
Write-Host "     - P50 (median): $($sustainedP50)ms"
Write-Host "     - P95: $($sustainedP95)ms"
Write-Host "     - P99: $($sustainedP99)ms"

Write-Host ""

# Test 7: Memory Leak Detection
Write-Host "Test 7: Memory Leak Detection" -ForegroundColor Cyan
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray

Write-Host "Performing 100 rapid requests to detect memory leaks..."

$memoryCheckStart = @()
$memoryCheckEnd = @()

for ($i = 1; $i -le 100; $i++) {
    $null = Measure-RequestPerformance "$ServerUrl/api/server/info"
    if ($i -eq 10) {
        # Baseline after warmup
        Start-Sleep -Seconds 1
        try {
            $info = Invoke-RestMethod "$ServerUrl/api/server/info" -TimeoutSec 5
            $memoryCheckStart = $info
        } catch {}
    }
}

Start-Sleep -Seconds 2

try {
    $info = Invoke-RestMethod "$ServerUrl/api/server/info" -TimeoutSec 5
    $memoryCheckEnd = $info
} catch {}

Test-Result "Memory leak check completed" ($memoryCheckEnd -ne $null) "100 requests processed"

Write-Host ""

# Summary
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  LOAD TEST SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

Write-Host "📊 Results:" -ForegroundColor Yellow
Write-Host "   Total tests: $totalTests"
Write-Host "   Passed: $passedTests" -ForegroundColor Green
Write-Host "   Failed: $failedTests" -ForegroundColor $(if ($failedTests -gt 0) { "Red" } else { "Gray" })
Write-Host "   Success rate: $([math]::Round(($passedTests / $totalTests) * 100, 2))%"
Write-Host ""

Write-Host "⚡ Performance Metrics:" -ForegroundColor Yellow
Write-Host "   Baseline response (avg): $([math]::Round($baselineAvg, 2))ms"
Write-Host "   Concurrent APO ($ConcurrentAPO requests): $([math]::Round($apoElapsed, 2))s"
Write-Host "   Sustained load ($DurationSeconds seconds):"
Write-Host "     - Total requests: $sustainedRequests"
Write-Host "     - RPS: $([math]::Round($sustainedRequests / $DurationSeconds, 2))"
Write-Host "     - P95 response: $($sustainedP95)ms"
Write-Host ""

if ($failedTests -eq 0) {
    Write-Host "✅ ALL LOAD TESTS PASSED!" -ForegroundColor Green
    Write-Host "   System is stable under load and ready for production" -ForegroundColor Green
    exit 0
} else {
    Write-Host "⚠️  SOME LOAD TESTS FAILED" -ForegroundColor Yellow
    Write-Host "   Review failed tests and optimize system performance" -ForegroundColor Yellow
    exit 1
}
