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
    [string]$Repo = "microsoft/PowerToys",
    [string]$ContextPath,
    [string]$PreviousReleasePath,
    [string]$DeltaDirectory,
    [string]$BodyPath,
    [switch]$DryRun,
    [Parameter(Mandatory)][string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "preview-release-assets.ps1")
. (Join-Path $PSScriptRoot "github-tag-target.ps1")

if ($TargetCommit -notmatch "^[0-9a-fA-F]{40}$") {
    throw "TargetCommit must be a full immutable commit SHA."
}

$release = if ($DryRun) {
    if (-not $BodyPath -or -not (Test-Path -LiteralPath $BodyPath -PathType Leaf)) {
        throw "Dry-run verification requires an existing -BodyPath."
    }
    [pscustomobject]@{
        databaseId = $null
        isDraft = $true
        isPrerelease = $true
        tagName = $Tag
        targetCommitish = $TargetCommit
        url = $null
        body = Get-Content -LiteralPath $BodyPath -Raw
        name = "Preview $Tag"
    }
}
else {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "GitHub CLI ('gh') is required. Install it and run 'gh auth login'."
    }
    $releaseJson = gh release view $Tag `
        --repo $Repo `
        --json databaseId,isDraft,isPrerelease,tagName,targetCommitish,url,body,name
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($releaseJson)) {
        throw "Draft release '$Tag' was not found."
    }
    $releaseJson | ConvertFrom-Json
}

if (-not [bool]$release.isDraft) {
    throw "Release '$Tag' is not a draft."
}
if (-not [bool]$release.isPrerelease) {
    throw "Release '$Tag' is not marked as a prerelease."
}
if ([string]$release.targetCommitish -ne $TargetCommit) {
    throw "Release target '$($release.targetCommitish)' does not match '$TargetCommit'."
}
if (-not $DryRun) {
    $tagCommit = Get-GitHubTagCommit -Repo $Repo -Tag $Tag
    Assert-GitHubTagTarget -Tag $Tag -ResolvedCommit $tagCommit -TargetCommit $TargetCommit
}
if ([string]$release.name -ne "Preview $Tag") {
    throw "Release title '$($release.name)' does not match 'Preview $Tag'."
}
if ([string]$release.body -notmatch "<!-- BEGIN POWERTOYS PREVIEW AGENT -->" -or
    [string]$release.body -notmatch "<!-- END POWERTOYS PREVIEW AGENT -->") {
    throw "Release '$Tag' is missing the managed preview body markers."
}

$localFiles = @(Get-PreviewReleaseAssets -AssetsDirectory $AssetsDirectory)

$assetResults = @()
if ($DryRun) {
    foreach ($file in $localFiles) {
        $assetResults += [pscustomobject]@{
            name = $file.Name
            size = [long]$file.Length
            state = "local"
        }
    }
}
else {
    $apiJson = gh api "repos/$Repo/releases/$($release.databaseId)"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to load release assets for '$Tag'."
    }
    $apiRelease = $apiJson | ConvertFrom-Json
    $remoteAssets = @($apiRelease.assets)
    $uploadedLocalOnlyManifests = @(
        $remoteAssets |
            Where-Object { $_.name -in @("release-manifest.json", "assets-manifest.json") }
    )
    if ($uploadedLocalOnlyManifests.Count -ne 0) {
        throw "Draft '$Tag' must not contain local-only manifests: $(($uploadedLocalOnlyManifests.name | Sort-Object) -join ', ')."
    }

    $expectedNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $localFiles) {
        [void]$expectedNames.Add($file.Name)
    }
    $unexpectedRemoteAssets = @(
        $remoteAssets |
            Where-Object {
                [System.IO.Path]::GetExtension([string]$_.name) -in @(".exe", ".zip") -and
                -not $expectedNames.Contains([string]$_.name)
            }
    )
    if ($unexpectedRemoteAssets.Count -gt 0) {
        throw "Draft '$Tag' contains unexpected generated assets: $(($unexpectedRemoteAssets.name | Sort-Object) -join ', ')"
    }

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
[void]$report.AppendLine($(if ($DryRun) {
    "**PASS:** Local dry-run package is complete; no GitHub draft was created."
} else {
    "**PASS:** Draft prerelease is complete and remains unpublished."
}))
[void]$report.AppendLine("")
[void]$report.AppendLine("- Draft: $(if ($DryRun) { "Not created (dry run)" } else { $release.url })")
[void]$report.AppendLine("- Title: $($release.name)")
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
[void]$report.AppendLine($(if ($DryRun) { "## Validated local assets" } else { "## Uploaded assets" }))
[void]$report.AppendLine("")
foreach ($asset in $assetResults) {
    [void]$report.AppendLine("- $($asset.name) ($($asset.size) bytes)")
}
[void]$report.AppendLine("")
[void]$report.AppendLine("## Unattributed commits")
[void]$report.AppendLine("")
if ($unattributed.Count -eq 0) {
    [void]$report.AppendLine("- None.")
}
else {
    foreach ($commit in $unattributed) {
        [void]$report.AppendLine("- `$($commit.sha)`: $($commit.subject)")
    }
}
[void]$report.AppendLine("")
[void]$report.AppendLine("## Human review remaining")
[void]$report.AppendLine("")
[void]$report.AppendLine("- Review highlights, branch-transition removals, and unattributed changes.")
[void]$report.AppendLine($(if ($DryRun) {
    "- Create the draft through the canonical release workflow before publication."
} else {
    "- Download one installer and one ZIP from the draft."
}))
[void]$report.AppendLine("- Publish only through the existing release-management process.")

$parent = Split-Path -Parent $OutputPath
if ($parent) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}
$report.ToString() | Set-Content -LiteralPath $OutputPath -Encoding utf8

[pscustomobject]@{
    status = "PASS"
    draftUrl = if ($DryRun) { $null } else { [string]$release.url }
    assetCount = $assetResults.Count
    outputPath = (Resolve-Path -LiteralPath $OutputPath).Path
}
