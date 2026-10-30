# Copyright (c) Microsoft Corporation
# Licensed under the MIT license.

[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('x64', 'ARM64')][string]$Architecture,
    [Parameter(Mandatory)][string]$CacheDirectory
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1') -ErrorAction Stop
$dependency = ([xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Mxc.Runtime.targets') -Raw)).Project.PropertyGroup
$cache = [IO.Path]::GetFullPath($CacheDirectory)
$archivePath = Join-Path $cache ($dependency.MxcVersion + '.zip')
$destination = Join-Path $cache ($dependency.MxcVersion + '\' + $Architecture.ToLowerInvariant())
$mutex = [Threading.Mutex]::new($false, ('Local\PowerToys.PowerScripts.Mxc.' + $dependency.MxcArchiveSha256))
$locked = $false

function Restore-VerifiedArchive([string]$Path, [string]$Url, [string]$Sha256) {
    if (-not (Test-Path -LiteralPath $Path)) {
        $download = $Path + '.' + [Guid]::NewGuid().ToString('N') + '.download'
        try {
            Write-Host "Restoring MXC dependency from $Url"
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $download
            if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $Sha256) {
                throw 'Downloaded MXC dependency failed SHA-256 verification.'
            }
            Move-Item -LiteralPath $download -Destination $Path
        } finally {
            if (Test-Path -LiteralPath $download) { Remove-Item -LiteralPath $download }
        }
    }
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Sha256) {
        throw "Cached MXC dependency failed SHA-256 verification. Remove '$Path' and rebuild."
    }
}

function Restore-Entry($Archive, [string]$EntryPath, [string]$Path) {
    $entry = $Archive.GetEntry($EntryPath)
    if ($null -eq $entry) { throw "MXC dependency is missing $EntryPath." }
    $temporary = $Path + '.restore'
    try {
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $temporary, $true)
        if (-not (Test-Path -LiteralPath $Path) -or
            (Get-FileHash -LiteralPath $temporary).Hash -ne (Get-FileHash -LiteralPath $Path).Hash) {
            Move-Item -LiteralPath $temporary -Destination $Path -Force
        }
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
    }
}

try {
    try {
        $locked = $mutex.WaitOne([TimeSpan]::FromMinutes(10))
    } catch [Threading.AbandonedMutexException] {
        $locked = $true
        Write-Warning 'A previous MXC restore was interrupted; revalidating its archive.'
    }
    if (-not $locked) { throw 'Timed out waiting for the MXC dependency restore.' }

    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Restore-VerifiedArchive $archivePath $dependency.MxcArchiveUrl $dependency.MxcArchiveSha256

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        # Only the WSLC daemon is needed for the shared managed-Linux session.
        foreach ($file in @(
            @{ Name = 'wxc-exec.exe'; Entry = 'wxc-exec.exe' },
            @{ Name = 'wxc-wslc-daemon.exe'; Entry = 'wxc-wslc-daemon.exe' },
            @{ Name = 'plm.exe'; Entry = 'plm.exe' },
            @{ Name = 'Mxc.spdx.json'; Entry = '_manifest/spdx_2.2/manifest.spdx.json' }
        )) {
            $entryPath = $Architecture.ToLowerInvariant() + '/' + $file.Entry
            Restore-Entry $archive $entryPath (Join-Path $destination $file.Name)
        }
    } finally {
        $archive.Dispose()
    }

    # The WSLC SDK is loaded only by the Linux backend; the Windows executor remains pinned.
    $sdkPath = Join-Path $cache ('wslc-' + $dependency.MxcWslcSdkVersion + '.nupkg')
    Restore-VerifiedArchive $sdkPath $dependency.MxcWslcSdkUrl $dependency.MxcWslcSdkSha256
    $sdk = [IO.Compression.ZipFile]::OpenRead($sdkPath)
    try {
        Restore-Entry $sdk ('runtimes/win-' + $Architecture.ToLowerInvariant() + '/native/wslcsdk.dll') (Join-Path $destination 'wslcsdk.dll')
        Restore-Entry $sdk 'NOTICE.txt' (Join-Path $destination 'Wslc.NOTICE.txt')
    } finally {
        $sdk.Dispose()
    }
} finally {
    if ($locked) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
