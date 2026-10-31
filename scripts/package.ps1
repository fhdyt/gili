<#
.SYNOPSIS
    Builds Gili's MSIX package (PRD 9.4, F-PUB-01) into artifacts/msix, and with -Install registers it on this laptop.
    -Installer builds the GitHub installer artifacts/installer/Gili-Setup-<version>.exe instead (F-PUB-03, design 5b).

.DESCRIPTION
    Publishes Gili self-contained for win-x64 into artifacts/msix/layout, adds packaging/AppxManifest.xml (with the
    version from Directory.Build.props) and packaging/Assets, then packs Gili-<version>.msix with makeappx for Partner
    Center. -Install registers the layout itself (Add-AppxPackage -Register), which needs Developer Mode but no admin
    rights and no certificate (plan 1h decision 1), then starts the package.

    -Installer publishes the same self-contained build into artifacts/installer/app and compiles packaging/gili.iss
    with Inno Setup (winget install JRSoftware.InnoSetup). It is unsigned until the OV certificate exists (PRD 9.4).

.EXAMPLE
    pwsh scripts/package.ps1 -Install

.EXAMPLE
    pwsh scripts/package.ps1 -Installer
#>
param([switch] $Install, [switch] $Installer)

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
$appVersion = $props.Project.PropertyGroup.Version
$version = "$appVersion.0"

# Gili self-contained (users have no .NET) with the NativeAOT Claude hook helper beside it.
function Publish-Gili([string] $target) {
    dotnet publish (Join-Path $root 'src/Gili.App/Gili.App.csproj') -c Release -r win-x64 --self-contained -o $target
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

    # The Claude hook helper is NativeAOT (F-CLD-07); the native linker finds MSVC through vswhere in the VS Installer folder.
    $vsInstaller = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
    if (Test-Path $vsInstaller) { $env:PATH = "$env:PATH;$vsInstaller" }
    dotnet publish (Join-Path $root 'src/Gili.ClaudeHook/Gili.ClaudeHook.csproj') -c Release -r win-x64 -o $target -p:DebugType=None -p:NativeDebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'Publishing Gili.ClaudeHook failed. It needs the C++ build tools (Desktop development with C++).' }
    Remove-Item (Join-Path $target '*.pdb') -ErrorAction SilentlyContinue
}

if ($Installer) {
    $iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") |
        Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) { throw 'Inno Setup was not found. Install it: winget install JRSoftware.InnoSetup' }
    $installerOut = Join-Path $root 'artifacts/installer'
    $app = Join-Path $installerOut 'app'
    if (Test-Path $installerOut) { Remove-Item -Recurse -Force $installerOut }
    Publish-Gili $app
    & $iscc "/DAppVersion=$appVersion" "/DSourceDir=$app" (Join-Path $root 'packaging/gili.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup could not build the installer.' }
    Write-Output "Installer: $(Join-Path $installerOut "Gili-Setup-$appVersion.exe")"
    return
}

if (Test-Path $out) { Remove-Item -Recurse -Force $out }
Publish-Gili $layout

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
