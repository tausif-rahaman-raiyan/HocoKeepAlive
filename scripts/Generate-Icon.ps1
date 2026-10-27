$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Generate a clean headphone-style app icon at build time.
# The supplied reference uses a black background, pale earbuds and bold black details.
$assetDir = Join-Path $PSScriptRoot '..\assets'
New-Item -ItemType Directory -Force -Path $assetDir | Out-Null
$iconPath = Join-Path $assetDir 'hoco-keepalive.ico'
$bmp = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Black)

$earbudFill = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(245, 247, 255))
$earbudShade = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(218, 225, 248))
$black = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::Black)

# Two rounded earbuds inspired by the provided reference image.
$g.FillEllipse($earbudFill, 20, 16, 126, 126)
$g.FillEllipse($earbudShade, 36, 24, 96, 112)
$g.FillEllipse($earbudFill, 112, 104, 126, 126)
$g.FillEllipse($earbudShade, 128, 112, 96, 112)

# Rounded stems
$g.FillRectangle($earbudFill, 56, 141, 31, 34)
$g.FillEllipse($earbudFill, 56, 126, 31, 31)
$g.FillEllipse($earbudFill, 56, 160, 31, 31)
$g.FillRectangle($earbudFill, 170, 220, 31, 20)
$g.FillEllipse($earbudFill, 170, 207, 31, 31)

# Face marks and smiles
$g.FillEllipse($black, 40, 65, 13, 27)
$g.FillEllipse($black, 111, 65, 13, 27)
$g.FillEllipse($black, 141, 151, 13, 27)
$g.FillEllipse($black, 210, 151, 13, 27)
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::Black), 12
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawArc($pen, 59, 69, 57, 38, 15, 150)
$g.DrawArc($pen, 157, 150, 57, 38, 15, 150)

$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)
$file = [System.IO.File]::Create($iconPath)
$icon.Save($file)
$file.Dispose()
$icon.Dispose()
$bmp.Dispose()
$g.Dispose()
$earbudFill.Dispose()
$earbudShade.Dispose()
$black.Dispose()
$pen.Dispose()
if (!(Test-Path $iconPath)) { throw 'Icon generation failed.' }
Write-Host "Generated $iconPath"
