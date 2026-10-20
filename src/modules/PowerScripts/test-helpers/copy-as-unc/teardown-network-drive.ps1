# Tears down the test mapped network drive and SMB share created by setup-network-drive.ps1.
# Run from an ELEVATED PowerShell.

[CmdletBinding()]
param(
    [string]$ShareName = "PSUncTest",
    [string]$DriveLetter = "Z"
)

$DriveLetter = $DriveLetter.TrimEnd(':')

cmd /c "net use ${DriveLetter}: /delete /y" 2>$null | Out-Null

if (Get-SmbShare -Name $ShareName -ErrorAction SilentlyContinue) {
    Remove-SmbShare -Name $ShareName -Force
}

$backingDir = Join-Path $env:TEMP "PowerScriptsUncTest"
if (Test-Path $backingDir) {
    Remove-Item $backingDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Copy as UNC test drive and share removed." -ForegroundColor Green
