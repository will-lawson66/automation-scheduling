@echo off
echo ========================================
echo WiX v4 Diagnostic Script
echo ========================================
echo.

echo Checking WiX installation...
dotnet tool list -g | findstr wix
if %errorlevel% neq 0 (
    echo [ERROR] WiX not found as global tool
    echo.
    echo To fix: dotnet tool install --global wix --version 4.0.5
) else (
    echo [OK] WiX is installed
)
echo.

echo Checking WiX version...
dotnet wix --version 2>nul
if %errorlevel% neq 0 (
    echo [ERROR] Cannot run 'dotnet wix'
    echo Trying alternate command...
    wix --version 2>nul
    if %errorlevel% neq 0 (
        echo [ERROR] WiX command not accessible
    )
)
echo.

echo Checking for old WiX v3 installations...
if exist "C:\Program Files (x86)\WiX Toolset v3*" (
    echo [WARNING] WiX v3 found - this might cause conflicts
    dir "C:\Program Files (x86)\WiX Toolset v3*" /b
)
echo.

echo Checking .NET SDK version...
dotnet --version
echo.

echo Checking for Import statements in wixproj files...
findstr /s /i "Import Project" *.wixproj
if %errorlevel% equ 0 (
    echo [ERROR] Found Import statements - these should be removed for WiX v4!
) else (
    echo [OK] No Import statements found
)
echo.

echo Checking for correct SDK reference...
findstr /s "WixToolset.Sdk" *.wixproj
if %errorlevel% neq 0 (
    echo [ERROR] No WixToolset.Sdk found in project files
) else (
    echo [OK] WixToolset.Sdk reference found
)
echo.

echo Testing minimal build...
cd MinimalBootstrapper 2>nul
if %errorlevel% equ 0 (
    echo Building minimal bootstrapper...
    dotnet build
    if %errorlevel% equ 0 (
        echo [OK] Minimal bootstrapper builds successfully!
    ) else (
        echo [ERROR] Minimal bootstrapper failed to build
    )
    cd ..
) else (
    echo [SKIP] MinimalBootstrapper directory not found
)
echo.

echo ========================================
echo Diagnostic complete
echo ========================================
