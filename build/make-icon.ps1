<#
    Generates src/AniVault/Resources/Icons/AniVault.ico from scratch (no external tools).

    The icon is an original mark: rounded-square badge in the app accent colour with a
    stylised "A" / vault slot. Multiple PNG-compressed sizes are packed into one .ico.

    Run with Windows PowerShell (System.Drawing is available there):
        powershell -ExecutionPolicy Bypass -File build/make-icon.ps1
#>

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repoRoot 'src/AniVault/Resources/Icons'
$outFile = Join-Path $outDir 'AniVault.ico'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$accent = [System.Drawing.Color]::FromArgb(255, 108, 140, 255)   # #6C8CFF
$bg     = [System.Drawing.Color]::FromArgb(255, 18, 20, 26)      # #12141A
$ink    = [System.Drawing.Color]::FromArgb(255, 237, 239, 245)   # #EDEFF5

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [Math]::Max(1, [int]($size * 0.06))
    $rect = New-Object System.Drawing.Rectangle($pad, $pad, ($size - 2 * $pad), ($size - 2 * $pad))
    $radius = [int]($size * 0.22)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $bgBrush = New-Object System.Drawing.SolidBrush($bg)
    $g.FillPath($bgBrush, $path)

    # Accent "A" formed by two strokes + a vault slot bar.
    $stroke = [Math]::Max(2, [int]($size * 0.11))
    $penAccent = New-Object System.Drawing.Pen($accent, $stroke)
    $penAccent.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penAccent.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penAccent.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $cx = $size / 2.0
    $top = $size * 0.26
    $bottom = $size * 0.76
    $spread = $size * 0.20
    $g.DrawLine($penAccent, [single]$cx, [single]$top, [single]($cx - $spread), [single]$bottom)
    $g.DrawLine($penAccent, [single]$cx, [single]$top, [single]($cx + $spread), [single]$bottom)

    $penInk = New-Object System.Drawing.Pen($ink, [Math]::Max(2, [int]($size * 0.075)))
    $penInk.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penInk.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($penInk, [single]($cx - $spread * 0.55), [single]($size * 0.56), [single]($cx + $spread * 0.55), [single]($size * 0.56))

    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = @{}
foreach ($s in $sizes) { $pngs[$s] = New-IconPng $s }

$fs = [System.IO.File]::Create($outFile)
$bw = New-Object System.IO.BinaryWriter($fs)

# ICONDIR
$bw.Write([UInt16]0)              # reserved
$bw.Write([UInt16]1)              # type: icon
$bw.Write([UInt16]$sizes.Count)   # image count

$offset = 6 + (16 * $sizes.Count)
foreach ($s in $sizes) {
    $bytes = $pngs[$s]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([Byte]$dim)         # width
    $bw.Write([Byte]$dim)         # height
    $bw.Write([Byte]0)            # palette
    $bw.Write([Byte]0)            # reserved
    $bw.Write([UInt16]1)          # colour planes
    $bw.Write([UInt16]32)         # bits per pixel
    $bw.Write([UInt32]$bytes.Length)
    $bw.Write([UInt32]$offset)
    $offset += $bytes.Length
}
foreach ($s in $sizes) { $bw.Write($pngs[$s]) }

$bw.Flush(); $bw.Dispose(); $fs.Dispose()
Write-Host "Wrote $outFile ($([Math]::Round((Get-Item $outFile).Length / 1kb, 1)) KB)"
