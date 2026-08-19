<#
.SYNOPSIS
    Selects the published PowerToys release immediately preceding a candidate.

.DESCRIPTION
    Includes stable releases and prereleases, excludes drafts and the target
    tag, and selects the latest release published before the candidate build
    entered the queue.

.EXAMPLE
    .\get-previous-published-release.ps1 -TargetTag v0.101.2181.0 -QueuedAt 2026-08-06T06:00:00Z
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TargetTag,
    [Parameter(Mandatory)][datetime]$QueuedAt,
    [string]$Repo = "microsoft/PowerToys",
    [string]$RepoPath = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path,
    [string]$OutputPath,
    [string]$ReleasesJsonPath,
    [switch]$SkipSourceCommitResolution
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-Releases {
    if ($ReleasesJsonPath) {
        return @(Get-Content -LiteralPath $ReleasesJsonPath -Raw | ConvertFrom-Json)
    }

    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "GitHub CLI ('gh') is required. Install it and run 'gh auth login'."
    }

    $json = gh api --paginate --slurp "repos/$Repo/releases?per_page=100"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to list releases for $Repo."
    }

    $pages = $json | ConvertFrom-Json
    $all = @()
    foreach ($page in @($pages)) {
        $all += @($page)
    }
    return $all
}

function Get-ReleaseManifest {
    param([Parameter(Mandatory)]$Release)

    if ($ReleasesJsonPath -or -not $Release.assets) {
        return $null
    }

    $manifestAsset = @($Release.assets | Where-Object { $_.name -eq "release-manifest.json" }) | Select-Object -First 1
    if (-not $manifestAsset) {
        return $null
    }

    $temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "pt-release-manifest-$([Guid]::NewGuid().ToString('N'))"
    try {
        New-Item -ItemType Directory -Path $temporaryDirectory -Force | Out-Null
        gh release download $Release.tag_name `
            --repo $Repo `
            --pattern "release-manifest.json" `
            --dir $temporaryDirectory `
            --clobber | Out-Null
        if ($LASTEXITCODE -ne 0) {
            return $null
        }

        $path = Join-Path $temporaryDirectory "release-manifest.json"
        if (Test-Path -LiteralPath $path) {
            return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        }
        return $null
    }
    finally {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Resolve-ReleaseCommit {
    param(
        [Parameter(Mandatory)]$Release,
        $Manifest
    )

    if ($Manifest -and [string]$Manifest.sourceCommit -match "^[0-9a-fA-F]{40}$") {
        return ([string]$Manifest.sourceCommit).ToLowerInvariant()
    }

    if ([string]$Release.target_commitish -match "^[0-9a-fA-F]{40}$") {
        return ([string]$Release.target_commitish).ToLowerInvariant()
    }

    if ($RepoPath -and (Test-Path -LiteralPath $RepoPath)) {
        $tagCommit = git -C $RepoPath rev-parse --verify "$($Release.tag_name)^{commit}" 2>$null
        if ($LASTEXITCODE -eq 0 -and [string]$tagCommit -match "^[0-9a-fA-F]{40}$") {
            return ([string]$tagCommit).Trim().ToLowerInvariant()
        }
    }

    if (-not $ReleasesJsonPath) {
        $commit = gh api "repos/$Repo/commits/$($Release.tag_name)" --jq ".sha" 2>$null
        if ($LASTEXITCODE -eq 0 -and [string]$commit -match "^[0-9a-fA-F]{40}$") {
            return ([string]$commit).Trim().ToLowerInvariant()
        }
    }

    throw "Could not resolve an immutable source commit for release '$($Release.tag_name)'."
}

$candidateQueueTime = $QueuedAt.ToUniversalTime()
$eligible = @(
    Get-Releases |
        Where-Object {
            -not [bool]$_.draft -and
            [string]$_.tag_name -ne $TargetTag -and
            $_.published_at -and
            ([datetime]$_.published_at).ToUniversalTime() -lt $candidateQueueTime
        } |
        Sort-Object { ([datetime]$_.published_at).ToUniversalTime() } -Descending
)

if ($eligible.Count -eq 0) {
    throw "No published PowerToys release predates candidate queue time $($candidateQueueTime.ToString('o'))."
}

$release = $eligible[0]
$manifest = Get-ReleaseManifest -Release $release
$sourceCommit = if ($SkipSourceCommitResolution) {
    $null
}
else {
    Resolve-ReleaseCommit -Release $release -Manifest $manifest
}

$sourceBranch = $null
if ($manifest -and $manifest.sourceBranch) {
    $sourceBranch = [string]$manifest.sourceBranch
}

$result = [ordered]@{
    schemaVersion = 1
    tag = [string]$release.tag_name
    name = [string]$release.name
    publishedAt = ([datetime]$release.published_at).ToUniversalTime().ToString("o")
    prerelease = [bool]$release.prerelease
    url = if ($release.html_url) { [string]$release.html_url } else { $null }
    sourceBranch = $sourceBranch
    sourceCommit = $sourceCommit
    source = if ($manifest) { "release-manifest" } else { "release-tag" }
}

if ($OutputPath) {
    $parent = Split-Path -Parent $OutputPath
    if ($parent) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding utf8
}

[pscustomobject]$result
