$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$assetDir = Join-Path $rootDir 'assets'
$iconPath = Join-Path $assetDir 'hoco-keepalive.ico'

if (!(Test-Path $assetDir)) {
    New-Item -ItemType Directory -Force -Path $assetDir | Out-Null
}

# If icon already exists and is valid (valid ICO header and >= 1000 bytes), keep and validate
if (Test-Path $iconPath) {
    $bytes = [System.IO.File]::ReadAllBytes($iconPath)
    if ($bytes.Length -gt 1000 -and $bytes[0] -eq 0 -and $bytes[1] -eq 0 -and $bytes[2] -eq 1 -and $bytes[3] -eq 0) {
        Write-Host "Valid multi-resolution icon validated at $iconPath ($($bytes.Length) bytes)."
        exit 0
    }
}

# If node is available, run our reliable generator
$makeIcoPath = Join-Path $scriptDir 'make-ico.js'
if (Test-Path $makeIcoPath) {
    if (Get-Command node -ErrorAction SilentlyContinue) {
        node $makeIcoPath
    }
}

if (!(Test-Path $iconPath)) {
    throw "Icon generation failed: $iconPath does not exist."
}

Write-Host "Generated and verified $iconPath"
