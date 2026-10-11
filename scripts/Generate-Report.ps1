$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
dotnet build ./tools/ReportGenerator/ReportGenerator.csproj
if ($LASTEXITCODE -ne 0) { throw 'Nie udalo sie skompilowac generatora raportu.' }
& powershell.exe -ExecutionPolicy Bypass -File ./tools/ReportGenerator/bin/Debug/net8.0/playwright.ps1 install chromium
if ($LASTEXITCODE -ne 0) { throw 'Nie udalo sie zainstalowac Chromium dla Playwright.' }
dotnet run --no-build --project ./tools/ReportGenerator/ReportGenerator.csproj
if ($LASTEXITCODE -ne 0) { throw 'Scenariusz UI nie powiodl sie. Raport nie zostal uznany za gotowy.' }
Write-Host 'Raport: Documentation/Raport_z_dzialania.pdf' -ForegroundColor Green
