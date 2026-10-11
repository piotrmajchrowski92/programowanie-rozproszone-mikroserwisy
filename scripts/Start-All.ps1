param([switch]$NoBrowser)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Zainstaluj SDK .NET 8.' }
dotnet restore ./MicroservicesAssignment.sln
if ($LASTEXITCODE -ne 0) { throw 'Nie udało się pobrać pakietów NuGet.' }
dotnet build ./MicroservicesAssignment.sln --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Kompilacja nie powiodła się.' }
foreach ($project in @('LogService','AuthService','TicketService','ApiGateway','WebClient')) {
    $command = "Set-Location '$($root.Replace("'", "''"))'; dotnet run --no-build --project './$project/$project.csproj' --launch-profile '$project'"
    Start-Process powershell.exe -ArgumentList @('-NoExit','-Command',$command)
    Start-Sleep -Milliseconds 800
}
if (-not $NoBrowser) { Start-Sleep -Seconds 4; Start-Process 'http://127.0.0.1:5200' }
