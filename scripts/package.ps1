<#
.SYNOPSIS
    Builds Gili's MSIX package (PRD 9.4, F-PUB-01) into artifacts/msix, and with -Install registers it on this laptop.

.DESCRIPTION
    Publishes Gili self-contained for win-x64 into artifacts/msix/layout, adds packaging/AppxManifest.xml (with the
    version from Directory.Build.props) and packaging/Assets, then packs Gili-<version>.msix with makeappx for Partner
    Center. -Install registers the layout itself (Add-AppxPackage -Register), which needs Developer Mode but no admin
    rights and no certificate (plan 1h decision 1), then starts the package.

.EXAMPLE
    pwsh scripts/package.ps1 -Install
#>
param([switch] $Install)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'artifacts/msix'
$layout = Join-Path $out 'layout'

if ($Install) {
    $unlock = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -ErrorAction SilentlyContinue
    if ($unlock.AllowDevelopmentWithoutDevLicense -ne 1) {
        throw 'Turn on Developer Mode (Settings > System > For developers), then run this again: start ms-settings:developers'
    }
}

[xml] $props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = "$($props.Project.PropertyGroup.Version).0"

if (Test-Path $out) { Remove-Item -Recurse -Force $out }
dotnet publish (Join-Path $root 'src/Gili.App/Gili.App.csproj') -c Release -r win-x64 --self-contained -o $layout
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

# The Claude hook helper is NativeAOT (F-CLD-07); the native linker finds MSVC through vswhere in the VS Installer folder.
$vsInstaller = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
if (Test-Path $vsInstaller) { $env:PATH = "$env:PATH;$vsInstaller" }
dotnet publish (Join-Path $root 'src/Gili.ClaudeHook/Gili.ClaudeHook.csproj') -c Release -r win-x64 -o $layout -p:DebugType=None -p:NativeDebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Publishing Gili.ClaudeHook failed. It needs the C++ build tools (Desktop development with C++).' }
Remove-Item (Join-Path $layout 'Gili.ClaudeHook.pdb') -ErrorAction SilentlyContinue

(Get-Content -Raw (Join-Path $root 'packaging/AppxManifest.xml')).Replace('{VERSION}', $version) |
    Set-Content -Encoding utf8 (Join-Path $layout 'AppxManifest.xml')
Copy-Item -Recurse (Join-Path $root 'packaging/Assets') (Join-Path $layout 'Assets')

$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object { [version]$_.Directory.Parent.Name } | Select-Object -Last 1
if (-not $makeappx) { throw 'makeappx.exe was not found. Install the Windows SDK.' }
$msix = Join-Path $out "Gili-$version.msix"
& $makeappx.FullName pack /o /d $layout /p $msix
if ($LASTEXITCODE -ne 0) { throw 'makeappx pack failed.' }
Write-Output "Package: $msix"

if (-not $Install) { return }

# A running copy (any build) releases its AppBar strip through --quit first, as in publish.ps1.
$running = @(Get-Process -Name Gili -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Start-Process -FilePath $running[0].Path -ArgumentList '--quit' -Wait
    foreach ($process in $running) {
        if (-not $process.WaitForExit(5000)) { throw 'Gili did not quit. Quit it from the bar menu and run this again.' }
    }
}
Add-AppxPackage -Register (Join-Path $layout 'AppxManifest.xml') -ForceUpdateFromAnyVersion
$family = (Get-AppxPackage -Name Gili).PackageFamilyName
Start-Process explorer.exe "shell:AppsFolder\$family!Gili"
Write-Output "Installed and started $family"
