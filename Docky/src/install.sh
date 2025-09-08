#!/bin/bash

# Docky CLI Tool Installation Script

echo "🐳 Installing Docky CLI Tool..."

# Check if .NET 9.0 is installed
if ! dotnet --version | grep -q "9."; then
    echo "❌ .NET 9.0 SDK is required but not found."
    echo "Please install .NET 9.0 SDK from: https://dotnet.microsoft.com/download"
    exit 1
fi

# Build and pack the project
echo "📦 Building and packing the project..."
dotnet pack -o ./nupkg

if [ $? -ne 0 ]; then
    echo "❌ Build failed!"
    exit 1
fi

# Install as global tool
echo "🔧 Installing as global tool..."
dotnet tool install --global docky --add-source ./nupkg

if [ $? -eq 0 ]; then
    echo "✅ Docky CLI Tool installed successfully!"
    echo ""
    echo "Usage examples:"
    echo "  docky generate docker-compose --model base"
    echo "  docky generate docker-compose --model full"
    echo "  docky generate docker-compose --model base --add-redis"
    echo ""
    echo "For more help, run: docky --help"
else
    echo "❌ Installation failed!"
    exit 1
fi
