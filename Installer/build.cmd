@echo off
REM Build script for Automation Scheduler Installer
REM This script builds the complete installer solution

echo ========================================
echo Automation Scheduler Installer Build
echo ========================================
echo.

REM Check if MSBuild is available
where msbuild >nul 2>nul
if %errorlevel% neq 0 (
    echo ERROR: MSBuild not found in PATH
    echo Please run this script from a Visual Studio Developer Command Prompt
    exit /b 1
)

REM Set build configuration
set Configuration=Release
set Platform=x86

REM Set source path for your application files
REM TODO: Update this path to point to your build output
set SourcePath=C:\Path\To\Your\Application\Build\Output

echo Configuration: %Configuration%
echo Platform: %Platform%
echo Source Path: %SourcePath%
echo.

REM Clean previous builds
echo Cleaning previous builds...
msbuild AutomationSchedulerInstaller.sln /t:Clean /p:Configuration=%Configuration% /p:Platform=%Platform%
if %errorlevel% neq 0 goto :error

REM Build the solution
echo.
echo Building solution...
msbuild AutomationSchedulerInstaller.sln /t:Build /p:Configuration=%Configuration% /p:Platform=%Platform% /p:SourcePath="%SourcePath%"
if %errorlevel% neq 0 goto :error

echo.
echo ========================================
echo Build completed successfully!
echo ========================================
echo.
echo Output files:
echo - MSI: AutomationScheduler.Installer\bin\%Configuration%\AutomationSchedulerSetup.msi
echo - Bootstrapper: AutomationScheduler.Bootstrapper\bin\%Configuration%\AutomationSchedulerBootstrapper.exe
echo.
goto :end

:error
echo.
echo ========================================
echo Build FAILED!
echo ========================================
exit /b 1

:end
