# Automation Scheduler Installer Tests

This test project provides comprehensive testing for the WiX installer solution, including unit tests, integration tests, and validation tests.

## Test Categories

### 1. Custom Action Tests (`CustomActions/`)
Tests for the C# custom actions that run during installation:
- **UpdateAppSettingsTests** - Tests updating appsettings.json with plugin path
- **UpdateConfigurationFilesTests** - Tests configuration file updates based on user selection
- **DownloadArtifactsTests** - Tests Azure DevOps artifact download functionality

### 2. WiX Validation Tests (`WixValidation/`)
Static analysis of WiX files to catch common issues:
- Valid XML structure
- Unique component IDs
- Valid GUIDs
- Required attributes present
- All referenced directories defined
- Components have proper KeyPath attributes

### 3. Integration Tests (`Integration/`)
Tests that require the actual MSI to be built:
- Installation/uninstallation tests
- Configuration option testing
- Directory structure validation
- MSI database validation

### 4. Performance Tests (`Performance/`)
Tests to ensure custom actions perform efficiently:
- Large file handling
- Timeout behavior
- Concurrent execution safety

## Running Tests

### Prerequisites
1. .NET 8.0 SDK installed
2. Visual Studio 2022 (recommended) or VS Code
3. For integration tests: Built MSI and administrator privileges

### Command Line

#### Run all unit tests:
```cmd
dotnet test --filter "Category!=Integration"
```

#### Run specific test category:
```cmd
dotnet test --filter "FullyQualifiedName~CustomActions"
```

#### Run integration tests (requires admin):
```cmd
dotnet test --filter "Category=Integration"
```

#### Run with coverage:
```cmd
dotnet test --collect:"XPlat Code Coverage"
```

### Using test.cmd Script
```cmd
test.cmd
```
This will:
1. Restore packages
2. Build the solution
3. Run unit tests
4. Optionally run integration tests

### Visual Studio
1. Open Test Explorer (Test → Test Explorer)
2. Build the solution
3. Click "Run All Tests" or select specific tests

## Test Utilities

### MockSessionFactory
Creates mock MSI session objects for testing custom actions:
```csharp
var session = MockSessionFactory.CreateMockSessionWithInstallFolder(@"C:\TestInstall");
```

### TestDataHelper
Provides methods for creating test files and directories:
```csharp
var testDir = TestDataHelper.CreateTempDirectory();
TestDataHelper.CreateAppSettingsFile(testDir);
```

### Skip Attributes
For conditional test execution:
```csharp
[SkippableFact]
public void Test_RequiresAdmin()
{
    Skip.IfNot(IsAdministrator(), "Requires admin privileges");
    // Test code
}
```

## Writing New Tests

### Custom Action Test Template
```csharp
[Fact]
public void CustomAction_ShouldDoSomething_WhenCondition()
{
    // Arrange
    var testDir = TestDataHelper.CreateTempDirectory();
    var session = MockSessionFactory.CreateMockSession(new Dictionary<string, string>
    {
        ["INSTALLFOLDER"] = testDir,
        ["PROPERTY"] = "value"
    });
    
    // Act
    var result = CustomActions.YourAction(session.Object);
    
    // Assert
    result.Should().Be(ActionResult.Success);
    // Additional assertions
}
```

### Integration Test Template
```csharp
[SkippableFact]
public void Msi_ShouldInstallFeature()
{
    Skip.IfNot(File.Exists(_msiPath), "MSI not found");
    Skip.IfNot(IsAdministrator(), "Requires admin");
    
    // Test MSI installation
}
```

## Debugging Tests

1. **Enable logging in tests:**
   ```csharp
   var logs = new List<string>();
   mockSession.Setup(s => s.Log(It.IsAny<string>()))
       .Callback<string>(msg => logs.Add(msg));
   ```

2. **Check test output:**
   - In Visual Studio: Click on test → View Test Detail Summary
   - Command line: Add `--logger "console;verbosity=detailed"`

3. **Debug specific test:**
   - Set breakpoint in test
   - Right-click test in Test Explorer → Debug

## CI/CD Integration

### Azure DevOps Pipeline
```yaml
- task: DotNetCoreCLI@2
  displayName: 'Run Tests'
  inputs:
    command: 'test'
    projects: '**/AutomationScheduler.Installer.Tests.csproj'
    arguments: '--configuration Release --filter "Category!=Integration"'
```

### GitHub Actions
```yaml
- name: Test
  run: dotnet test --no-build --verbosity normal --filter "Category!=Integration"
```

## Best Practices

1. **Isolation**: Each test should create its own test directory
2. **Cleanup**: Always dispose of test resources
3. **Mocking**: Use mocks for external dependencies
4. **Assertions**: Use FluentAssertions for readable test assertions
5. **Naming**: Follow the pattern: `MethodName_ShouldExpectedBehavior_WhenCondition`
6. **Categories**: Tag integration tests appropriately
7. **Performance**: Keep unit tests fast (< 1 second each)

## Troubleshooting

### "MSI not found" errors
- Build the installer project first
- Check the path in `GetMsiPath()` method

### "Access denied" errors
- Run Visual Studio or command prompt as administrator
- Check file permissions in test directories

### Test discovery issues
- Clean and rebuild the solution
- Ensure test project references are correct
- Check for compilation errors

### Flaky tests
- Ensure proper cleanup in Dispose methods
- Add retry logic for network-dependent tests
- Use unique paths for each test run
