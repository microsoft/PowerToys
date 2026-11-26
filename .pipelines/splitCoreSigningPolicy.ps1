# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

param(
    [Parameter(Mandatory)]
    [string]$PolicyFile,

    [Parameter(Mandatory)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

$policy = Get-Content -LiteralPath $PolicyFile -Raw | ConvertFrom-Json
$batches = $policy.SignBatches
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
