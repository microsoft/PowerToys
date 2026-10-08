<#
.SYNOPSIS
    Calculates semantic PR changes between two exact PowerToys release commits.

.DESCRIPTION
    Uses a direct range for same-lineage releases and a symmetric comparison
    from the merge base for branch transitions. PR numbers, cherry-pick source
    annotations, and stable patch IDs prevent equivalent changes from being
    reported as new solely because their commit SHAs differ.

.EXAMPLE
    .\get-preview-release-delta.ps1 -PreviousCommit abc123 -TargetCommit def456 -OutputDirectory .\preview-154000000
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PreviousCommit,
    [Parameter(Mandatory)][string]$TargetCommit,
    [string]$Repo = "microsoft/PowerToys",
    [string]$RepoPath = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [switch]$Fetch,
    [switch]$NoGitHubLookup
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Git {
    param([Parameter(ValueFromRemainingArguments)][string[]]$Arguments)

    $output = & git -C $RepoPath @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed: $($output -join "`n")"
    }
    return $output
}

function Resolve-Commit {
    param([Parameter(Mandatory)][string]$Commit)

    $resolved = Invoke-Git rev-parse --verify "$Commit^{commit}"
    $sha = ([string]$resolved).Trim()
    if ($sha -notmatch "^[0-9a-fA-F]{40}$") {
        throw "Commit '$Commit' did not resolve to a full SHA."
    }
    return $sha.ToLowerInvariant()
}

function Test-Ancestor {
    param(
        [Parameter(Mandatory)][string]$Ancestor,
        [Parameter(Mandatory)][string]$Descendant
    )

    & git -C $RepoPath merge-base --is-ancestor $Ancestor $Descendant 2>$null
    if ($LASTEXITCODE -eq 0) {
        return $true
    }
    if ($LASTEXITCODE -eq 1) {
        return $false
    }
    throw "git merge-base --is-ancestor failed for $Ancestor and $Descendant."
}

function Get-PatchId {
    param([Parameter(Mandatory)][string]$Sha)

    $patchOutput = & git -C $RepoPath show --pretty=format: --no-ext-diff --binary $Sha |
        & git -C $RepoPath patch-id --stable
    if ($LASTEXITCODE -ne 0 -or -not $patchOutput) {
        return $null
    }

    $first = @($patchOutput)[0]
    if ([string]$first -match "^([0-9a-fA-F]{40})\s") {
        return $matches[1].ToLowerInvariant()
    }
    return $null
}

function Get-SubjectPrNumber {
    param([Parameter(Mandatory)][string]$Subject)

    if ($Subject -match "\(#(\d+)\)\s*$") {
        return [int]$matches[1]
    }
    if ($Subject -match "^Merge pull request #(\d+)\b") {
        return [int]$matches[1]
    }
    return $null
}

function Get-AssociatedPrNumber {
    param([Parameter(Mandatory)][string]$Sha)

    if ($NoGitHubLookup) {
        return $null
    }
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "GitHub CLI ('gh') is required for commit-to-PR fallback resolution."
    }

    $json = gh api `
        -H "Accept: application/vnd.github+json" `
        "repos/$Repo/commits/$Sha/pulls" 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) {
        return $null
    }

    $pulls = @($json | ConvertFrom-Json)
    $selected = @($pulls | Where-Object { $_.merged_at } | Sort-Object merged_at -Descending | Select-Object -First 1)
    if ($selected.Count -eq 0) {
        $selected = @($pulls | Select-Object -First 1)
    }
    if ($selected.Count -gt 0) {
        return [int]$selected[0].number
    }
    return $null
}

