# Package AutonautsMP for distribution
$ErrorActionPreference = "Stop"

Write-Host "Building AutonautsMP..." -ForegroundColor Cyan

# Build mod
Write-Host "[1/2] Building mod..." -ForegroundColor Yellow
dotnet build AutonautsMP.csproj -c Release
if ($LASTEXITCODE -ne 0) { exit 1 }

# Build installer/updater
Write-Host "[2/2] Building installer..." -ForegroundColor Yellow
dotnet publish Updater/AutonautsMP.Updater.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
if ($LASTEXITCODE -ne 0) { exit 1 }

# Extract version from DevSettings.cs
$devSettings = Get-Content "Core/DevSettings.cs" -Raw
if ($devSettings -match 'Version\s*=\s*"([^"]+)"') {
    $version = $Matches[1]
} else {
    Write-Host "ERROR: Could not find Version in DevSettings.cs" -ForegroundColor Red
    exit 1
}
Write-Host "Version: $version" -ForegroundColor Cyan

# Create AutonautsMPInstaller
$dist = "AutonautsMPInstaller"
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Path $dist | Out-Null

Copy-Item "bin/Release/AutonautsMP.dll" "$dist/"
Copy-Item "bin/Release/Telepathy.dll" "$dist/"
Copy-Item "Updater/bin/Release/net8.0/win-x64/publish/AutonautsMP.exe" "$dist/"

# Write version.txt (included in package and installed alongside mod)
$version | Out-File -FilePath "$dist/version.txt" -Encoding utf8 -NoNewline

Write-Host "`nPackage created in: $((Resolve-Path $dist).Path)" -ForegroundColor Green
Get-ChildItem $dist | ForEach-Object { Write-Host "  $($_.Name) - $("{0:N0}" -f $_.Length) bytes" }
