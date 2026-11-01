# Draws the MSIX logos in packaging/Assets and packaging/Gili.ico (Gili.exe, the installer and the tray icon) from Gili
# the mascot in its circle form (revision 14, issue #32): a black circle with two white rounded-square eyes, each a
# quarter of the circle across with a gap of a twelfth, like the 24 px circle beside the island. Run once and commit the
# results.
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
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.FillEllipse([System.Drawing.Brushes]::Black, 0, 0, $s, $s)
    # At 16 px a quarter is 4 px, too small to read, so the smallest icon gets slightly bigger eyes.
    $eye = $s * $(if ($size -le 16) { 0.28 } else { 0.25 })
    $gap = $s / 12
    $top = ($s - $eye) / 2
    $leftEye = ($s - $gap) / 2 - $eye
    $rightEye = ($s + $gap) / 2
    foreach ($x in @($leftEye, $rightEye)) {
        $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
        Add-RoundRect $path $x $top $eye $eye ($eye / 3)
        $g.FillPath([System.Drawing.Brushes]::White, $path)
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
