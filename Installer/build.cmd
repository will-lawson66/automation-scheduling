@echo off
REM Build script for Automation Scheduler Installer (WiX v4)
REM This script builds the complete installer solution

echo ========================================
echo Automation Scheduler Installer Build
echo WiX v4 Version
echo ========================================
echo.

REM Check if dotnet is available
where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo ERROR: dotnet CLI not found in PATH
    echo Please install .NET SDK 8.0 or later
    exit /b 1
)

REM Check if wix is available
dotnet wix --version >nul 2>nul
if %errorlevel% neq 0 (
    echo ERROR: WiX v4 not found
    echo Installing WiX v4...
    dotnet tool install --global wix
    if %errorlevel% neq 0 (
        echo Failed to install WiX v4
        exit /b 1
    )
)

REM Set build configuration
set Configuration=Release
set Platform=x64

REM Set source path for your application files
REM TODO: Update this path to point to your build output
set SourcePath=C:\Path\To\Your\Application\Build\Output

echo Configuration: %Configuration%
echo Platform: %Platform%
echo Source Path: %SourcePath%
echo.

REM Restore packages
echo Restoring packages...
dotnet restore AutomationSchedulerInstaller.sln
if %errorlevel% neq 0 goto :error

REM Clean previous builds
echo Cleaning previous builds...
dotnet clean AutomationSchedulerInstaller.sln -c %Configuration%
if %errorlevel% neq 0 goto :error

REM Build Custom Actions first
echo.
echo Building Custom Actions...
dotnet build AutomationScheduler.CustomActions\AutomationScheduler.CustomActions.csproj -c %Configuration%
if %errorlevel% neq 0 goto :error

REM Build MSI
echo.
echo Building MSI package...
dotnet build AutomationScheduler.Installer\AutomationScheduler.Installer.wixproj -c %Configuration% /p:SourcePath="%SourcePath%"
if %errorlevel% neq 0 goto :error

REM Build Bootstrapper
echo.
echo Building Bootstrapper...
dotnet build AutomationScheduler.Bootstrapper\AutomationScheduler.Bootstrapper.wixproj -c %Configuration%
if %errorlevel% neq 0 goto :error

echo.
echo ========================================
echo Build completed successfully!
echo ========================================
echo.
echo Output files:
echo - MSI: AutomationScheduler.Installer\bin\%Platform%\%Configuration%\en-US\AutomationSchedulerSetup.msi
echo - Bootstrapper: AutomationScheduler.Bootstrapper\bin\%Platform%\%Configuration%\AutomationSchedulerBootstrapper.exe
echo.
goto :end

:error
echo.
echo ========================================
echo Build FAILED!
echo ========================================
exit /b 1

:end
