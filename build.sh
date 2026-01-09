#!/bin/bash
# Build script for Codebase RAG solution

echo "===================================="
echo "Building Codebase RAG Solution"
echo "===================================="
echo ""

# Check if .NET SDK is installed
echo "Checking .NET SDK..."
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET SDK not found. Please install .NET 8 SDK."
    exit 1
fi
DOTNET_VERSION=$(dotnet --version)
echo "Found .NET SDK version: $DOTNET_VERSION"
echo ""

# Restore dependencies
echo "Restoring NuGet packages..."
dotnet restore CodebaseRag.sln
if [ $? -ne 0 ]; then
    echo "ERROR: Failed to restore packages"
    exit 1
fi
echo "Packages restored successfully"
echo ""

# Build solution
echo "Building solution..."
dotnet build CodebaseRag.sln --configuration Release --no-restore
if [ $? -ne 0 ]; then
    echo "ERROR: Build failed"
    exit 1
fi
echo "Build completed successfully"
echo ""

# Display output
echo "===================================="
echo "Build Summary"
echo "===================================="
echo "Configuration: Release"
echo "Output: src/CodebaseRag.Api/bin/Release/net8.0/"
echo ""
echo "Build completed successfully!"
