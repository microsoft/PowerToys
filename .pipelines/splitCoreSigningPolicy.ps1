# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

param(
    [Parameter(Mandatory)]
    [string]$PolicyFile,

    [Parameter(Mandatory)]
    [string]$OutputDirectory,

    [Parameter(Mandatory)]
    [string]$Platform,

    [Parameter(Mandatory)]
    [string]$BuildOutputDirectory
)

$ErrorActionPreference = 'Stop'

$policy = Get-Content -LiteralPath $PolicyFile -Raw | ConvertFrom-Json
$batches = $policy.SignBatches
if ($Platform -eq 'x64') {
    $fileLocksmithPaths = @(
        'WinUI3Apps\PowerToys.FileLocksmithExt.dll'
        'WinUI3Apps\PowerToys.FileLocksmithLib.Interop.dll'
        'WinUI3Apps\PowerToys.FileLocksmithUI.exe'
        'WinUI3Apps\PowerToys.FileLocksmithUI.dll'
    )
    $fileLocksmithBatches = @($batches | Where-Object {
        @($_.MatchedPath | Where-Object { $_ -in $fileLocksmithPaths }).Count -gt 0
    })
    if ($fileLocksmithBatches.Count -ne 1) {
        throw 'Expected one Core signing batch for the x64 File Locksmith files.'
    }

    $fileLocksmithBatch = $fileLocksmithBatches[0]
    foreach ($path in $fileLocksmithPaths) {
        if (@($fileLocksmithBatch.MatchedPath | Where-Object { $_ -eq $path }).Count -ne 1) {
            throw "Expected one Core signing rule for '$path'."
        }

        $file = Join-Path $BuildOutputDirectory $path
        if (!(Test-Path -LiteralPath $file -PathType Leaf)) {
            throw "Required x64 signing file not found: $file"
        }
    }

    $remainingBatchPaths = @($fileLocksmithBatch.MatchedPath | Where-Object { $_ -notin $fileLocksmithPaths })
    $fileLocksmithBatch.MatchedPath = $fileLocksmithPaths
    $policy.SignBatches = $fileLocksmithBatches
    ConvertTo-Json -InputObject $policy -Depth 20 |
        Set-Content -LiteralPath (Join-Path $OutputDirectory 'ESRPSigning_core.filelocksmith.json') -Encoding utf8

    $fileLocksmithBatch.MatchedPath = $remainingBatchPaths
}

$resourcePattern = '*.resources.dll'
$resourceBatches = @($batches | Where-Object { $_.MatchedPath -contains $resourcePattern })
if ($resourceBatches.Count -ne 1) {
    throw "Expected one Core signing batch containing '$resourcePattern'."
}

# Carry the original signing operations and policy metadata into each ESRP task.
$resourceBatch = $resourceBatches[0]
$otherPaths = @($resourceBatch.MatchedPath | Where-Object { $_ -ne $resourcePattern })
$resourceBatch.MatchedPath = @($resourcePattern)
$policy.SignBatches = $resourceBatches
ConvertTo-Json -InputObject $policy -Depth 20 |
    Set-Content -LiteralPath (Join-Path $OutputDirectory 'ESRPSigning_core.resources.json') -Encoding utf8

$resourceBatch.MatchedPath = $otherPaths
$policy.SignBatches = @($batches | Where-Object { $_.MatchedPath.Count -gt 0 })
ConvertTo-Json -InputObject $policy -Depth 20 |
    Set-Content -LiteralPath (Join-Path $OutputDirectory 'ESRPSigning_core.binaries.json') -Encoding utf8