function Get-CommitRecord {
    param([Parameter(Mandatory)][string]$Sha)

    $subject = ([string](Invoke-Git show -s --format=%s $Sha)).Trim()
    $body = ([string](Invoke-Git show -s --format=%B $Sha)).Trim()
    $prNumber = Get-SubjectPrNumber -Subject $subject
    $identitySource = if ($prNumber) { "subject" } else { $null }
    $cherryPickedFrom = $null

    if (-not $prNumber) {
        $prNumber = Get-AssociatedPrNumber -Sha $Sha
        if ($prNumber) {
            $identitySource = "github-associated-pr"
        }
    }

    if (-not $prNumber -and $body -match "\(cherry picked from commit ([0-9a-fA-F]{7,40})\)") {
        $cherryPickedFrom = $matches[1].ToLowerInvariant()
        $sourceSha = Invoke-Git rev-parse --verify "$cherryPickedFrom^{commit}"
        if ($sourceSha) {
            $sourceSubject = ([string](Invoke-Git show -s --format=%s ([string]$sourceSha).Trim())).Trim()
            $prNumber = Get-SubjectPrNumber -Subject $sourceSubject
            if (-not $prNumber) {
                $prNumber = Get-AssociatedPrNumber -Sha ([string]$sourceSha).Trim()
            }
            if ($prNumber) {
                $identitySource = "cherry-pick-source"
            }
        }
    }

    $patchId = Get-PatchId -Sha $Sha
    $identity = if ($prNumber) {
        "pr:$prNumber"
    }
    elseif ($patchId) {
        "patch:$patchId"
    }
    else {
        "commit:$Sha"
    }

    [pscustomobject]@{
        sha = $Sha
        subject = $subject
        prNumber = $prNumber
        identity = $identity
        identitySource = if ($identitySource) { $identitySource } elseif ($patchId) { "patch-id" } else { "unattributed" }
        patchId = $patchId
        cherryPickedFrom = $cherryPickedFrom
    }
}

function Get-RangeRecords {
    param(
        [Parameter(Mandatory)][string]$Start,
        [Parameter(Mandatory)][string]$End
    )

    $commits = @(Invoke-Git rev-list --reverse "$Start..$End" | Where-Object { $_ })
    $records = @()
    foreach ($commit in $commits) {
        $records += Get-CommitRecord -Sha ([string]$commit).Trim()
    }
    return $records
}

function New-IdentitySet {
    param([Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Records)

    $set = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($record in $Records) {
        [void]$set.Add([string]$record.identity)
    }
    return ,$set
}

function Resolve-CrossSidePatchIdentities {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Left,
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Right
    )

    foreach ($record in @($Left | Where-Object { -not $_.prNumber -and $_.patchId })) {
        $candidateNumbers = @(
            $Right |
                Where-Object { $_.prNumber -and $_.patchId -eq $record.patchId } |
                ForEach-Object { [int]$_.prNumber } |
                Sort-Object -Unique
        )
        if ($candidateNumbers.Count -gt 1) {
            throw "Patch ID '$($record.patchId)' maps to multiple PR numbers: $($candidateNumbers -join ', ')."
        }
        if ($candidateNumbers.Count -eq 1) {
            $record.prNumber = $candidateNumbers[0]
            $record.identity = "pr:$($candidateNumbers[0])"
            $record.identitySource = "patch-id-equivalent-pr"
        }
    }
}

function Get-PrOutput {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Records,
        [Parameter(Mandatory)][AllowEmptyCollection()][System.Collections.Generic.HashSet[string]]$OtherSide,
        [switch]$IncludeExisting
    )

    $groups = @($Records | Where-Object {
        $_.prNumber -and ($IncludeExisting -or -not $OtherSide.Contains([string]$_.identity))
    } | Group-Object prNumber)

    return @($groups | ForEach-Object {
        $groupRecords = @($_.Group)
        [pscustomobject]@{
            number = [int]$_.Name
            commits = @($groupRecords | ForEach-Object { $_.sha })
            subjects = @($groupRecords | ForEach-Object { $_.subject } | Select-Object -Unique)
            identitySources = @($groupRecords | ForEach-Object { $_.identitySource } | Select-Object -Unique)
        }
    } | Sort-Object number)
}

