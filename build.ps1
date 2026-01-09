#!/usr/bin/env pwsh
# Build script for Codebase RAG solution

Write-Host "====================================" -ForegroundColor Cyan
Write-Host "Building Codebase RAG Solution" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

# Check if .NET SDK is installed
Write-Host "Checking .NET SDK..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: .NET SDK not found. Please install .NET 8 SDK." -ForegroundColor Red
    exit 1
}
Write-Host "Found .NET SDK version: $dotnetVersion" -ForegroundColor Green
Write-Host ""

# Restore dependencies
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore CodebaseRag.sln
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to restore packages" -ForegroundColor Red
    exit 1
}
Write-Host "Packages restored successfully" -ForegroundColor Green
Write-Host ""

# Build solution
Write-Host "Building solution..." -ForegroundColor Yellow
dotnet build CodebaseRag.sln --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed" -ForegroundColor Red
    exit 1
}
Write-Host "Build completed successfully" -ForegroundColor Green
Write-Host ""

# Display output
Write-Host "====================================" -ForegroundColor Cyan
Write-Host "Build Summary" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host "Configuration: Release" -ForegroundColor White
Write-Host "Output: src/CodebaseRag.Api/bin/Release/net8.0/" -ForegroundColor White
Write-Host ""
Write-Host "Build completed successfully!" -ForegroundColor Green
