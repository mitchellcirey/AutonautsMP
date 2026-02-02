# Install AutonautsMP mod
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$binRelease = Join-Path $root "bin\Release"

# Kill game if running
Stop-Process -Name "Autonauts" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

# Install main plugin to game
$gamePath = "C:\Program Files (x86)\Steam\steamapps\common\Autonauts"
$pluginDir = Join-Path $gamePath "BepInEx\plugins\AutonautsMP"
New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
Copy-Item (Join-Path $binRelease "AutonautsMP.dll") $pluginDir -Force

# Install network DLLs to AppData (hidden from game's assembly scanner)
$appDataDir = Join-Path $env:APPDATA "AutonautsMP"
New-Item -ItemType Directory -Path $appDataDir -Force | Out-Null
Copy-Item (Join-Path $binRelease "AutonautsMP.Network.dll") $appDataDir -Force
Copy-Item (Join-Path $binRelease "LiteNetLib.dll") $appDataDir -Force

Write-Host "Installed!"
Write-Host "  Plugin: $pluginDir\AutonautsMP.dll"
Write-Host "  Network: $appDataDir\"
Write-Host ""
Write-Host "Launch Autonauts and click the MP button!"
