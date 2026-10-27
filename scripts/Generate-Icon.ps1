$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$assetDir = Join-Path $rootDir 'assets'
$iconPath = Join-Path $assetDir 'hoco-keepalive.ico'
$generator = Join-Path $scriptDir 'make-ico.js'

New-Item -ItemType Directory -Force -Path $assetDir | Out-Null
if (!(Test-Path $generator)) { throw "Icon generator is missing: $generator" }
if (!(Get-Command node -ErrorAction SilentlyContinue)) { throw "Node.js is required to generate the application icon." }

# Regenerate on every CI build to prevent stale icons from shipping.
Push-Location $rootDir
try {
    node $generator
    if ($LASTEXITCODE -ne 0) { throw "Icon generator exited with code $LASTEXITCODE." }
} finally {
    Pop-Location
}

if (!(Test-Path $iconPath)) { throw "Icon generation failed: $iconPath does not exist." }
$bytes = [System.IO.File]::ReadAllBytes($iconPath)
if ($bytes.Length -lt 1000 -or $bytes[0] -ne 0 -or $bytes[1] -ne 0 -or $bytes[2] -ne 1 -or $bytes[3] -ne 0) {
    throw "Generated icon is not a valid ICO file."
}
$count = [BitConverter]::ToUInt16($bytes, 4)
if ($count -lt 1 -or $bytes.Length -lt (6 + 16 * $count)) { throw "Generated ICO directory is invalid." }
for ($i = 0; $i -lt $count; $i++) {
    $entry = 6 + 16 * $i
    $length = [BitConverter]::ToUInt32($bytes, $entry + 8)
    $offset = [BitConverter]::ToUInt32($bytes, $entry + 12)
    if ($length -eq 0 -or $offset -lt (6 + 16 * $count) -or ([long]$offset + [long]$length) -gt $bytes.Length) {
        throw "Generated ICO contains an invalid image entry at index $i."
    }
}
Write-Host "Generated and validated $count icon frames ($($bytes.Length) bytes): $iconPath"
