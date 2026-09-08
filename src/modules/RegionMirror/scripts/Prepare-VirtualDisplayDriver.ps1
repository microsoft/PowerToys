# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

<#
.SYNOPSIS
Prepares the pinned external driver package used by the local RegionMirror PoC.
.DESCRIPTION
Downloads and verifies the x64 package into this module's artifacts directory.
Only an explicit -Stage adds the signed INF to the Windows driver store. It does
not create a display device, edit shared VDD settings, change certificate stores,
enable test signing, or disable Windows security features.
#>
[CmdletBinding()]
param(
    [switch] $Stage
)

$ErrorActionPreference = 'Stop'
$release = '25.7.23'
$downloadUrl = 'https://github.com/VirtualDrivers/Virtual-Display-Driver/releases/download/25.7.23/VirtualDisplayDriver-x86.Driver.Only.zip'
$expectedSha256 = 'e24210692b442b39af763536330ce78b423f19342b7a7792c26de3944e418b3a'
$artifactDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\artifacts\virtual-display-driver\$release"))
$zipPath = Join-Path $artifactDirectory 'VirtualDisplayDriver-x86.Driver.Only.zip'
$packageDirectory = Join-Path $artifactDirectory 'VirtualDisplayDriver'

if (-not [Environment]::Is64BitOperatingSystem -or $env:PROCESSOR_ARCHITECTURE -eq 'ARM64' -or $env:PROCESSOR_ARCHITEW6432 -eq 'ARM64') {
    throw 'This pinned PoC package supports Windows x64 only.'
}

New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) {
    Write-Host "Downloading Virtual Display Driver $release..."
    Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath -UseBasicParsing
}

$actualSha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
if ($actualSha256 -ne $expectedSha256) {
    throw "The package SHA-256 does not match the pinned release. Stopped without staging. File: $zipPath"
}

Expand-Archive -LiteralPath $zipPath -DestinationPath $artifactDirectory -Force
$infPath = Join-Path $packageDirectory 'MttVDD.inf'
$catalogPath = Join-Path $packageDirectory 'mttvdd.cat'
$binaryPath = Join-Path $packageDirectory 'MttVDD.dll'
foreach ($filePath in @($infPath, $catalogPath, $binaryPath)) {
    if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
        throw "The verified package is missing an expected file: $filePath"
    }
}

$catalogSignature = Get-AuthenticodeSignature -LiteralPath $catalogPath
if ($catalogSignature.Status -ne 'Valid') {
    throw "Windows did not validate the driver catalog signature ($($catalogSignature.Status)). Stopped without changing trust or security settings. $($catalogSignature.StatusMessage)"
}
$binarySignature = Get-AuthenticodeSignature -LiteralPath $binaryPath
if ($binarySignature.Status -ne 'Valid') {
    throw "Windows did not validate the driver binary signature ($($binarySignature.Status)). Stopped without changing trust or security settings. $($binarySignature.StatusMessage)"
}

$infText = Get-Content -LiteralPath $infPath -Raw
if ($infText -notmatch '\[Standard\.NTamd64\]' -or $infText -notmatch '(?m)^%DeviceName%=MyDevice_Install, MttVDD\s') {
    throw 'The verified package does not expose the expected x64 MttVDD software-device hardware ID.'
}

Write-Host "Prepared package: $packageDirectory"
Write-Host "SHA-256: $expectedSha256"
Write-Host "Catalog signer: $($catalogSignature.SignerCertificate.Subject)"
Write-Host 'Shared driver settings are not copied; RegionMirror uses the built-in one-monitor defaults.'

if ($Stage) {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Staging requires an administrator PowerShell session. Re-run this script in that session with -Stage.'
    }

    # Deliberately omit /install: only stage the package, with no root/device creation.
    & "$env:SystemRoot\System32\pnputil.exe" /add-driver $infPath
    $stageExitCode = $LASTEXITCODE
    if ($stageExitCode -ne 0 -and $stageExitCode -ne 3010) {
        throw "PnPUtil failed with exit code $stageExitCode. No security settings were changed."
    }
    if ($stageExitCode -eq 3010) {
        Write-Host 'Windows reports that a restart is required to finish staging.'
    }
    Write-Host 'Driver package staged. No display device was created. Launch RegionMirror as administrator to create its temporary display.'
} else {
    Write-Host 'No driver was staged. Use -Stage explicitly from an administrator PowerShell session when ready.'
}
