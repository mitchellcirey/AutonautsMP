# Build and package AutonautsMP for distribution
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dist = Join-Path $root "dist"

# Build mod
Write-Host "Building mod..."
dotnet build (Join-Path $root "AutonautsMP.csproj") -c Release
dotnet build (Join-Path $root "Network\Impl\AutonautsMP.Network.csproj") -c Release

# Build installer
Write-Host "Building installer..."
dotnet publish (Join-Path $root "Installer\AutonautsMP.Installer.csproj") -c Release

# Create dist folder
Write-Host "Creating distribution package..."
Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $dist -Force | Out-Null

# Copy files
Copy-Item (Join-Path $root "bin\Release\AutonautsMP.dll") $dist
Copy-Item (Join-Path $root "bin\Release\AutonautsMP.Network.dll") $dist
Copy-Item (Join-Path $root "bin\Release\LiteNetLib.dll") $dist
Copy-Item (Join-Path $root "Installer\bin\Release\net8.0\win-x64\publish\AutonautsMP.Installer.exe") $dist

Write-Host ""
Write-Host "================================================"
Write-Host "  Package created: $dist"
Write-Host "================================================"
Write-Host ""
Write-Host "Files:"
Get-ChildItem $dist | ForEach-Object { Write-Host "  - $($_.Name)" }
Write-Host ""
Write-Host "Send the entire 'dist' folder to your friend."
Write-Host "They just need to run AutonautsMP.Installer.exe"
