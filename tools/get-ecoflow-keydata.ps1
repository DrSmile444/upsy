<#
.SYNOPSIS
    Fetches the EcoFlow key table upsy needs for EcoFlow stations.

.DESCRIPTION
    EcoFlow stations derive their Bluetooth session key from a constant table in EcoFlow's app. It is EcoFlow's,
    so it is never committed to this repo. This script downloads it from a pinned commit of rabits/ha-ef-ble
    (eflib/keydata.py), decodes it, checks its size and SHA-256, and writes it to
    %LOCALAPPDATA%\upsy\ecoflow-keydata.bin, where upsy reads it.

.EXAMPLE
    pwsh tools/get-ecoflow-keydata.ps1
#>
[CmdletBinding()]
param(
    [string] $Destination = (Join-Path $env:LOCALAPPDATA 'upsy\ecoflow-keydata.bin')
)

$ErrorActionPreference = 'Stop'

$Commit = '1c1eb3310eb1ec1cee9cd4eab5f28a1f0f7aba83'
$Url = "https://raw.githubusercontent.com/rabits/ha-ef-ble/$Commit/custom_components/ef_ble/eflib/keydata.py"
$Length = 65280
$Sha256 = '3d1ba59b40c46bc86ba35fa231a3db196b448bbdfb1635c88ddd42f24abd4b2a'

if (Test-Path $Destination) {
    $existing = [IO.File]::ReadAllBytes($Destination)
    $hash = (Get-FileHash -Algorithm SHA256 -InputStream ([IO.MemoryStream]::new($existing))).Hash.ToLowerInvariant()
    if ($existing.Length -eq $Length -and $hash -eq $Sha256) {
        Write-Host "EcoFlow key table already present: $Destination"
        return
    }
}

Write-Host "Downloading the EcoFlow key table (ha-ef-ble $($Commit.Substring(0, 7)))..."
$source = (Invoke-WebRequest -Uri $Url -UseBasicParsing).Content

# keydata.py holds one base64 bytes literal: _data = base64.b64decode(b"\<lines ending in \>")
$match = [regex]::Match($source, 'b64decode\(\s*b"(?<b64>[^"]+)"', 'Singleline')
if (-not $match.Success) { throw "keydata.py has an unexpected format; the upstream file may have changed." }
$base64 = $match.Groups['b64'].Value -replace '\\\r?\n', '' -replace '\\', '' -replace '\s', ''
$bytes = [Convert]::FromBase64String($base64)

$hash = (Get-FileHash -Algorithm SHA256 -InputStream ([IO.MemoryStream]::new($bytes))).Hash.ToLowerInvariant()
if ($bytes.Length -ne $Length -or $hash -ne $Sha256) {
    throw "The downloaded key table does not match the expected size and SHA-256 ($($bytes.Length) bytes, $hash)."
}

New-Item -ItemType Directory -Force (Split-Path $Destination -Parent) | Out-Null
[IO.File]::WriteAllBytes($Destination, $bytes)
Write-Host "EcoFlow key table written to $Destination"
