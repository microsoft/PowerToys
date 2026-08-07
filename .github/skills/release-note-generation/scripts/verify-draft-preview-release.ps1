<#
.SYNOPSIS
    Verifies a PowerToys draft preview release and writes the final review report.

.DESCRIPTION
    Asserts immutable target identity, draft/prerelease flags, managed body
    markers, and exact asset names and sizes. This script performs no writes to
    GitHub.

.EXAMPLE
    .\verify-draft-preview-release.ps1 -Tag v0.101.2181.0 -TargetCommit 0123... -AssetsDirectory .\assets -OutputPath .\final-review.md
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][string]$TargetCommit,
    [Parameter(Mandatory)][string]$AssetsDirectory,
    [string[]]$AdditionalAsset = @(),
    [string]$Repo = "microsoft/PowerToys",
    [string]$ContextPath,
    [string]$PreviousReleasePath,
    [string]$DeltaDirectory,
    [Parameter(Mandatory)][string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI ('gh') is required. Install it and run 'gh auth login'."
}
if ($TargetCommit -notmatch "^[0-9a-fA-F]{40}$") {
    throw "TargetCommit must be a full immutable commit SHA."
}

$releaseJson = gh release view $Tag `
    --repo $Repo `
    --json databaseId,isDraft,isPrerelease,tagName,targetCommitish,url,body
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($releaseJson)) {
    throw "Draft release '$Tag' was not found."
}
$release = $releaseJson | ConvertFrom-Json

if (-not [bool]$release.isDraft) {
    throw "Release '$Tag' is not a draft."
}
if (-not [bool]$release.isPrerelease) {
    throw "Release '$Tag' is not marked as a prerelease."
}
if ([string]$release.targetCommitish -ne $TargetCommit) {
    throw "Release target '$($release.targetCommitish)' does not match '$TargetCommit'."
}
if ([string]$release.body -notmatch "<!-- BEGIN POWERTOYS PREVIEW AGENT -->" -or
    [string]$release.body -notmatch "<!-- END POWERTOYS PREVIEW AGENT -->") {
    throw "Release '$Tag' is missing the managed preview body markers."
}

$localFiles = @(
    Get-ChildItem -LiteralPath $AssetsDirectory -File |
        Where-Object {
            $_.Name -notmatch "^\." -and (
                $_.Extension -in @(".exe", ".zip") -or
                $_.Name -eq "assets-manifest.json"
            )
        }
)
foreach ($path in $AdditionalAsset) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Additional release asset not found: $path"
    }
    $localFiles += Get-Item -LiteralPath $path
}
$localFiles = @($localFiles | Sort-Object FullName -Unique)

$apiJson = gh api "repos/$Repo/releases/$($release.databaseId)"
if ($LASTEXITCODE -ne 0) {
    throw "Failed to load release assets for '$Tag'."
}
$apiRelease = $apiJson | ConvertFrom-Json
$remoteAssets = @($apiRelease.assets)

$assetResults = @()
foreach ($file in $localFiles) {
    $remote = @($remoteAssets | Where-Object { $_.name -eq $file.Name })
    if ($remote.Count -ne 1) {
        throw "Expected exactly one uploaded asset named '$($file.Name)', found $($remote.Count)."
    }
    if ([long]$remote[0].size -ne [long]$file.Length) {
        throw "Uploaded asset '$($file.Name)' size '$($remote[0].size)' does not match local size '$($file.Length)'."
    }
    $assetResults += [pscustomobject]@{
        name = $file.Name
        size = [long]$file.Length
        state = [string]$remote[0].state
    }
}

$context = if ($ContextPath) {
    Get-Content -LiteralPath $ContextPath -Raw | ConvertFrom-Json
}
else {
    $null
}
$baseline = if ($PreviousReleasePath) {
    Get-Content -LiteralPath $PreviousReleasePath -Raw | ConvertFrom-Json
}
else {
    $null
}

$added = @()
$removed = @()
$unattributed = @()
$deltaDetails = $null
if ($DeltaDirectory) {
    $added = @(Get-Content -LiteralPath (Join-Path $DeltaDirectory "delta-prs.json") -Raw | ConvertFrom-Json)
    $removed = @(Get-Content -LiteralPath (Join-Path $DeltaDirectory "removed-prs.json") -Raw | ConvertFrom-Json)
    $unattributed = @(Get-Content -LiteralPath (Join-Path $DeltaDirectory "unattributed-commits.json") -Raw | ConvertFrom-Json)
    $deltaDetails = Get-Content -LiteralPath (Join-Path $DeltaDirectory "delta-commits.json") -Raw | ConvertFrom-Json
}

$report = [System.Text.StringBuilder]::new()
[void]$report.AppendLine("# Preview release final review")
[void]$report.AppendLine("")
[void]$report.AppendLine("**PASS:** Draft prerelease is complete and remains unpublished.")
[void]$report.AppendLine("")
[void]$report.AppendLine("- Draft: $($release.url)")
if ($context) {
    [void]$report.AppendLine("- Build: [$($context.buildId)]($($context.buildUrl))")
    [void]$report.AppendLine("- Version: $($context.version)")
    [void]$report.AppendLine("- Source: $($context.sourceBranch)@$(([string]$context.sourceCommit).Substring(0, 12))")
    [void]$report.AppendLine("- Intent/channel: $($context.intent) / $($context.channel)")
}
if ($baseline) {
    [void]$report.AppendLine("- Baseline: $($baseline.tag)@$(([string]$baseline.sourceCommit).Substring(0, 12))")
}
if ($deltaDetails) {
    [void]$report.AppendLine("- Delta mode: $($deltaDetails.deltaMode)")
}
[void]$report.AppendLine("- Added PRs: $($added.Count)$(if ($added.Count) { " (" + (($added | ForEach-Object { "#$($_.number)" }) -join ", ") + ")" })")
[void]$report.AppendLine("- Removed PRs: $($removed.Count)$(if ($removed.Count) { " (" + (($removed | ForEach-Object { "#$($_.number)" }) -join ", ") + ")" })")
[void]$report.AppendLine("- Unattributed commits: $($unattributed.Count)")
[void]$report.AppendLine("- Assets: $($assetResults.Count)/$($localFiles.Count) verified")
[void]$report.AppendLine("")
[void]$report.AppendLine("## Uploaded assets")
[void]$report.AppendLine("")
foreach ($asset in $assetResults) {
    [void]$report.AppendLine("- $($asset.name) ($($asset.size) bytes)")
}
[void]$report.AppendLine("")
[void]$report.AppendLine("## Human review remaining")
[void]$report.AppendLine("")
[void]$report.AppendLine("- Review highlights, branch-transition removals, and unattributed changes.")
[void]$report.AppendLine("- Download one installer and one ZIP from the draft.")
[void]$report.AppendLine("- Publish only through the existing release-management process.")

$parent = Split-Path -Parent $OutputPath
if ($parent) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}
$report.ToString() | Set-Content -LiteralPath $OutputPath -Encoding utf8

[pscustomobject]@{
    status = "PASS"
    draftUrl = [string]$release.url
    assetCount = $assetResults.Count
    outputPath = (Resolve-Path -LiteralPath $OutputPath).Path
}
