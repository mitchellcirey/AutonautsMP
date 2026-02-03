# Quick dev install - builds and copies mod directly to game folder
$ErrorActionPreference = "Stop"

# Build mod only (skip installer)
Write-Host "Building mod..." -ForegroundColor Cyan
dotnet build AutonautsMP.csproj -c Release
if ($LASTEXITCODE -ne 0) { exit 1 }

# Find game path
$gamePaths = @(
    "C:\Program Files (x86)\Steam\steamapps\common\Autonauts",
    "C:\Program Files\Steam\steamapps\common\Autonauts",
    "D:\Steam\steamapps\common\Autonauts",
    "D:\SteamLibrary\steamapps\common\Autonauts"
)

$gamePath = $gamePaths | Where-Object { Test-Path "$_\Autonauts.exe" } | Select-Object -First 1

if (-not $gamePath) {
    Write-Host "ERROR: Could not find Autonauts. Edit this script with your game path." -ForegroundColor Red
    exit 1
}

$modDir = "$gamePath\BepInEx\plugins\AutonautsMP"

# Create mod folder if needed
if (-not (Test-Path $modDir)) {
    New-Item -ItemType Directory -Path $modDir | Out-Null
}

# Copy mod files
Write-Host "Installing to: $modDir" -ForegroundColor Yellow
Copy-Item "bin/Release/AutonautsMP.dll" "$modDir/" -Force
Copy-Item "bin/Release/Telepathy.dll" "$modDir/" -Force

# Write version.txt
$devSettings = Get-Content "Core/DevSettings.cs" -Raw
if ($devSettings -match 'Version\s*=\s*"([^"]+)"') {
    $Matches[1] | Out-File -FilePath "$modDir/version.txt" -Encoding utf8 -NoNewline
}

# Clear cache
$cachePath = "$gamePath\BepInEx\cache"
if (Test-Path $cachePath) {
    Remove-Item $cachePath -Recurse -Force
    Write-Host "Cache cleared" -ForegroundColor Green
}

Write-Host "`nDev install complete!" -ForegroundColor Green
