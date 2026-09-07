<#
.SYNOPSIS
    Creates the auditable manifest for a generated PowerToys preview release.

.EXAMPLE
    .\new-preview-release-manifest.ps1 -ContextPath .\release-context.json -PreviousReleasePath .\previous-release.json -DeltaDirectory . -OutputPath .\release-manifest.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ContextPath,
    [Parameter(Mandatory)][string]$PreviousReleasePath,
    [Parameter(Mandatory)][string]$DeltaDirectory,
    [Parameter(Mandatory)][string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$context = Get-Content -LiteralPath $ContextPath -Raw | ConvertFrom-Json
$baseline = Get-Content -LiteralPath $PreviousReleasePath -Raw | ConvertFrom-Json
$commitDelta = Get-Content -LiteralPath (Join-Path $DeltaDirectory "delta-commits.json") -Raw | ConvertFrom-Json
$added = @(Get-Content -LiteralPath (Join-Path $DeltaDirectory "delta-prs.json") -Raw | ConvertFrom-Json)
$removed = @(Get-Content -LiteralPath (Join-Path $DeltaDirectory "removed-prs.json") -Raw | ConvertFrom-Json)
$unattributed = @(Get-Content -LiteralPath (Join-Path $DeltaDirectory "unattributed-commits.json") -Raw | ConvertFrom-Json)

$manifest = [ordered]@{
    schemaVersion = 1
    tag = [string]$context.tag
    releaseKind = "preview"
    buildId = [int]$context.buildId
    definitionId = [int]$context.definitionId
    buildUrl = [string]$context.buildUrl
    buildIntent = [string]$context.intent
    buildChannel = [string]$context.channel
    buildShouldPublishPreview = [bool]$context.shouldPublishPreview
    sourceBranch = [string]$context.sourceBranch
    sourceCommit = [string]$context.sourceCommit
    previousReleaseTag = [string]$baseline.tag
    previousSourceBranch = if ($baseline.sourceBranch) { [string]$baseline.sourceBranch } else { $null }
    previousSourceCommit = [string]$baseline.sourceCommit
    deltaMode = [string]$commitDelta.deltaMode
    mergeBase = if ($commitDelta.mergeBase) { [string]$commitDelta.mergeBase } else { $null }
    addedPrNumbers = @($added | ForEach-Object { [int]$_.number })
    removedPrNumbers = @($removed | ForEach-Object { [int]$_.number })
    unattributedCommits = @($unattributed | ForEach-Object {
        [ordered]@{
            sha = [string]$_.sha
            subject = [string]$_.subject
        }
    })
    generatedAt = (Get-Date).ToUniversalTime().ToString("o")
}

$parent = Split-Path -Parent $OutputPath
if ($parent) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}
$manifest | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $OutputPath -Encoding utf8

[pscustomobject]$manifest
