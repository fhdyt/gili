# Publishes Gili framework-dependent to %LOCALAPPDATA%\Programs\Gili (PRD 9.4) and starts it.
# A running copy is asked to quit with --quit first, so it releases its AppBar strip; taskkill can leave it running without a bar.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$target = Join-Path $env:LOCALAPPDATA 'Programs\Gili'

$running = @(Get-Process -Name Gili -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Start-Process -FilePath $running[0].Path -ArgumentList '--quit' -Wait
    foreach ($process in $running) {
        if (-not $process.WaitForExit(5000)) { throw 'Gili did not quit. Quit it from the bar menu and run this again.' }
    }
}

# Empty the program folder first, like the installer does: after Gili-Setup was installed here, its self-contained
# runtime (hostfxr.dll and the rest) would stay beside this framework-dependent build, and Gili.exe would then look for
# .NET in this folder and fail with "You must install or update .NET". The installer's own uninstaller (unins*) stays.
# Notes and settings live in %LOCALAPPDATA%\Gili, not here.
if (Test-Path $target) {
    Get-ChildItem $target -Force | Where-Object { $_.Name -notlike 'unins*' } | Remove-Item -Recurse -Force
}

# Windows can hold the old files for a moment after the process exits, so a failed copy is tried once more.
foreach ($attempt in 1..2) {
    dotnet publish (Join-Path $root 'src/Gili.App/Gili.App.csproj') -c Release -o $target --no-self-contained
    if ($LASTEXITCODE -eq 0) { break }
    if ($attempt -eq 2) { throw 'dotnet publish failed.' }
    Start-Sleep -Seconds 2
}

# The Claude hook helper is NativeAOT (F-CLD-07); the native linker finds MSVC through vswhere in the VS Installer folder.
$vsInstaller = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
if (Test-Path $vsInstaller) { $env:PATH = "$env:PATH;$vsInstaller" }
dotnet publish (Join-Path $root 'src/Gili.ClaudeHook/Gili.ClaudeHook.csproj') -c Release -r win-x64 -o $target -p:DebugType=None -p:NativeDebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Publishing Gili.ClaudeHook failed. It needs the C++ build tools (Desktop development with C++).' }
Remove-Item (Join-Path $target 'Gili.ClaudeHook.pdb') -ErrorAction SilentlyContinue
Start-Process (Join-Path $target 'Gili.exe')
