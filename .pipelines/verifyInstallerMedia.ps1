# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$MsiPath,

    [Parameter(Mandatory)]
    [string]$ManifestPath,

    [Parameter(Mandatory)]
    [string]$ExtractedFilesPath,

    [string]$WixToolsDirectory,

    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'

function Get-CabinetFileEntries {
    param([Parameter(Mandatory)][string]$Path)

    $reader = [IO.BinaryReader]::new([IO.File]::OpenRead($Path))
    try {
        if ([Text.Encoding]::ASCII.GetString($reader.ReadBytes(4)) -ne 'MSCF') {
            throw "Invalid CAB signature: $Path"
        }
        $reader.BaseStream.Position = 16
        $offset = $reader.ReadUInt32()
        $reader.BaseStream.Position = 28
        $count = $reader.ReadUInt16()
        $reader.BaseStream.Position = $offset
        for ($i = 0; $i -lt $count; $i++) {
            $size = $reader.ReadUInt32()
            $dataOffset = $reader.ReadUInt32()
            $folder = $reader.ReadUInt16()
            $null = $reader.ReadBytes(6)
            $name = [Collections.Generic.List[byte]]::new()
            while (($value = $reader.ReadByte()) -ne 0) {
                $name.Add($value)
            }
            [pscustomobject]@{
                Id = [Text.Encoding]::UTF8.GetString($name.ToArray())
                Size = $size
                Offset = $dataOffset
                Folder = $folder
            }
        }
    }
    finally {
        $reader.Dispose()
    }
}

