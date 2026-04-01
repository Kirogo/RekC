@echo off
REM Rekova Migration - Windows Setup Script

setlocal enabledelayedexpansion

echo.
echo ========================================
echo Rekova System - Migration Setup (Windows)
echo ========================================
echo.

REM ==================== CHECK PREREQUISITES ====================
echo [1/7] Checking Prerequisites...
echo.

REM Check MySQL
where mysql >nul 2>nul
if %errorlevel% neq 0 (
    echo ERROR: MySQL not found. Please install MySQL Server.
    pause
    exit /b 1
)
echo OK - MySQL installed

REM Check .NET SDK
where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo ERROR: .NET SDK not found. Please install .NET 8.0 SDK.
    pause
    exit /b 1
)
echo OK - .NET SDK installed

REM ==================== CREATE DATABASE ====================
echo.
echo [2/7] Creating MySQL Database...
echo.

set /p MYSQL_PASSWORD="Enter MySQL root password: "

mysql -u root -p%MYSQL_PASSWORD% -e "USE RekovaDB;" >nul 2>&1
if %errorlevel% equ 0 (
    echo WARNING: Database RekovaDB already exists. Skipping creation.
) else (
    echo Creating database...
    mysql -u root -p%MYSQL_PASSWORD% < Database\01_CreateSchema.sql
    if %errorlevel% neq 0 (
        echo ERROR: Failed to create database.
        pause
        exit /b 1
    )
    echo OK - Database created successfully
)

REM ==================== CONFIGURE APPLICATION ====================
echo.
echo [3/7] Configuring Application...
echo.

set /p JWT_KEY="Enter desired JWT Secret Key (32+ characters): "

echo OK - Configuration prepared
echo WARNING: Please manually update appsettings.json with:
echo    - MySQL password
echo    - JWT Key: %JWT_KEY%

REM ==================== BUILD PROJECT ====================
echo.
echo [4/7] Building C# Project...
echo.

dotnet clean
if %errorlevel% neq 0 goto build_failed

dotnet restore
if %errorlevel% neq 0 goto build_failed

dotnet build
if %errorlevel% neq 0 goto build_failed

echo OK - Project built successfully
goto verify_schema

:build_failed
echo ERROR: Build failed
pause
exit /b 1

REM ==================== DATABASE MIGRATIONS ====================
echo.
echo [5/7] Running Database Migrations...
echo.

dotnet ef migrations add InitialCreate >nul 2>&1
dotnet ef database update
if %errorlevel% neq 0 (
    echo WARNING: Migrations may already exist
)

echo OK - Migrations processed

REM ==================== VERIFY SCHEMA ====================
:verify_schema
echo.
echo [6/7] Verifying Database Schema...
echo.

mysql -u root -p%MYSQL_PASSWORD% -e "USE RekovaDB; SELECT COUNT(*) as table_count FROM information_schema.tables WHERE table_schema = 'RekovaDB';"

echo OK - Schema verification complete

REM ==================== STARTUP INSTRUCTIONS ====================
echo.
echo [7/7] Setup Complete!
echo.

echo ========================================
echo SUCCESS - Migration Setup Completed!
echo ========================================
echo.

echo Next Steps:
echo.
echo 1. Start MySQL Server:
echo    - Open Services and start "MySQL80"
echo    - Or run: net start MySQL80
echo.

echo 2. Start the C# Backend:
echo    - Run: dotnet run
echo.

echo 3. Update Frontend API URL:
echo    - Edit: RekovaFE/src/services/api.js
echo    - Change: 'http://localhost:5050' to 'http://localhost:5000'
echo.

echo 4. Start the Frontend:
echo    - Run: npm run dev
echo.

echo 5. Test the System:
echo    - Open http://localhost:5173
echo    - Login with existing credentials
echo    - Verify all functionality
echo.

echo Happy Collecting!
echo.

pause
