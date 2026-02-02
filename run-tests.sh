#!/bin/bash
# StatStock Test Runner (Bash version for Linux/Mac)
# Run all unit tests and display results

echo "========================================"
echo "  StatStock Test Suite Runner"
echo "========================================"
echo ""

echo "Building projects..."
dotnet build --nologo --verbosity quiet

if [ $? -ne 0 ]; then
    echo "Build failed!"
    exit 1
fi

echo "Build successful!"
echo ""

echo "Running Unit Tests..."
echo ""

dotnet test tests/StatStock.UnitTests/StatStock.UnitTests.csproj --nologo --verbosity normal

if [ $? -eq 0 ]; then
    echo ""
    echo "========================================"
    echo "  All Tests Passed! ✅"
    echo "========================================"
else
    echo ""
    echo "========================================"
    echo "  Some Tests Failed ❌"
    echo "========================================"
    exit 1
fi

echo ""
echo "Test Summary Document: docs/TESTING_SUMMARY.md"
echo ""
