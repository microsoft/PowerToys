<#
.SYNOPSIS
    Creates or updates a PowerToys GitHub draft preview release.

.DESCRIPTION
    This script intentionally exposes no publish operation. It preserves text
    outside the managed preview-agent body markers and uploads only explicitly
    generated release assets.

.EXAMPLE
    .\upsert-draft-preview-release.ps1 -Tag v0.101.2181.0 -TargetCommit 0123... -BodyPath .\release-notes.md -AssetsDirectory .\assets
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Tag,
    [Parameter(Mandatory)][string]$TargetCommit,
    [Parameter(Mandatory)][string]$BodyPath,
    [Parameter(Mandatory)][string]$AssetsDirectory,
    [string]$Repo = "microsoft/PowerToys",
    [string]$OutputPath,
    [string]$ExistingReleaseJsonPath,
    [string]$MergedBodyOutputPath,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "preview-release-assets.ps1")
. (Join-Path $PSScriptRoot "github-tag-target.ps1")

$beginMarker = "<!-- BEGIN POWERTOYS PREVIEW AGENT -->"
$endMarker = "<!-- END POWERTOYS PREVIEW AGENT -->"
$releaseTitle = "Preview $Tag"

function Get-ManagedBlock {
    param([Parameter(Mandatory)][string]$Body)

    $start = $Body.IndexOf($beginMarker, [StringComparison]::Ordinal)
    $end = $Body.IndexOf($endMarker, [StringComparison]::Ordinal)
    if ($start -ge 0 -and $end -gt $start) {
        return $Body.Substring($start, ($end + $endMarker.Length) - $start)
    }

    return "$beginMarker`n$($Body.Trim())`n$endMarker"
}

function Merge-ReleaseBody {
    param(
        [string]$ExistingBody,
        [Parameter(Mandatory)][string]$GeneratedBody
    )

    $managedBlock = Get-ManagedBlock -Body $GeneratedBody
    if ([string]::IsNullOrWhiteSpace($ExistingBody)) {
        return $managedBlock
    }

    $start = $ExistingBody.IndexOf($beginMarker, [StringComparison]::Ordinal)
    $end = $ExistingBody.IndexOf($endMarker, [StringComparison]::Ordinal)
    if ($start -ge 0 -and $end -gt $start) {
        $prefix = $ExistingBody.Substring(0, $start)
        $suffix = $ExistingBody.Substring($end + $endMarker.Length)
        return "$prefix$managedBlock$suffix"
    }

    return "$($ExistingBody.TrimEnd())`n`n$managedBlock"
}

if ($TargetCommit -notmatch "^[0-9a-fA-F]{40}$") {
    throw "TargetCommit must be a full immutable commit SHA."
}
if (-not (Test-Path -LiteralPath $BodyPath -PathType Leaf)) {
    throw "Release body not found: $BodyPath"
}
if (-not (Test-Path -LiteralPath $AssetsDirectory -PathType Container)) {
    throw "Assets directory not found: $AssetsDirectory"
}

$generatedBody = Get-Content -LiteralPath $BodyPath -Raw
$assetFiles = @(Get-PreviewReleaseAssets -AssetsDirectory $AssetsDirectory)
if ($assetFiles.Count -eq 0) {
    throw "No generated release assets were found in '$AssetsDirectory'."
}
foreach ($asset in $assetFiles) {
    if ($asset.Length -le 0) {
        throw "Release asset '$($asset.FullName)' is empty."
    }
}

$existing = $null
if ($ExistingReleaseJsonPath) {
    $existing = Get-Content -LiteralPath $ExistingReleaseJsonPath -Raw | ConvertFrom-Json
}
elseif (-not $DryRun) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "GitHub CLI ('gh') is required. Install it and run 'gh auth login'."
    }
    gh auth status | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI is not authenticated."
    }

    $existingJson = gh release view $Tag `
        --repo $Repo `
        --json databaseId,isDraft,isPrerelease,tagName,targetCommitish,url,body 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($existingJson)) {
        $existing = $existingJson | ConvertFrom-Json
    }
}

if ($existing -and -not [bool]$existing.isDraft) {
    throw "Published release '$Tag' already exists. Published releases are immutable in this workflow."
}
if (-not $DryRun) {
    $tagCommit = Get-GitHubTagCommit -Repo $Repo -Tag $Tag
    Assert-GitHubTagTarget -Tag $Tag -ResolvedCommit $tagCommit -TargetCommit $TargetCommit
}

