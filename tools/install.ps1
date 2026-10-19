<#
.SYNOPSIS
    Builds upsy and installs it for the current user, or removes it.

.DESCRIPTION
    Publishes src/Upsy (Release, single file, framework-dependent) into
    %LOCALAPPDATA%\Programs\upsy, adds a Start menu shortcut and an entry in
    Settings > Apps, and starts it. No admin rights needed. Settings in
    %APPDATA%\upsy are kept on reinstall and on uninstall.

    If "Start with Windows" is on, the autostart entry is pointed at the
    installed copy. Run again to update; -Uninstall removes everything except
    the settings.

.EXAMPLE
    pwsh tools/install.ps1
.EXAMPLE
    pwsh tools/install.ps1 -Uninstall
#>
[CmdletBinding()]
param(
    [switch] $Uninstall,
    [switch] $NoStart
)

$ErrorActionPreference = 'Stop'

$Name = 'upsy'
$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\$Name"
$Exe = Join-Path $InstallDir "$Name.exe"
$Shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) "$Name.lnk"
$RunKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$UninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$Name"

function Stop-Upsy {
    $running = Get-Process -Name $Name -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host "Stopping the running $Name..."
        $running | Stop-Process -Force
        $running | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
    }
}

if ($Uninstall) {
    Stop-Upsy
    if ((Get-ItemProperty $RunKey -ErrorAction SilentlyContinue).$Name) {
        Remove-ItemProperty $RunKey -Name $Name
    }
    Remove-Item $Shortcut -ErrorAction SilentlyContinue
    Remove-Item $UninstallKey -Recurse -ErrorAction SilentlyContinue
    # The uninstaller may run from inside the install folder; remove it once this process exits.
    if ($PSScriptRoot -eq $InstallDir) {
        Start-Process cmd.exe -WindowStyle Hidden -ArgumentList "/c timeout /t 2 >nul & rmdir /s /q `"$InstallDir`""
    } else {
        Remove-Item $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Host "$Name removed. Settings stay in $env:APPDATA\$Name."
    return
}

$Root = Split-Path $PSScriptRoot -Parent
$Staging = Join-Path $Root 'out\install'

$Rid = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'win-arm64' } else { 'win-x64' }

Write-Host "Publishing ($Rid)..."
Remove-Item $Staging -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish (Join-Path $Root 'src\Upsy') -c Release -r $Rid -o $Staging `
    -p:PublishSingleFile=true -p:SelfContained=false -p:DebugType=none --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

Stop-Upsy
New-Item -ItemType Directory -Force $InstallDir | Out-Null
Get-ChildItem $InstallDir -Exclude 'uninstall.ps1' | Remove-Item -Recurse -Force
Copy-Item (Join-Path $Staging '*') $InstallDir -Recurse -Force
Copy-Item $PSCommandPath (Join-Path $InstallDir 'uninstall.ps1') -Force

$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($Shortcut)
$link.TargetPath = $Exe
$link.WorkingDirectory = $InstallDir
$link.Description = 'Bluetti EB3A UPS monitor'
$link.Save()

$version = (Get-Item $Exe).VersionInfo.ProductVersion -replace '\+.*$', ''
New-Item $UninstallKey -Force | Out-Null
$entry = @{
    DisplayName     = $Name
    DisplayVersion  = $version
    DisplayIcon     = $Exe
    Publisher       = 'upsy contributors'
    InstallLocation = $InstallDir
    UninstallString = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$InstallDir\uninstall.ps1`" -Uninstall"
}
foreach ($k in $entry.Keys) { Set-ItemProperty $UninstallKey -Name $k -Value $entry[$k] }
Set-ItemProperty $UninstallKey -Name NoModify -Value 1 -Type DWord
Set-ItemProperty $UninstallKey -Name NoRepair -Value 1 -Type DWord
$size = (Get-ChildItem $InstallDir -Recurse -File | Measure-Object Length -Sum).Sum
Set-ItemProperty $UninstallKey -Name EstimatedSize -Value ([int]($size / 1KB)) -Type DWord

# Autostart stores the exe path; move an existing entry from a build folder to the installed copy.
if ((Get-ItemProperty $RunKey -ErrorAction SilentlyContinue).$Name) {
    Set-ItemProperty $RunKey -Name $Name -Value "`"$Exe`""
}

Write-Host "$Name $version installed to $InstallDir"
if (-not $NoStart) {
    Start-Process $Exe
}
