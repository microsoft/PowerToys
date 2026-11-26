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
    $fileLocksmithPath = 'WinUI3Apps\PowerToys.FileLocksmithExt.dll'
    $fileLocksmithBatches = @($batches | Where-Object { $_.MatchedPath -contains $fileLocksmithPath })
    if ($fileLocksmithBatches.Count -ne 1 -or
        @($fileLocksmithBatches[0].MatchedPath | Where-Object { $_ -eq $fileLocksmithPath }).Count -ne 1) {
        throw "Expected one Core signing rule for '$fileLocksmithPath'."
    }

    $fileLocksmithDll = Join-Path $BuildOutputDirectory $fileLocksmithPath
    if (!(Test-Path -LiteralPath $fileLocksmithDll -PathType Leaf)) {
        throw "Required x64 signing file not found: $fileLocksmithDll"
    }

    $fileLocksmithBatch = $fileLocksmithBatches[0]
    $otherFileLocksmithPaths = @($fileLocksmithBatch.MatchedPath | Where-Object { $_ -ne $fileLocksmithPath })
    $fileLocksmithBatch.MatchedPath = @($fileLocksmithPath)
    $policy.SignBatches = $fileLocksmithBatches
    ConvertTo-Json -InputObject $policy -Depth 20 |
        Set-Content -LiteralPath (Join-Path $OutputDirectory 'ESRPSigning_core.filelocksmith.json') -Encoding utf8

    $fileLocksmithBatch.MatchedPath = $otherFileLocksmithPaths
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
