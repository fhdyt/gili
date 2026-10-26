# Publishes TopBar framework-dependent to %LOCALAPPDATA%\Programs\TopBar (PRD 9.4) and starts it.
# A running copy is asked to quit with --quit first, so it releases its AppBar strip; taskkill can leave it running without a bar.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$target = Join-Path $env:LOCALAPPDATA 'Programs\TopBar'

$running = @(Get-Process -Name TopBar -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Start-Process -FilePath $running[0].Path -ArgumentList '--quit' -Wait
    foreach ($process in $running) {
        if (-not $process.WaitForExit(5000)) { throw 'TopBar did not quit. Quit it from the bar menu and run this again.' }
    }
}

dotnet publish (Join-Path $root 'src/TopBar.App/TopBar.App.csproj') -c Release -o $target --no-self-contained
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
Start-Process (Join-Path $target 'TopBar.exe')
