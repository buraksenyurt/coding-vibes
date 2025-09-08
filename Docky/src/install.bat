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

REM Build and pack the project
echo 📦 Building and packing the project...
dotnet pack -o ./nupkg

if %errorlevel% neq 0 (
    echo ❌ Build failed!
    exit /b 1
)

REM Install as global tool
echo 🔧 Installing as global tool...
dotnet tool install --global docky --add-source ./nupkg

if %errorlevel% equ 0 (
    echo ✅ Docky CLI Tool installed successfully!
    echo.
    echo Usage examples:
    echo   docky generate docker-compose --model base
    echo   docky generate docker-compose --model full
    echo   docky generate docker-compose --model base --add-redis
    echo.
    echo For more help, run: docky --help
) else (
    echo ❌ Installation failed!
    exit /b 1
)

pause
