$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDir = Join-Path $PSScriptRoot '..\assets'
New-Item -ItemType Directory -Force -Path $assetDir | Out-Null
$iconPath = Join-Path $assetDir 'hoco-keepalive.ico'
$bmp = New-Object System.Drawing.Bitmap 64, 64
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)
$bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 18, 28, 48))
$g.FillEllipse($bg, 2, 2, 60, 60)
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 64, 210, 255)), 4
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
# Stylized Bluetooth rune
$g.DrawLine($pen, 32, 12, 32, 52)
$g.DrawLine($pen, 32, 12, 45, 24)
$g.DrawLine($pen, 45, 24, 20, 44)
$g.DrawLine($pen, 32, 52, 45, 40)
$g.DrawLine($pen, 45, 40, 20, 20)
$g.Dispose()
$bg.Dispose()
$pen.Dispose()
$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)
$file = [System.IO.File]::Create($iconPath)
$icon.Save($file)
$file.Dispose()
$icon.Dispose()
$bmp.Dispose()
Write-Host "Generated $iconPath"
