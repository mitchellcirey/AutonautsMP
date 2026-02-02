# Build AutonautsMP mod
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# Copy BepInEx/Unity DLLs from game if needed
$lib = Join-Path $root "lib"
if (-not (Test-Path (Join-Path $lib "BepInEx.dll"))) {
    Write-Host "Copying DLLs from game..."
    $gamePath = "C:\Program Files (x86)\Steam\steamapps\common\Autonauts"
    New-Item -ItemType Directory -Path $lib -Force | Out-Null
    Copy-Item (Join-Path $gamePath "BepInEx\core\BepInEx.dll") $lib -Force
    Copy-Item (Join-Path $gamePath "BepInEx\core\0Harmony.dll") $lib -Force
    Copy-Item (Join-Path $gamePath "Autonauts_Data\Managed\UnityEngine.dll") $lib -Force
    Copy-Item (Join-Path $gamePath "Autonauts_Data\Managed\UnityEngine.CoreModule.dll") $lib -Force
    Copy-Item (Join-Path $gamePath "Autonauts_Data\Managed\UnityEngine.IMGUIModule.dll") $lib -Force
}

Write-Host "Building AutonautsMP..."
dotnet build (Join-Path $root "AutonautsMP.csproj") -c Release
dotnet build (Join-Path $root "Network\Impl\AutonautsMP.Network.csproj") -c Release

Write-Host ""
Write-Host "Build complete! Run install.ps1 to install."
