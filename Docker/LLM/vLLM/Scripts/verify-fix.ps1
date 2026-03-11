# Quick verification that the PowerShell heredoc fix works
Write-Host "Testing PowerShell heredoc syntax fix..." -ForegroundColor Cyan

Write-Host "`nRunning Docker test command with @'...'@ syntax..." -ForegroundColor Yellow

docker run --rm python:3.11-slim bash -c @'
echo "✓ Bash received command correctly"
python -c "test_str = 'Qwen/Qwen3.5-9B'; print(f'Model path test: {test_str}')"
echo "✓ Quote nesting works!"
'@

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✓ Syntax fix verified successfully!" -ForegroundColor Green
    Write-Host "`nYou can now run:" -ForegroundColor Cyan
    Write-Host "  .\download-models.ps1" -ForegroundColor White
} else {
    Write-Host "`n✗ Test failed" -ForegroundColor Red
}
