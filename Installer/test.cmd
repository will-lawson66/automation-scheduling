@echo off
REM Test script for Automation Scheduler Installer
REM This script runs all tests in the solution

echo ========================================
echo Automation Scheduler Installer Tests
echo ========================================
echo.

REM Check if dotnet is available
where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo ERROR: dotnet CLI not found in PATH
    echo Please install .NET SDK 8.0 or later
    exit /b 1
)

REM Set test configuration
set Configuration=Release

echo Configuration: %Configuration%
echo.

REM Restore packages
echo Restoring NuGet packages...
dotnet restore AutomationSchedulerInstaller.sln
if %errorlevel% neq 0 goto :error

REM Build the solution
echo.
echo Building solution...
dotnet build AutomationSchedulerInstaller.sln -c %Configuration%
if %errorlevel% neq 0 goto :error

REM Run unit tests
echo.
echo ========================================
echo Running Unit Tests
echo ========================================
dotnet test AutomationScheduler.Installer.Tests\AutomationScheduler.Installer.Tests.csproj ^
    -c %Configuration% ^
    --no-build ^
    --logger "console;verbosity=normal" ^
    --filter "Category!=Integration"
if %errorlevel% neq 0 goto :error

REM Ask if integration tests should be run
echo.
set /p RunIntegration="Run integration tests? (requires built MSI and admin privileges) [y/N]: "
if /i "%RunIntegration%"=="y" (
    echo.
    echo ========================================
    echo Running Integration Tests
    echo ========================================
    echo Note: Some tests may be skipped if MSI is not built or admin privileges are not available
    echo.
    
    dotnet test AutomationScheduler.Installer.Tests\AutomationScheduler.Installer.Tests.csproj ^
        -c %Configuration% ^
        --no-build ^
        --logger "console;verbosity=normal" ^
        --filter "Category=Integration"
)

echo.
echo ========================================
echo All tests completed successfully!
echo ========================================
goto :end

:error
echo.
echo ========================================
echo Tests FAILED!
echo ========================================
exit /b 1

:end
