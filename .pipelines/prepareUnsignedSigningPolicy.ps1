# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

param(
    [Parameter(Mandatory = $true)]
    [string]$FolderPath,
    [Parameter(Mandatory = $true)]
    [string]$SigningPolicyPath,
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

function Test-FileHasSignature {
    param([System.IO.FileInfo]$File)

    # Inspect the PE certificate table directly. WinTrust can prefer a machine-local
    # catalog, and neither its trust result nor a CLR strong name answers whether
    # the file already carries an Authenticode signature.
    $stream = [System.IO.File]::OpenRead($File.FullName)
    $reader = [System.IO.BinaryReader]::new($stream)
    try {
        if ($stream.Length -ge 64 -and $reader.ReadUInt16() -eq 0x5A4D) {
            $stream.Position = 0x3C
            $peOffset = $reader.ReadUInt32()
            if ($peOffset + 26 -gt $stream.Length) {
                throw "Invalid PE header: $($File.FullName)"
            }
            $stream.Position = $peOffset
            if ($reader.ReadUInt32() -ne 0x00004550) {
                throw "Invalid PE signature: $($File.FullName)"
            }
            $stream.Position = $peOffset + 20
            $optionalHeaderSize = $reader.ReadUInt16()
            $stream.Position = $peOffset + 24
            $magic = $reader.ReadUInt16()
            $directoryOffset = switch ($magic) {
                0x10B { 96 }
                0x20B { 112 }
                default { throw "Unsupported PE optional header: $($File.FullName)" }
            }
            if ($optionalHeaderSize -lt $directoryOffset + 40 -or
                $peOffset + 24 + $optionalHeaderSize -gt $stream.Length) {
                throw "Incomplete PE optional header: $($File.FullName)"
            }
            $stream.Position = $peOffset + 24 + $directoryOffset - 4
            if ($reader.ReadUInt32() -le 4) {
                return $false
            }
            $stream.Position = $peOffset + 24 + $directoryOffset + 32
            $certificateOffset = $reader.ReadUInt32()
            $certificateSize = $reader.ReadUInt32()
            # Preserve even an untrusted or damaged existing signature; the normal
            # post-signing validation is responsible for reporting invalid files.
            return $certificateOffset -ne 0 -or $certificateSize -ne 0
        }
    }
    finally {
        $reader.Dispose()
    }

    # Scripts, fonts and packages use their registered Authenticode providers.
    $signature = Get-AuthenticodeSignature -LiteralPath $File.FullName
    return $null -ne $signature.SignerCertificate
}

$root = (Get-Item -LiteralPath $FolderPath).FullName.TrimEnd('\', '/')
$policy = Get-Content -LiteralPath $SigningPolicyPath -Raw | ConvertFrom-Json
if ($policy.UseMinimatch) {
    throw 'This filter supports the existing non-minimatch batch signing policies only.'
}

$files = @(Get-ChildItem -LiteralPath $root -File -Recurse -Force)
$selected = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$batches = @(
    foreach ($batch in $policy.SignBatches) {
        $unsignedPaths = @(
            foreach ($file in $files) {
                $relativePath = $file.FullName.Substring($root.Length + 1).Replace('/', '\')
                $matches = @($batch.MatchedPath | Where-Object {
                    $pattern = $_.Replace('/', '\')
                    if ($pattern.Contains('\')) { $relativePath -like $pattern }
                    else { $file.Name -like $pattern }
                })
                if ($matches.Count -eq 0) { continue }
                if (-not $selected.Add($relativePath)) {
                    throw "File matches more than one signing batch: $relativePath"
                }
                if (Test-FileHasSignature -File $file) {
                    Write-Host "Preserving existing signature: $relativePath"
                    continue
                }
                $relativePath.Replace('\', '/')
            }
        )
        if ($unsignedPaths.Count -gt 0) {
            $batch.MatchedPath = $unsignedPaths
            $batch
        }
    }
)
$policy.SignBatches = $batches
# Exact minimatch paths keep a root-level filename from recursively matching an
# already-signed copy with the same name in another directory.
$policy.UseMinimatch = $true
$policy | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
$hasUnsignedFiles = ($batches.Count -gt 0).ToString().ToLowerInvariant()
Write-Host "##vso[task.setvariable variable=EsrpHasUnsignedSigningFiles]$hasUnsignedFiles"
