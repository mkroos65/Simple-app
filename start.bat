@echo off
title Simple Flight Tracker
echo.
echo =========================================
echo     Simple Flight Tracker - Launcher
echo =========================================
echo.

:: Check if .NET SDK is available
where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK not found.
    echo Please install .NET 8 SDK from https://dotnet.microsoft.com/download
    echo.
    pause
    exit /b 1
)

:: Check for real SimConnect DLLs
if not exist "lib\SimConnect.dll" (
    echo WARNING: lib\SimConnect.dll not found.
    echo Copy the native SimConnect.dll from your MSFS SDK:
    echo   copy "C:\MSFS SDK\SimConnect SDK\lib\SimConnect.dll" lib\
    echo.
)

:: Build the project
echo [1/2] Building project...
echo.
dotnet build --nologo -v q
if errorlevel 1 (
    echo.
    echo BUILD FAILED. See errors above.
    echo.
    pause
    exit /b 1
)

echo.
echo [2/2] Starting Simple Flight Tracker...
echo.

:: Run the app
dotnet run --no-build

echo.
echo Simple Flight Tracker has stopped.
pause
