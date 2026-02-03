# Package AutonautsMP for distribution
$ErrorActionPreference = "Stop"

Write-Host "Building AutonautsMP..." -ForegroundColor Cyan

# Build mod
Write-Host "[1/3] Building mod..." -ForegroundColor Yellow
dotnet build AutonautsMP.csproj -c Release
if ($LASTEXITCODE -ne 0) { exit 1 }

# Build installer
Write-Host "[2/3] Building installer..." -ForegroundColor Yellow
dotnet publish Installer/AutonautsMP.Installer.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
if ($LASTEXITCODE -ne 0) { exit 1 }

# Build updater
Write-Host "[3/3] Building updater..." -ForegroundColor Yellow
dotnet publish Updater/AutonautsMP.Updater.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
if ($LASTEXITCODE -ne 0) { exit 1 }

# Create AutonautsMPInstaller
$dist = "AutonautsMPInstaller"
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Path $dist | Out-Null

Copy-Item "bin/Release/AutonautsMP.dll" "$dist/"
Copy-Item "bin/Release/Telepathy.dll" "$dist/"
Copy-Item "Installer/bin/Release/net8.0/win-x64/publish/AutonautsMP.Installer.exe" "$dist/"
Copy-Item "Updater/bin/Release/net8.0/win-x64/publish/AutonautsMP.Updater.exe" "$dist/"

Write-Host "`nPackage created in: $((Resolve-Path $dist).Path)" -ForegroundColor Green
Get-ChildItem $dist | ForEach-Object { Write-Host "  $($_.Name) - $("{0:N0}" -f $_.Length) bytes" }
