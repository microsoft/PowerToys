# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#requires -Version 7.0

[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$OutputDirectory)

$ErrorActionPreference = 'Stop'

# Pinned Microsoft WinDbg bundle; only download its stored x64 MSIX member.
# This is diagnostic tooling, not an installed package or a product dependency.
$uri = 'https://windbg.download.prss.microsoft.com/dbazure/prod/1-2606-22001-0/windbg.msixbundle'
$offset = 394595125L
$length = 399789162L
$expectedHash = 'AE309D63724C72B9918ECC72F94A594E6DBFA4631757A7138943AC3367767AE0'

function Assert-NativeDebuggerArchive {
    param([string]$Path)
    if ((Get-Item -LiteralPath $Path).Length -ne $length -or
        (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $expectedHash) {
        throw 'The pinned Microsoft debugger archive length or SHA256 does not match.'
    }
}

function Get-NativeDebuggerEntryPath {
    param([string]$Root, [string]$Name)
    $destination = [IO.Path]::GetFullPath((Join-Path $Root $Name.Replace('/', '\')))
    if (-not $destination.StartsWith($Root.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Debugger archive entry would escape the extraction directory.'
    }
    return $destination
}

function Get-NativeTestDebugger {
    param([string]$Directory)
    if (-not [IO.Path]::IsPathFullyQualified($Directory)) {
        throw 'A fully qualified diagnostic-tool output directory is required.'
    }
    $directory = [IO.Path]::GetFullPath($Directory)
    if (Test-Path -LiteralPath $directory) {
        throw 'Use a fresh output directory for the pinned diagnostic tools.'
    }
    $null = New-Item -ItemType Directory -Path $directory
    $package = Join-Path $directory 'windbg-x64.msix'
    $client = [Net.Http.HttpClient]::new()
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get, $uri)
    $request.Headers.Range = [Net.Http.Headers.RangeHeaderValue]::new($offset, $offset + $length - 1)
    $timeout = [Threading.CancellationTokenSource]::new([TimeSpan]::FromMinutes(5))
    $response = $null
    try {
        $response = $client.SendAsync($request, [Net.Http.HttpCompletionOption]::ResponseHeadersRead, $timeout.Token).GetAwaiter().GetResult()
        $range = $response.Content.Headers.ContentRange
        if ([int]$response.StatusCode -ne 206 -or -not $range -or
            $range.From -ne $offset -or $range.To -ne ($offset + $length - 1)) {
            throw 'Microsoft debugger download did not return the requested bounded archive member.'
        }
        $output = [IO.File]::Create($package)
        try {
            $response.Content.CopyToAsync($output, $timeout.Token).GetAwaiter().GetResult() | Out-Null
        }
        finally { $output.Dispose() }
    }
    finally {
        if ($response) { $response.Dispose() }
        $timeout.Dispose()
        $request.Dispose()
        $client.Dispose()
    }

    Assert-NativeDebuggerArchive $package
    $archive = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        foreach ($entry in $archive.Entries) {
            if (-not $entry.FullName.StartsWith('amd64/', [StringComparison]::Ordinal) -or $entry.Name.Length -eq 0) { continue }
            $destination = Get-NativeDebuggerEntryPath $directory $entry.FullName
            $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination)
        }
    }
    finally { $archive.Dispose() }

    $debugger = Join-Path $directory 'amd64\cdb.exe'
    $signature = Get-AuthenticodeSignature -LiteralPath $debugger
    if ($signature.Status -ne 'Valid' -or -not $signature.SignerCertificate -or
        $signature.SignerCertificate.Subject -notmatch '(^|,\s*)O=Microsoft Corporation(,|$)') {
        throw 'The extracted CDB executable does not have a valid Microsoft signature.'
    }
    Remove-Item -LiteralPath $package
    return $debugger
}

if ($MyInvocation.InvocationName -ne '.') {
    Get-NativeTestDebugger $OutputDirectory
}
