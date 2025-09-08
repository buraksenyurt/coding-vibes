@echo off

REM Docky CLI Tool Installation Script for Windows

echo 🐳 Installing Docky CLI Tool...

REM Check if .NET 9.0 is installed
dotnet --version | findstr "9." >nul
if %errorlevel% neq 0 (
    echo ❌ .NET 9.0 SDK is required but not found.
    echo Please install .NET 9.0 SDK from: https://dotnet.microsoft.com/download
    exit /b 1
)

REM Create nupkg directory if it doesn't exist
if not exist "nupkg" mkdir nupkg

REM Build and pack the project
echo 📦 Building and packing the project...
dotnet pack -c Release -o nupkg

if %errorlevel% neq 0 (
    echo ❌ Build failed!
    exit /b 1
)

REM Check if nupkg file exists
if not exist "nupkg\Docky.1.0.0.nupkg" (
    echo ❌ Package file not found! Build may have failed.
    exit /b 1
)

echo ✅ Package created successfully: nupkg\Docky.1.0.0.nupkg

REM Uninstall existing version (ignore errors)
echo 🔄 Removing any existing installation...
dotnet tool uninstall --global docky >nul 2>&1

REM Install as global tool
echo 🔧 Installing as global tool...
dotnet tool install --global docky --add-source "%cd%\nupkg"

if %errorlevel% equ 0 (
    echo ✅ Docky CLI Tool installed successfully!
    echo.
    echo Usage examples:
    echo   docky generate docker-compose --model base
    echo   docky generate docker-compose --model full
    echo   docky generate docker-compose --model microservices
    echo   docky generate docker-compose --model ai-ml
    echo.
    echo For more help, run: docky --help
) else (
    echo ❌ Installation failed!
    echo.
    echo Try manual installation:
    echo 1. dotnet pack -c Release -o nupkg
    echo 2. dotnet tool install --global docky --add-source "%cd%\nupkg"
    exit /b 1
)

pause
