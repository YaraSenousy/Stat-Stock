# StatStock Test Runner
# Run all unit tests and display results

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  StatStock Test Suite Runner" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Building projects..." -ForegroundColor Yellow
dotnet build --nologo --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build successful!" -ForegroundColor Green
Write-Host ""

Write-Host "Running Unit Tests..." -ForegroundColor Yellow
Write-Host ""

$testResult = dotnet test tests\StatStock.UnitTests\StatStock.UnitTests.csproj --nologo --verbosity normal

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  All Tests Passed! ✅" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "  Some Tests Failed ❌" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Test Summary Document: docs\TESTING_SUMMARY.md" -ForegroundColor Cyan
Write-Host ""
