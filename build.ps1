# Build script for AutonautsMP
# Builds both the mod DLL and the installer

$ErrorActionPreference = "Stop"

Write-Host "Building AutonautsMP..." -ForegroundColor Cyan
Write-Host ""

# Build the main mod
Write-Host "[1/2] Building mod DLL..." -ForegroundColor Yellow
dotnet build AutonautsMP.csproj -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build mod!" -ForegroundColor Red
    exit 1
}
Write-Host "  Mod built successfully" -ForegroundColor Green
Write-Host ""

# Build the installer
Write-Host "[2/2] Building installer..." -ForegroundColor Yellow
dotnet build Installer/AutonautsMP.Installer.csproj -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build installer!" -ForegroundColor Red
    exit 1
}
Write-Host "  Installer built successfully" -ForegroundColor Green
Write-Host ""

Write-Host "Build complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Output files:"
Write-Host "  Mod:       bin/Release/netstandard2.0/AutonautsMP.dll"
Write-Host "  Installer: Installer/bin/Release/net6.0/AutonautsMP.Installer.exe"
Write-Host ""
Write-Host "Run .\package.ps1 to create a distributable package."
