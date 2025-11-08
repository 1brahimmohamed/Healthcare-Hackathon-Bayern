@echo off
echo ============================================
echo JWT License Validator - Quick Test
echo ============================================
echo.

echo Step 1: Rebuilding LicenseValidatorLibrary...
cd /d "%~dp0LicenseValidatorLibrary"
dotnet build --configuration Debug
if errorlevel 1 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)
echo.

echo Step 2: Rebuilding WpfHack...
cd /d "%~dp0WpfHack"
dotnet build --configuration Debug
if errorlevel 1 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)
echo.

echo Step 3: Checking if keys.json was copied...
if exist "bin\Debug\net9.0-windows10.0.19041.0\keys.json" (
    echo ✓ keys.json found in output directory
    echo.
    echo Contents:
    type "bin\Debug\net9.0-windows10.0.19041.0\keys.json"
) else (
    echo ✗ keys.json NOT found in output directory!
)
echo.

echo Step 4: Starting Node.js server (if not already running)...
echo Please make sure your Node.js server is running with:
echo   cd LicenseValidatorLibrary
echo   npx ts-node index.ts
echo.

echo ============================================
echo Build complete! You can now test the app.
echo ============================================
pause

