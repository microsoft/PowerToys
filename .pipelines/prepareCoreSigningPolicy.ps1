# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

param(
    [Parameter(Mandatory)]
    [string]$BuildOutputDirectory,

    [Parameter(Mandatory)]
    [string]$PolicyFile,

    [Parameter(Mandatory)]
    [string]$OutputPolicyFile
)

$ErrorActionPreference = 'Stop'

$buildOutput = (Resolve-Path -LiteralPath $BuildOutputDirectory).ProviderPath
$policy = Get-Content -LiteralPath $PolicyFile -Raw | ConvertFrom-Json -AsHashtable
$resourcePattern = '*.resources.dll'
$resourceBatches = @($policy.SignBatches | Where-Object { $_.MatchedPath -contains $resourcePattern })
if ($resourceBatches.Count -eq 0) {
    throw "Expected a '$resourcePattern' rule in the Core signing policy."
}

# The CmdPal MSIX payload is signed separately. Exclude its loose per-locale copies.
$looseCmdPalResourcePattern = '^WinUI3Apps\\CmdPal\\[a-z]{2,3}(?:-[a-z0-9]{2,8})+\\[^\\]+\.resources\.dll$'
$resourcePaths = @(Get-ChildItem -LiteralPath $buildOutput -Recurse -File -Force -Filter $resourcePattern | ForEach-Object {
    [IO.Path]::GetRelativePath($buildOutput, $_.FullName).Replace('/', '\')
})
$includedPaths = @($resourcePaths | Where-Object { $_ -notmatch $looseCmdPalResourcePattern } | Sort-Object)

foreach ($batch in $resourceBatches) {
    $batch.MatchedPath = @(foreach ($pattern in $batch.MatchedPath) {
        if ($pattern -eq $resourcePattern) { $includedPaths }
        else { $pattern }
    })
}

ConvertTo-Json -InputObject $policy -Depth 20 | Set-Content -LiteralPath $OutputPolicyFile -Encoding utf8
Write-Host "Excluded $($resourcePaths.Count - $includedPaths.Count) loose CmdPal resource DLLs from Core signing."
