# Sets up a REAL mapped network drive so you can test the "Copy as UNC path" PowerScript end-to-end
# without needing an actual file server. It creates a local folder, shares it over SMB (loopback),
# maps a free drive letter to \\localhost\<share>, and drops a sample file + folder on it.
#
# Run this from an ELEVATED PowerShell (New-SmbShare requires admin). Pair with
# teardown-network-drive.ps1 to clean everything up afterwards.
#
# Output: prints the mapped drive letter and the sample paths you can right-click in Explorer.

[CmdletBinding()]
param(
    [string]$ShareName = "PSUncTest",
    [string]$DriveLetter
)

$ErrorActionPreference = "Stop"

function Assert-Admin {
    $id = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object System.Security.Principal.WindowsPrincipal($id)
    if (-not $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "This script must be run from an elevated (Administrator) PowerShell — New-SmbShare needs admin rights."
    }
}

function Get-FreeDriveLetter {
    foreach ($c in [char[]](90..68)) {
        # Z down to E
        if (-not (Test-Path "${c}:")) { return "$c" }
    }
    throw "No free drive letter available."
}

Assert-Admin

if (-not $DriveLetter) { $DriveLetter = Get-FreeDriveLetter }
$DriveLetter = $DriveLetter.TrimEnd(':')

$backingDir = Join-Path $env:TEMP "PowerScriptsUncTest"
New-Item -ItemType Directory -Force $backingDir | Out-Null
"This file lives on a network share." | Set-Content -Path (Join-Path $backingDir "sample.txt") -Encoding UTF8
New-Item -ItemType Directory -Force (Join-Path $backingDir "subfolder") | Out-Null

# (Re)create the share.
if (Get-SmbShare -Name $ShareName -ErrorAction SilentlyContinue) {
    Remove-SmbShare -Name $ShareName -Force
}
New-SmbShare -Name $ShareName -Path $backingDir -FullAccess "$env:USERDOMAIN\$env:USERNAME" | Out-Null

# Map the drive (net use gives a genuine DRIVE_REMOTE that WNetGetUniversalName resolves).
$uncRoot = "\\localhost\$ShareName"
cmd /c "net use ${DriveLetter}: /delete /y" 2>$null | Out-Null
cmd /c "net use ${DriveLetter}: `"$uncRoot`" /persistent:no" | Out-Null

Write-Host ""
Write-Host "=== Copy as UNC test drive ready ===" -ForegroundColor Green
Write-Host "  Share:        $uncRoot  ->  $backingDir"
Write-Host "  Mapped drive: ${DriveLetter}:"
Write-Host ""
Write-Host "  Right-click these in Explorer and choose  PowerScripts > Copy as UNC path :"
Write-Host "    ${DriveLetter}:\sample.txt      (expect  $uncRoot\sample.txt)"
Write-Host "    ${DriveLetter}:\subfolder       (expect  $uncRoot\subfolder)"
Write-Host ""
Write-Host "  When finished, run:  teardown-network-drive.ps1 -ShareName $ShareName -DriveLetter $DriveLetter"
