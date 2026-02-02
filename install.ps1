# Install AutonautsMP mod
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# Kill game if running
Stop-Process -Name "Autonauts" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

# Paths to built files
$mainDll = Join-Path $root "bin\Release\netstandard2.0\AutonautsMP.dll"
$networkDll = Join-Path $root "Network\Impl\bin\Release\net471\AutonautsMP.Network.dll"
$liteNetDll = Join-Path $root "Network\Impl\bin\Release\net471\LiteNetLib.dll"

# Install main plugin to game
$gamePath = "C:\Program Files (x86)\Steam\steamapps\common\Autonauts"
$pluginDir = Join-Path $gamePath "BepInEx\plugins\AutonautsMP"
New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
Copy-Item $mainDll $pluginDir -Force

# Install network DLLs to AppData (hidden from game's assembly scanner)
$appDataDir = Join-Path $env:APPDATA "AutonautsMP"
New-Item -ItemType Directory -Path $appDataDir -Force | Out-Null
Copy-Item $networkDll $appDataDir -Force
Copy-Item $liteNetDll $appDataDir -Force

Write-Host "Installed!"
Write-Host "  Plugin: $pluginDir\AutonautsMP.dll"
Write-Host "  Network: $appDataDir\"
Write-Host ""
Write-Host "Launch Autonauts and click the MP button!"
