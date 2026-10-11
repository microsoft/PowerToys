# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$InstalledRelease,
    [ValidateSet('x64', 'ARM64')][string]$Platform = 'x64',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'PowerToys')
)
$ErrorActionPreference = 'Stop'
$owner = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
if ($InstalledRelease) {
    if ([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run installed-release cleanup from the original user''s non-elevated terminal. Setup handles its own UAC.'
    }
    $setup = Join-Path $InstallRoot 'PowerToys.ProtectedStorageSetup.exe'
    $signature = Get-AuthenticodeSignature -LiteralPath $setup
    if ($signature.Status -ne 'Valid') { throw 'Installed Setup does not have a valid Authenticode signature.' }
    if ($PSCmdlet.ShouldProcess("Installed protected-storage instance for $owner", 'Remove via signed Setup; keep business data')) {
        $resultFile = Join-Path $env:TEMP "PTStorage-cleanup-$([guid]::NewGuid().ToString('N')).json"
        $process = Start-Process -FilePath $setup -ArgumentList 'remove','--keep-data','--json' -Wait -PassThru -NoNewWindow -RedirectStandardOutput $resultFile
        $text = Get-Content -LiteralPath $resultFile -Raw
        Write-Output $text
        if ($process.ExitCode -notin @(0,3010,1641)) { throw "Installed cleanup failed ($($process.ExitCode)); evidence: $resultFile" }
        Write-Host "Cleanup result retained at $resultFile. Business data was not purged."
    }
} elseif ($PSCmdlet.ShouldProcess("Isolated local-test instance for $owner", 'Remove test service/code/data only')) {
    & (Join-Path $PSScriptRoot 'Test-ProtectedStorageLocal.ps1') -Action Clean -Platform $Platform -Configuration $Configuration
}
Write-Host 'Unrelated prototype/demo services were not removed.'
