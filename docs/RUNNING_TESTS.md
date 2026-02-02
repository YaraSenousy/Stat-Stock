# Running StatStock Tests

This guide explains how to run tests on different platforms.

## Quick Start

### On Windows (PowerShell)
```powershell
# Make sure you're in PowerShell, not bash/cmd
pwsh

# Then run:
.\run-tests.ps1

# Or manually:
dotnet test tests/StatStock.UnitTests
```

### On Linux/Mac (Bash)
```bash
# Make the script executable
chmod +x run-tests.sh

# Run the tests
./run-tests.sh

# Or manually:
dotnet test tests/StatStock.UnitTests
```

### Universal Command (All Platforms)
```bash
# This works everywhere
dotnet test tests/StatStock.UnitTests/StatStock.UnitTests.csproj
```

## Running Specific Tests

```bash
# Run only unit tests
dotnet test tests/StatStock.UnitTests

# Run only integration tests
dotnet test tests/StatStock.IntegrationTests

# Run a specific test class
dotnet test --filter "FullyQualifiedName~ProductTests"

# Run tests matching a name pattern
dotnet test --filter "Name~LowStock"

# Run with detailed output
dotnet test --verbosity detailed
```

## Integration Tests Setup

The integration tests are now configured with `StatStockWebApplicationFactory` which:
- Uses an in-memory database for testing
- Configures the test server automatically
- Isolates tests from the actual database

To run integration tests:
```bash
dotnet test tests/StatStock.IntegrationTests
```

**Note:** Integration tests may take longer to run as they start the full web application.

## Troubleshooting

### "Write-Host: command not found"
You're trying to run a PowerShell script in bash. Solutions:
1. Use `./run-tests.sh` instead (bash script)
2. Or run `pwsh` first, then `.\run-tests.ps1`
3. Or use the universal command: `dotnet test`

### "Program class not accessible"
Make sure `Program.cs` has this at the end:
```csharp
public partial class Program { }
```

### Integration tests failing
Make sure:
1. StatStock.Web project compiles successfully
2. All dependencies are restored: `dotnet restore`
3. No other instance is using the same ports

### Tests running slowly
Integration tests are slower because they start the web app. To speed up:
- Run only unit tests: `dotnet test tests/StatStock.UnitTests`
- Use `--no-build` flag if you've already built: `dotnet test --no-build`

## Continuous Integration

For CI/CD pipelines (GitHub Actions, Azure DevOps, etc.):

```yaml
# .github/workflows/tests.yml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      - name: Restore dependencies
        run: dotnet restore
      - name: Build
        run: dotnet build --no-restore
      - name: Run Unit Tests
        run: dotnet test tests/StatStock.UnitTests --no-build --verbosity normal
      - name: Run Integration Tests
        run: dotnet test tests/StatStock.IntegrationTests --no-build --verbosity normal
```

## Test Results

Expected output:
```
Test Run Successful.
Total tests: 135
     Passed: 135
 Total time: 3.8 Seconds
```

## More Information

- Full test documentation: [docs/TESTING_SUMMARY.md](../docs/TESTING_SUMMARY.md)
- Test structure: [tests/README.md](README.md)
- xUnit documentation: https://xunit.net/docs/getting-started/netcore/cmdline

## Common Commands Cheat Sheet

```bash
# Run all tests
dotnet test

# Run unit tests only
dotnet test tests/StatStock.UnitTests

# Run integration tests only
dotnet test tests/StatStock.IntegrationTests

# Run with coverage (requires coverlet.msbuild)
dotnet test /p:CollectCoverage=true

# Run and watch for changes
dotnet watch test --project tests/StatStock.UnitTests

# List all tests
dotnet test --list-tests

# Run tests in parallel (default)
dotnet test --parallel

# Run tests one at a time
dotnet test -- xUnit.ParallelizeTestCollections=false
```
