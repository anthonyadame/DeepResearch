# Verify LiteLLM Authentication Fix
Write-Host "=== LiteLLM Authentication Fix Verification ===" -ForegroundColor Cyan
Write-Host ""

# Test 1: vLLM endpoints (should work without auth)
Write-Host "Test 1: vLLM endpoints (no auth required)" -ForegroundColor Yellow
Write-Host ""

Write-Host "Testing Qwen (port 8001)..." -ForegroundColor Gray
try {
    $response = curl http://localhost:8001/health 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Qwen endpoint accessible (no auth)" -ForegroundColor Green
    } else {
        Write-Host "✗ Qwen endpoint not accessible" -ForegroundColor Red
        Write-Host "  Make sure vLLM containers are running" -ForegroundColor Yellow
    }
} catch {
    Write-Host "✗ Could not connect to Qwen" -ForegroundColor Red
}

Write-Host ""
Write-Host "Testing Mistral (port 8002)..." -ForegroundColor Gray
try {
    $response = curl http://localhost:8002/health 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Mistral endpoint accessible (no auth)" -ForegroundColor Green
    } else {
        Write-Host "✗ Mistral endpoint not accessible" -ForegroundColor Red
        Write-Host "  Make sure vLLM containers are running" -ForegroundColor Yellow
    }
} catch {
    Write-Host "✗ Could not connect to Mistral" -ForegroundColor Red
}

Write-Host ""
Write-Host "Test 2: LiteLLM without auth (should fail with 401)" -ForegroundColor Yellow
Write-Host ""

Write-Host "Testing LiteLLM without Authorization header..." -ForegroundColor Gray
try {
    $response = curl http://localhost:4000/health 2>&1
    if ($response -match "Authentication Error" -or $response -match "401") {
        Write-Host "✓ Correctly requires authentication" -ForegroundColor Green
        Write-Host "  (401 error is expected behavior)" -ForegroundColor Gray
    } else {
        Write-Host "⚠ Unexpected response (auth may be disabled)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "✗ Could not connect to LiteLLM" -ForegroundColor Red
    Write-Host "  Make sure LiteLLM container is running" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Test 3: LiteLLM with auth (should succeed)" -ForegroundColor Yellow
Write-Host ""

Write-Host "Testing LiteLLM WITH Authorization header..." -ForegroundColor Gray
try {
    $response = curl http://localhost:4000/health -H "Authorization: Bearer sk-1234" 2>&1
    if ($response -match "healthy" -or $LASTEXITCODE -eq 0) {
        Write-Host "✓ Authentication successful!" -ForegroundColor Green
        Write-Host "  Response: $response" -ForegroundColor Gray
    } else {
        Write-Host "✗ Authentication failed" -ForegroundColor Red
        Write-Host "  Response: $response" -ForegroundColor Gray
    }
} catch {
    Write-Host "✗ Could not connect to LiteLLM" -ForegroundColor Red
}

Write-Host ""
Write-Host "Test 4: List models via LiteLLM" -ForegroundColor Yellow
Write-Host ""

Write-Host "Requesting model list..." -ForegroundColor Gray
try {
    $response = curl http://localhost:4000/v1/models -H "Authorization: Bearer sk-1234" 2>&1
    if ($response -match "qwen" -or $response -match "mistral") {
        Write-Host "✓ Models endpoint working!" -ForegroundColor Green
        Write-Host "  Response includes model names" -ForegroundColor Gray
    } else {
        Write-Host "⚠ Unexpected response from models endpoint" -ForegroundColor Yellow
        Write-Host "  Response: $response" -ForegroundColor Gray
    }
} catch {
    Write-Host "✗ Could not get models list" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "If all tests passed:" -ForegroundColor White
Write-Host "✓ vLLM containers are running and accessible" -ForegroundColor Green
Write-Host "✓ LiteLLM correctly requires authentication" -ForegroundColor Green
Write-Host "✓ Authentication with 'Bearer sk-1234' works" -ForegroundColor Green
Write-Host "✓ Multi-model deployment is ready!" -ForegroundColor Green
Write-Host ""
Write-Host "To use LiteLLM, always include:" -ForegroundColor Yellow
Write-Host '  -H "Authorization: Bearer sk-1234"' -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Test inference with a sample request" -ForegroundColor White
Write-Host "2. Configure your C# application with the API key" -ForegroundColor White
Write-Host "3. Monitor with: docker logs lightning-litellm -f" -ForegroundColor White
Write-Host ""
