# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

function Get-WinAppCliRelease {
    param([ValidateSet('x64', 'arm64')][string] $Platform = 'x64')

    $Platform = $Platform.ToLowerInvariant()
    $pin = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'winapp-cli\release.json') -Raw | ConvertFrom-Json
    $asset = $pin.Assets.$Platform
    if ($pin.Repository -cne 'https://github.com/microsoft/winappCli' -or
        $pin.Version -notmatch '^\d+\.\d+\.\d+$' -or $pin.Tag -cne "v$($pin.Version)" -or
        $asset.Name -cne "winappcli-$Platform.zip" -or $asset.Sha256 -notmatch '^[0-9a-f]{64}$') {
        throw 'The winapp CLI release pin must identify an official versioned architecture asset and SHA256.'
    }
    [pscustomobject]@{
        Repository = $pin.Repository; Tag = $pin.Tag; Version = $pin.Version
        Platform = $Platform.ToLowerInvariant(); Asset = $asset.Name; Sha256 = $asset.Sha256
        DownloadUrl = "$($pin.Repository)/releases/download/$($pin.Tag)/$($asset.Name)"
        Files = @($pin.Files); ArchiveFiles = @($pin.ArchiveFiles)
    }
}

function Assert-WinAppCliNativeFile {
    param([string] $Path, [ValidateSet('x64', 'arm64')][string] $Platform = 'x64')

    # Reject managed, emulated and ARM64EC substitutes for the released NativeAOT closure.
    $expectedMachine = if ($Platform -eq 'arm64') { 0xAA64 } else { 0x8664 }
    $stream = [IO.File]::OpenRead($Path)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        if ($reader.ReadUInt16() -ne 0x5A4D) { throw 'winapp CLI output is not a PE image.' }
        $stream.Position = 0x3C
        $header = $reader.ReadInt32()
        if ($header -lt 64 -or $header + 264 -gt $stream.Length) { throw 'winapp CLI output has an invalid PE header.' }
        $stream.Position = $header
        if ($reader.ReadUInt32() -ne 0x4550 -or $reader.ReadUInt16() -ne $expectedMachine) {
            throw "winapp CLI output is not native $Platform."
        }
        $stream.Position = $header + 24
        if ($reader.ReadUInt16() -ne 0x20B) { throw 'winapp CLI output is not PE32+.' }
        $stream.Position = $header + 24 + 112 + (14 * 8)
        if ($reader.ReadUInt64() -ne 0) { throw 'winapp CLI output unexpectedly requires a managed runtime.' }
    }
    finally { $reader.Dispose() }
}

function Get-WinAppCliFileVersion {
    param([string] $Path)

    [Diagnostics.FileVersionInfo]::GetVersionInfo($Path).ProductVersion
}

function Expand-WinAppCliFile {
    param([IO.Compression.ZipArchiveEntry] $Entry, [string] $Destination)

    [IO.Compression.ZipFileExtensions]::ExtractToFile($Entry, $Destination, $false)
}

function Save-WinAppCliRelease {
    param(
        [Parameter(Mandatory)][string] $Destination,
        [Parameter(Mandatory)][string] $WorkRoot,
        [ValidateSet('x64', 'arm64')][string] $Platform = 'x64',
        [string] $ArchivePath
    )

    $ErrorActionPreference = 'Stop'
    $pin = Get-WinAppCliRelease -Platform $Platform
    if (Test-Path -LiteralPath $Destination) { throw 'winapp CLI extraction requires a new destination directory.' }
    if (-not $ArchivePath) {
        $null = New-Item -ItemType Directory -Path $WorkRoot -Force
        $ArchivePath = Join-Path $WorkRoot $pin.Asset
        Invoke-WebRequest -Uri $pin.DownloadUrl -OutFile $ArchivePath -TimeoutSec 300 -ErrorAction Stop
    }
    $hash = (Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256 -ErrorAction Stop).Hash
    if ($hash -ine $pin.Sha256) {
        throw "$($pin.Asset) SHA256 mismatch: $hash (expected $($pin.Sha256))."
    }

    $zip = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    $created = $false
    try {
        $entries = @{}
        foreach ($entry in $zip.Entries) {
            # Official archives are flat. Reject path escapes, duplicates, links and new
            # payloads rather than silently deploying a changed release layout.
            if ($entry.FullName -cnotin $pin.ArchiveFiles -or $entries.ContainsKey($entry.FullName) -or
                (($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) {
                throw 'The winapp CLI release archive contains an unexpected, duplicate, or symbolic-link file.'
            }
            $entries[$entry.FullName] = $entry
        }
        if ($entries.Count -ne $pin.ArchiveFiles.Count) {
            throw 'The winapp CLI release archive file inventory is incomplete.'
        }
        $null = New-Item -ItemType Directory -Path $Destination -ErrorAction Stop
        $created = $true
        $files = foreach ($name in $pin.Files) {
            $path = Join-Path $Destination $name
            Expand-WinAppCliFile $entries[$name] $path
            Assert-WinAppCliNativeFile $path -Platform $Platform
            Unblock-File -LiteralPath $path -ErrorAction Stop
            [ordered]@{ Path = $name; Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
        }
        $version = Get-WinAppCliFileVersion (Join-Path $Destination 'winapp.exe')
        if ($version -cnotmatch ('^' + [regex]::Escape($pin.Version) + '(?:\+[0-9a-f]+)?$')) {
            throw "The winapp CLI release version does not match $($pin.Version): $version."
        }
        [ordered]@{
            Repository = $pin.Repository; Tag = $pin.Tag; Version = $pin.Version
            Asset = $pin.Asset; Sha256 = $hash; Files = @($files)
        }
    }
    catch {
        if ($created) { Remove-Item -LiteralPath $Destination -Recurse -Force }
        throw
    }
    finally { $zip.Dispose() }
}
