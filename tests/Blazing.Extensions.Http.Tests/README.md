# Blazing.Extensions.Http.Tests

This project contains comprehensive unit tests for the Blazing.Extensions.Http library.

## Test Framework

- **xUnit**: Primary testing framework
- **FluentAssertions**: For readable and expressive assertions
- **NSubstitute**: For mocking dependencies

## Test Coverage

### Models Tests
- `TransferRateTests`: Tests for transfer rate calculations and units
- `TransferStateTests`: Tests for transfer state tracking and progress calculations
- `LatencyTrackerTests`: Tests for latency tracking (TTFB, total transfer time)
- `ByteUnitTests`: Tests for byte unit enum values
- `BitUnitTests`: Tests for bit unit enum values

### HttpClient Extensions Tests
- Tests for download operations with progress tracking
- Tests for upload operations with progress tracking
- Tests for latency measurement
- Tests for transfer statistics

## Multi-Targeting

This test project targets:
- .NET 8.0
- .NET 9.0
- .NET 10.0

This ensures the library works correctly across all supported .NET versions.

## Running Tests

### Using Visual Studio
1. Open Test Explorer (Test > Test Explorer)
2. Click "Run All" to execute all tests

### Using .NET CLI
```bash
# Run all tests
dotnet test

# Run tests for specific framework
dotnet test --framework net8.0
dotnet test --framework net9.0
dotnet test --framework net10.0

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Using Visual Studio Code
1. Install the .NET Core Test Explorer extension
2. Tests will appear in the Test Explorer sidebar
3. Click the play button to run tests

## Test Organization

Tests are organized by namespace matching the source code structure:
- `Blazing.Extensions.Http.Tests.Models`: Tests for model classes
- `Blazing.Extensions.Http.Tests`: Tests for HttpClient extension methods

## Adding New Tests

When adding new tests:
1. Follow the existing naming convention: `{ClassName}Tests.cs`
2. Use descriptive test method names: `MethodName_Scenario_ExpectedBehavior`
3. Follow AAA pattern: Arrange, Act, Assert
4. Use FluentAssertions for assertions
5. Add Theory tests for multiple scenarios
6. Ensure tests work across all target frameworks