$finalBody = Merge-ReleaseBody `
    -ExistingBody $(if ($existing) { [string]$existing.body } else { "" }) `
    -GeneratedBody $generatedBody

if ($MergedBodyOutputPath) {
    $mergedParent = Split-Path -Parent $MergedBodyOutputPath
    if ($mergedParent) {
        New-Item -ItemType Directory -Path $mergedParent -Force | Out-Null
    }
    Set-Content -LiteralPath $MergedBodyOutputPath -Value $finalBody -Encoding utf8
}

$temporaryBody = [System.IO.Path]::GetTempFileName()
try {
    Set-Content -LiteralPath $temporaryBody -Value $finalBody -Encoding utf8

    if (-not $DryRun) {
        if ($existing) {
            gh release edit $Tag `
                --repo $Repo `
                --title $releaseTitle `
                --notes-file $temporaryBody `
                --target $TargetCommit `
                --draft `
                --prerelease | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to update draft release '$Tag'."
            }
        }
        else {
            gh release create $Tag `
                --repo $Repo `
                --title $releaseTitle `
                --notes-file $temporaryBody `
                --target $TargetCommit `
                --draft `
                --prerelease | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to create draft release '$Tag'."
            }
        }

        $releaseMetadataJson = gh release view $Tag `
            --repo $Repo `
            --json databaseId
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($releaseMetadataJson)) {
            throw "Failed to load draft '$Tag' before asset upload."
        }
        $releaseMetadata = $releaseMetadataJson | ConvertFrom-Json
        $remoteReleaseJson = gh api "repos/$Repo/releases/$($releaseMetadata.databaseId)"
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($remoteReleaseJson)) {
            throw "Failed to inspect existing assets for draft '$Tag'."
        }
        $remoteRelease = $remoteReleaseJson | ConvertFrom-Json
        $localOnlyManifestNames = @("release-manifest.json", "assets-manifest.json")
        $staleManifests = @($remoteRelease.assets | Where-Object { $_.name -in $localOnlyManifestNames })
        foreach ($asset in $staleManifests) {
            gh api --method DELETE "repos/$Repo/releases/assets/$($asset.id)" | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to remove stale local-only manifest '$($asset.name)' from draft '$Tag'."
            }
        }

        $assetPaths = @($assetFiles | ForEach-Object { $_.FullName })
        & gh release upload $Tag --repo $Repo --clobber @assetPaths
        if ($LASTEXITCODE -ne 0) {
            throw "Draft '$Tag' exists, but one or more generated assets failed to upload."
        }

        $verifiedJson = gh release view $Tag `
            --repo $Repo `
            --json isDraft,isPrerelease,targetCommitish,url,name
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to reload draft '$Tag' after update."
        }
        $verified = $verifiedJson | ConvertFrom-Json
        if (-not [bool]$verified.isDraft -or -not [bool]$verified.isPrerelease) {
            throw "Release '$Tag' failed the post-write draft/prerelease safety assertion."
        }
        if ([string]$verified.targetCommitish -ne $TargetCommit) {
            throw "Release '$Tag' target '$($verified.targetCommitish)' does not match '$TargetCommit'."
        }
        if ([string]$verified.name -ne $releaseTitle) {
            throw "Release '$Tag' title '$($verified.name)' does not match '$releaseTitle'."
        }
    }

    $result = [ordered]@{
        schemaVersion = 1
        dryRun = [bool]$DryRun
        action = if ($existing) { "updated" } else { "created" }
        tag = $Tag
        title = $releaseTitle
        targetCommit = $TargetCommit.ToLowerInvariant()
        draft = $true
        prerelease = $true
        url = if ($DryRun) { $null } else { [string]$verified.url }
        assetNames = @($assetFiles | ForEach-Object { $_.Name })
    }

    if ($OutputPath) {
        $parent = Split-Path -Parent $OutputPath
        if ($parent) {
            New-Item -ItemType Directory -Path $parent -Force | Out-Null
        }
        $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding utf8
    }

    [pscustomobject]$result
}
finally {
    Remove-Item -LiteralPath $temporaryBody -Force -ErrorAction SilentlyContinue
}
