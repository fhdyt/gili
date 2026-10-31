# Draws the MSIX logos in packaging/Assets and packaging/Gili.ico (Gili.exe and the installer, design 5b) from Gili the
# mascot: a black rounded square with two white eyes. Placeholder art until Phase 5 (plan 1h decision 2). Run once and
# commit the results.
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

function New-Logo([int] $size) {
    $s = [single]$size
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
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
    $g.Dispose()
    $bitmap
}

foreach ($logo in @(@{ Name = 'Square44x44Logo.png'; Size = 44 }, @{ Name = 'Square150x150Logo.png'; Size = 150 }, @{ Name = 'StoreLogo.png'; Size = 50 })) {
    $bitmap = New-Logo $logo.Size
    $bitmap.Save((Join-Path $assets $logo.Name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

# An .ico of PNG images, one per size Windows asks for (Explorer, Start, Apps, the installer).
$sizes = 16, 24, 32, 48, 64, 256
$images = foreach ($size in $sizes) {
    $bitmap = New-Logo $size
    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    , $stream.ToArray()
}
$ico = [System.IO.BinaryWriter]::new([System.IO.File]::Create((Join-Path (Split-Path $assets -Parent) 'Gili.ico')))
$ico.Write([uint16]0); $ico.Write([uint16]1); $ico.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $side = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $ico.Write([byte]$side); $ico.Write([byte]$side); $ico.Write([byte]0); $ico.Write([byte]0)
    $ico.Write([uint16]1); $ico.Write([uint16]32); $ico.Write([uint32]$images[$i].Length); $ico.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($image in $images) { $ico.Write($image) }
$ico.Dispose()
Get-ChildItem $assets, (Join-Path (Split-Path $assets -Parent) 'Gili.ico') | Select-Object Name, Length
