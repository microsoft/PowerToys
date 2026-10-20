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
try {
    try {
        $locked = $mutex.WaitOne([TimeSpan]::FromMinutes(10))
    } catch [Threading.AbandonedMutexException] {
        $locked = $true
        Write-Warning 'A previous MXC restore was interrupted; revalidating its archive.'
    }
    if (-not $locked) { throw 'Timed out waiting for the MXC dependency restore.' }

    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    if (-not (Test-Path -LiteralPath $archivePath)) {
        $download = $archivePath + '.' + [Guid]::NewGuid().ToString('N') + '.download'
        try {
            Write-Host "Restoring MXC $($dependency.MxcVersion) from $($dependency.MxcArchiveUrl)"
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -UseBasicParsing -Uri $dependency.MxcArchiveUrl -OutFile $download
            if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $dependency.MxcArchiveSha256) {
                throw 'Downloaded MXC archive failed SHA-256 verification.'
            }
            Move-Item -LiteralPath $download -Destination $archivePath
        } finally {
            if (Test-Path -LiteralPath $download) { Remove-Item -LiteralPath $download }
        }
    }
    if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $dependency.MxcArchiveSha256) {
        throw "Cached MXC archive failed SHA-256 verification. Remove '$archivePath' and rebuild."
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        # Only the ProcessContainer runtime and its diagnostic sidecar; no elevated host-prep tools.
        foreach ($file in @(
            @{ Name = 'wxc-exec.exe'; Entry = 'wxc-exec.exe' },
            @{ Name = 'plm.exe'; Entry = 'plm.exe' },
            @{ Name = 'Mxc.spdx.json'; Entry = '_manifest/spdx_2.2/manifest.spdx.json' }
        )) {
            $entryPath = $Architecture.ToLowerInvariant() + '/' + $file.Entry
            $entry = $archive.GetEntry($entryPath)
            if ($null -eq $entry) { throw "MXC archive is missing $entryPath." }
            $path = Join-Path $destination $file.Name
            $temporary = $path + '.restore'
            try {
                [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $temporary, $true)
                if (-not (Test-Path -LiteralPath $path) -or
                    (Get-FileHash -LiteralPath $temporary).Hash -ne (Get-FileHash -LiteralPath $path).Hash) {
                    Move-Item -LiteralPath $temporary -Destination $path -Force
                }
            } finally {
                if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
            }
        }
    } finally {
        $archive.Dispose()
    }
} finally {
    if ($locked) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