if ([string]::IsNullOrWhiteSpace($WixToolsDirectory)) {
    $packages = $env:NUGET_PACKAGES
    if ([string]::IsNullOrWhiteSpace($packages)) {
        $packages = Join-Path $HOME '.nuget\packages'
    }
    $WixToolsDirectory = Join-Path $packages 'wixtoolset.sdk\5.0.2\tools\net6.0'
}
Add-Type -Path (Join-Path $WixToolsDirectory 'WixToolset.Dtf.WindowsInstaller.dll')

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
if (@($manifest.Files).Count -eq 0) {
    throw 'Installer media manifest does not contain any harvested files.'
}
$msi = (Resolve-Path -LiteralPath $MsiPath).Path
$temporaryDirectory = Join-Path ([IO.Path]::GetDirectoryName($msi)) ("media-check-" + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $temporaryDirectory
$database = $null
try {
    $database = [WixToolset.Dtf.WindowsInstaller.Database]::new(
        $msi, [WixToolset.Dtf.WindowsInstaller.DatabaseOpenMode]::ReadOnly)
    $files = @{}
    $view = $database.OpenView('SELECT `File`, `Component_`, `FileSize`, `Attributes`, `Sequence` FROM `File`')
    try {
        $view.Execute()
        while ($record = $view.Fetch()) {
            try {
                $files[$record.GetString(1)] = [pscustomobject]@{
                    Component = $record.GetString(2)
                    Size = $record.GetInteger(3)
                    Attributes = $record.GetInteger(4)
                    Sequence = $record.GetInteger(5)
                }
            }
            finally { $record.Dispose() }
        }
    }
    finally { $view.Dispose() }

    if ($files.ContainsKey('WinUI3Apps_hardlinks_txt')) {
        throw 'The legacy runtime-copy manifest is still installed.'
    }
    if ($database.Tables.Contains('CustomAction')) {
        $actions = $database.ExecuteStringQuery('SELECT `Action` FROM `CustomAction`')
        if (@($actions | Where-Object { $_ -match 'WinAppSDKHardlinks' }).Count -gt 0) {
            throw 'The legacy runtime copy/delete actions are still authored.'
        }
    }
    if ($database.Tables.Contains('DuplicateFile') -and
        $database.ExecuteStringQuery('SELECT `FileKey` FROM `DuplicateFile`').Count -gt 0) {
        throw 'Smart Cabbing must not use install-time DuplicateFile entries.'
    }

    $media = [Collections.Generic.List[object]]::new()
    $view = $database.OpenView('SELECT `DiskId`, `LastSequence`, `Cabinet` FROM `Media`')
    try {
        $view.Execute()
        while ($record = $view.Fetch()) {
            try {
                $media.Add([pscustomobject]@{
                    Id = $record.GetInteger(1)
                    LastSequence = $record.GetInteger(2)
                    Cabinet = $record.GetString(3)
                })
            }
            finally { $record.Dispose() }
        }
    }
    finally { $view.Dispose() }

    $cabinetFiles = @{}
    $cabinetBytes = 0L
    foreach ($disk in ($media | Sort-Object Id)) {
        if (-not $disk.Cabinet.StartsWith('#')) {
            throw "Expected an embedded cabinet for media $($disk.Id)."
        }
        $streamName = $disk.Cabinet.Substring(1)
        $path = Join-Path $temporaryDirectory "$($disk.Id).cab"
        $view = $database.OpenView('SELECT `Data` FROM `_Streams` WHERE `Name` = ?')
        $parameter = [WixToolset.Dtf.WindowsInstaller.Record]::new(1)
        try {
            $parameter.SetString(1, $streamName)
            $view.Execute($parameter)
            $record = $view.Fetch()
            if ($null -eq $record) { throw "Missing cabinet stream: $streamName" }
            try { $record.GetStream(1, $path) }
            finally { $record.Dispose() }
        }
        finally {
            $parameter.Dispose()
            $view.Dispose()
        }
        $cabinetBytes += (Get-Item -LiteralPath $path).Length
        foreach ($entry in Get-CabinetFileEntries -Path $path) {
            $cabinetFiles[$entry.Id] = [pscustomobject]@{
                DiskId = $disk.Id
                Size = $entry.Size
                Location = "$($disk.Id):$($entry.Folder):$($entry.Offset):$($entry.Size)"
            }
        }
    }

    $orderedMedia = @($media | Sort-Object LastSequence)
    foreach ($expected in $manifest.Files) {
        $actual = $files[$expected.Id]
        $cabinetFile = $cabinetFiles[$expected.Id]
        if ($null -eq $actual -or $null -eq $cabinetFile) {
            throw "Missing MSI File row or cabinet entry: $($expected.Id)"
        }
        $disk = $orderedMedia | Where-Object { $_.LastSequence -ge $actual.Sequence } | Select-Object -First 1
        if ($actual.Component -ne $expected.Component -or $actual.Size -ne $expected.Size -or
            ($actual.Attributes -band 512) -eq 0 -or $disk.Id -ne $expected.DiskId -or
            $cabinetFile.DiskId -ne $expected.DiskId -or $cabinetFile.Size -ne $expected.Size) {
            throw "MSI ownership or media assignment mismatch: $($expected.Id)"
        }
        $extractedFile = Join-Path $ExtractedFilesPath $expected.Id
        if ((Get-FileHash -LiteralPath $extractedFile -Algorithm SHA256).Hash -ne $expected.Hash) {
            throw "Extracted MSI payload mismatch: $($expected.Id)"
        }
    }

    $duplicateGroups = @($manifest.Files | Group-Object Hash | Where-Object Count -GT 1)
    foreach ($group in $duplicateGroups) {
        $locations = @($group.Group | ForEach-Object { $cabinetFiles[$_.Id].Location } | Select-Object -Unique)
        if ($locations.Count -ne 1) {
            throw "Identical payloads were not deduplicated in one cabinet: $($group.Group.Id -join ', ')"
        }
    }

    $result = [pscustomobject]@{
        Msi = [IO.Path]::GetFileName($msi)
        MsiBytes = (Get-Item -LiteralPath $msi).Length
        CabinetBytes = $cabinetBytes
        CabinetCount = $media.Count
        VerifiedFiles = @($manifest.Files).Count
        DeduplicatedGroups = $duplicateGroups.Count
        PayloadHashesMatch = $true
    }
    if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
        $result | ConvertTo-Json | Set-Content -LiteralPath $ReportPath -Encoding UTF8
    }
    $result
}
finally {
    if ($null -ne $database) { $database.Dispose() }
    Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
}
