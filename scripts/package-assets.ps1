# Draws the MSIX logos in packaging/Assets from Gili the mascot: a black rounded square with two white eyes.
# Placeholder art until Phase 5 (plan 1h decision 2). Run once and commit the PNGs.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assets = Join-Path (Split-Path $PSScriptRoot -Parent) 'packaging/Assets'
New-Item -ItemType Directory -Force $assets | Out-Null

function Add-RoundRect($path, [single] $x, [single] $y, [single] $w, [single] $h, [single] $r) {
    $d = 2 * $r
    $path.AddArc($x, $y, $d, $d, 180, 90); $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
}

foreach ($logo in @(@{ Name = 'Square44x44Logo.png'; Size = 44 }, @{ Name = 'Square150x150Logo.png'; Size = 150 }, @{ Name = 'StoreLogo.png'; Size = 50 })) {
    $s = [single]$logo.Size
    $bitmap = [System.Drawing.Bitmap]::new($logo.Size, $logo.Size)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $square = [System.Drawing.Drawing2D.GraphicsPath]::new()
    Add-RoundRect $square 0 0 $s $s ($s * 0.22)
    $g.FillPath([System.Drawing.Brushes]::Black, $square)
    $eyeW = $s * 0.16; $eyeH = $s * 0.30
    $left = $s / 2 - $s * 0.13; $right = $s / 2 + $s * 0.13
    foreach ($cx in @($left, $right)) {
        $eye = [System.Drawing.Drawing2D.GraphicsPath]::new()
        Add-RoundRect $eye ($cx - $eyeW / 2) (($s - $eyeH) / 2) $eyeW $eyeH ($eyeW / 2)
        $g.FillPath([System.Drawing.Brushes]::White, $eye)
    }
    $bitmap.Save((Join-Path $assets $logo.Name), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bitmap.Dispose()
}
Get-ChildItem $assets | Select-Object Name, Length
