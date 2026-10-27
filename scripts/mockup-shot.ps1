<#
.SYNOPSIS
    Renders one state of the HTML mockup to a PNG, to compare it side by side with the app.

.DESCRIPTION
    Copies docs/mockup/top-taskbar-mockup.html to a temporary folder, switches off every CSS transition and animation
    (so springs land at once), hides the mockup controls panel, presses the mockup's own buttons for the state, and
    screenshots it with headless Microsoft Edge at the screen's scale, so one mockup pixel lines up with one app pixel.

.EXAMPLE
    pwsh scripts/mockup-shot.ps1 -State media-page -Out $env:TEMP\mockup-media.png
#>
param(
    [Parameter(Mandatory)] [ValidateSet('idle', 'music', 'media-page', 'charging', 'power', 'settings')] [string] $State,
    [Parameter(Mandatory)] [string] $Out,
    [double] $Scale = 0,
    [int] $Width = 0,
    [int] $Height = 0
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
if ($Scale -le 0) {
    Add-Type -Namespace GiliShot -Name Dpi -MemberDefinition '[DllImport("user32.dll")] public static extern uint GetDpiForSystem();'
    $Scale = [GiliShot.Dpi]::GetDpiForSystem() / 96.0
}
if ($Width -le 0) { $Width = [int][math]::Round($screen.Width / $Scale) }
if ($Height -le 0) { $Height = [int][math]::Round($screen.Height / $Scale) }

$edge = @(
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw 'Microsoft Edge was not found.' }

# The mockup's own buttons for each state (see its "Mockup controls" panel).
$clicks = @{
    'idle'       = @('[data-act="idle"]')
    'music'      = @()
    'media-page' = @('[data-page="0"]')
    'charging'   = @('[data-act="idle"]', '[data-act="charge"]')
    'power'      = @('#mPower')
    'settings'   = @('#mSet')
}[$State]
$selectors = ($clicks | ForEach-Object { "'" + $_ + "'" }) -join ','

$freeze = '<style>*,*::before,*::after{transition:none!important;animation:none!important}#ctl{display:none!important}</style>'
$press = "<script>addEventListener('load',()=>setTimeout(()=>{for(const s of [$selectors]){const b=document.querySelector(s);if(b)b.click()}},300));</script>"

$source = Join-Path $PSScriptRoot '..\docs\mockup\top-taskbar-mockup.html'
$work = Join-Path ([System.IO.Path]::GetTempPath()) ('gili-mockup-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    $html = Get-Content -Raw -Encoding utf8 $source
    $html = $html.Replace('</head>', "$freeze</head>").Replace('</body>', "$press</body>")
    $page = Join-Path $work 'mockup.html'
    Set-Content -Path $page -Value $html -Encoding utf8
    $uri = ([System.Uri](Resolve-Path $page).Path).AbsoluteUri
    $target = [System.IO.Path]::GetFullPath($Out)
    & $edge --headless=new --hide-scrollbars --disable-gpu "--force-device-scale-factor=$Scale" "--window-size=$Width,$Height" `
        --virtual-time-budget=3000 "--user-data-dir=$work\profile" "--screenshot=$target" $uri 2>$null | Out-Null
    if (-not (Test-Path $target)) { throw "Edge did not write $target." }
    Write-Output $target
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