if ($Fetch) {
    Invoke-Git fetch origin --tags --prune | Out-Null
}

$previousSha = Resolve-Commit -Commit $PreviousCommit
$targetSha = Resolve-Commit -Commit $TargetCommit

$previousIsAncestor = Test-Ancestor -Ancestor $previousSha -Descendant $targetSha
$targetIsAncestor = Test-Ancestor -Ancestor $targetSha -Descendant $previousSha

if ($targetIsAncestor -and -not $previousIsAncestor) {
    throw "Target commit $targetSha predates published baseline $previousSha on the same lineage."
}

$mergeBase = $null
$previousRecords = @()
$targetRecords = @()
if ($previousIsAncestor) {
    $deltaMode = "same-lineage"
    $targetRecords = @(Get-RangeRecords -Start $previousSha -End $targetSha)
}
else {
    $deltaMode = "branch-transition"
    $mergeBase = ([string](Invoke-Git merge-base $previousSha $targetSha)).Trim().ToLowerInvariant()
    $previousRecords = @(Get-RangeRecords -Start $mergeBase -End $previousSha)
    $targetRecords = @(Get-RangeRecords -Start $mergeBase -End $targetSha)
}

Resolve-CrossSidePatchIdentities -Left $previousRecords -Right $targetRecords
Resolve-CrossSidePatchIdentities -Left $targetRecords -Right $previousRecords

$previousIdentities = New-IdentitySet -Records $previousRecords
$targetIdentities = New-IdentitySet -Records $targetRecords

$addedPrs = Get-PrOutput -Records $targetRecords -OtherSide $previousIdentities
$removedPrs = if ($deltaMode -eq "branch-transition") {
    Get-PrOutput -Records $previousRecords -OtherSide $targetIdentities
}
else {
    @()
}

$unattributed = @($targetRecords | Where-Object {
    -not $_.prNumber -and -not $previousIdentities.Contains([string]$_.identity)
})
$removedUnattributed = @($previousRecords | Where-Object {
    -not $_.prNumber -and -not $targetIdentities.Contains([string]$_.identity)
})
$commonIdentities = @($targetRecords | Where-Object {
    $previousIdentities.Contains([string]$_.identity)
} | ForEach-Object { $_.identity } | Select-Object -Unique)

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$commitOutput = [ordered]@{
    schemaVersion = 1
    previousCommit = $previousSha
    targetCommit = $targetSha
    deltaMode = $deltaMode
    mergeBase = $mergeBase
    previousSide = $previousRecords
    targetSide = $targetRecords
    commonIdentities = $commonIdentities
    removedUnattributedCommits = $removedUnattributed
}

$commitPath = Join-Path $OutputDirectory "delta-commits.json"
$addedPath = Join-Path $OutputDirectory "delta-prs.json"
$removedPath = Join-Path $OutputDirectory "removed-prs.json"
$unattributedPath = Join-Path $OutputDirectory "unattributed-commits.json"

$commitOutput | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $commitPath -Encoding utf8
ConvertTo-Json -InputObject @($addedPrs) -Depth 6 | Set-Content -LiteralPath $addedPath -Encoding utf8
ConvertTo-Json -InputObject @($removedPrs) -Depth 6 | Set-Content -LiteralPath $removedPath -Encoding utf8
ConvertTo-Json -InputObject @($unattributed) -Depth 6 | Set-Content -LiteralPath $unattributedPath -Encoding utf8

[pscustomobject]@{
    previousCommit = $previousSha
    targetCommit = $targetSha
    deltaMode = $deltaMode
    mergeBase = $mergeBase
    addedPrNumbers = @($addedPrs | ForEach-Object { $_.number })
    removedPrNumbers = @($removedPrs | ForEach-Object { $_.number })
    unattributedCommitCount = $unattributed.Count
    outputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
}
