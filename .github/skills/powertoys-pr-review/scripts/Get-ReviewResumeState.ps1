<#
.SYNOPSIS
    Discover resumable PowerToys PR review state across sessions.
.DESCRIPTION
    Reads durable traces from the current teammate's PowerToys repository,
    review branches, review PRs, Copilot reviews, unresolved threads, commits,
    and local git worktrees. It never mutates branches, reviews, or worktrees.
.PARAMETER PRNumber
    One or more upstream microsoft/PowerToys PR numbers.
.PARAMETER ClonePath
    Optional local PowerToys clone path.
.PARAMETER AsJson
    Emit JSON instead of PowerShell objects.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][int[]]$PRNumber,
    [string]$ClonePath,
    [switch]$AsJson
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReviewPayload.Common.ps1')

$config = & (Join-Path $PSScriptRoot 'Get-ForkConfig.ps1') -ClonePath $ClonePath 6>$null
if ([string]::IsNullOrWhiteSpace([string]$config.ClonePath)) {
    throw 'No local PowerToys clone was found. Pass -ClonePath.'
}

function Get-LocalWorktreeForBranch {
    param(
        [Parameter(Mandatory)][string]$RepositoryPath,
        [Parameter(Mandatory)][string]$BranchName
    )

    $currentPath = ''
    $currentBranch = ''
    foreach ($line in @(& git -C $RepositoryPath worktree list --porcelain)) {
        if ($line.StartsWith('worktree ')) {
            $currentPath = $line.Substring('worktree '.Length)
            $currentBranch = ''
        }
        elseif ($line.StartsWith('branch ')) {
            $currentBranch = $line.Substring('branch refs/heads/'.Length)
            if ($currentBranch -eq $BranchName) {
                return $currentPath.Replace('/', '\')
            }
        }
    }

    return ''
}

$results = [System.Collections.Generic.List[object]]::new()
foreach ($number in $PRNumber) {
    $upstream = & gh pr view $number --repo microsoft/PowerToys --json headRefOid,state,title | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) {
        throw "Could not read upstream PR $number."
    }

    $expectedBranch = "pr-iterate/$number"
    $branchExists = $false
    $localBranchExists = $false
    $branchSha = ''
    $refOutput = & gh api "repos/$($config.ForkRepo)/git/ref/heads/$expectedBranch" 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($refOutput)) {
        $ref = $refOutput | ConvertFrom-Json
        $branchExists = $true
        $branchSha = [string]$ref.object.sha
    }
    & git -C $config.ClonePath show-ref --verify --quiet "refs/heads/$expectedBranch"
    if ($LASTEXITCODE -eq 0) {
        $localBranchExists = $true
        if ([string]::IsNullOrWhiteSpace($branchSha)) {
            $branchSha = (& git -C $config.ClonePath rev-parse "refs/heads/$expectedBranch").Trim()
        }
    }

    $reviewPullRequests = [System.Collections.Generic.List[object]]::new()
    foreach ($candidate in @(
        & gh pr list --repo $config.ForkRepo --state all --head $expectedBranch --limit 20 --json number,title,state,headRefName,headRefOid,url,updatedAt |
            ConvertFrom-Json
    )) {
        $reviewPullRequests.Add($candidate)
    }
    foreach ($candidate in @(
        & gh pr list --repo $config.ForkRepo --state all --search "`"[PR $number]`" in:title" --limit 20 --json number,title,state,headRefName,headRefOid,url,updatedAt |
            ConvertFrom-Json |
            Where-Object { $_.title -match "^\[PR $number\]\s" }
    )) {
        if ($candidate.number -notin @($reviewPullRequests.number)) {
            $reviewPullRequests.Add($candidate)
        }
    }
    $reviewPullRequest = @(
        $reviewPullRequests.ToArray() |
            Sort-Object -Property `
                @{ Expression = { if ($_.state -eq 'OPEN') { 1 } else { 0 } }; Descending = $true },
                @{ Expression = { [datetime]$_.updatedAt }; Descending = $true } |
            Select-Object -First 1
    )
    $reviewPullRequestExists = $reviewPullRequest.Count -eq 1
    if ($reviewPullRequestExists) {
        $reviewPullRequest = $reviewPullRequest[0]
        $expectedBranch = [string]$reviewPullRequest.headRefName
        $branchExists = $true
        $branchSha = [string]$reviewPullRequest.headRefOid
    }

    $worktreePath = Get-LocalWorktreeForBranch -RepositoryPath $config.ClonePath -BranchName $expectedBranch
    $copilotReviews = @()
    $unresolvedThreadCount = 0
    $newestCommitAt = $null
    $latestCopilotReviewAt = $null
    if ($reviewPullRequestExists) {
        $reviews = @(Get-GhPagedItems -Endpoint "repos/$($config.ForkRepo)/pulls/$($reviewPullRequest.number)/reviews")
        $copilotReviews = @(
            $reviews |
                Where-Object { $_.user.login -eq 'copilot-pull-request-reviewer[bot]' } |
                Sort-Object submitted_at
        )
        if ($copilotReviews.Count -gt 0) {
            $latestCopilotReviewAt = [datetime]$copilotReviews[-1].submitted_at
        }

        $unresolvedThreadCount = & (Join-Path $PSScriptRoot 'Get-UnresolvedCopilotThreads.ps1') `
            -ForkOwner $config.ForkOwner `
            -PRNumber ([int]$reviewPullRequest.number)

        $reviewDetails = & gh pr view $reviewPullRequest.number --repo $config.ForkRepo --json commits | ConvertFrom-Json
        $commitDates = @($reviewDetails.commits | ForEach-Object { [datetime]$_.committedDate })
        if ($commitDates.Count -gt 0) {
            $newestCommitAt = $commitDates | Sort-Object | Select-Object -Last 1
        }
    }

    $resumeAction = Get-ReviewResumeAction `
        -BranchExists $branchExists `
        -LocalBranchExists $localBranchExists `
        -ReviewPullRequestExists $reviewPullRequestExists `
        -ReviewPullRequestOpen ($reviewPullRequestExists -and $reviewPullRequest.state -eq 'OPEN') `
        -WorktreePath $worktreePath `
        -CopilotReviewCount $copilotReviews.Count `
        -UnresolvedThreadCount $unresolvedThreadCount `
        -LatestCopilotReviewAt $latestCopilotReviewAt `
        -NewestCommitAt $newestCommitAt

    $results.Add([pscustomobject]@{
        upstreamPr = $number
        upstreamState = [string]$upstream.state
        upstreamHeadSha = [string]$upstream.headRefOid
        reviewBranch = $expectedBranch
        reviewBranchExists = $branchExists
        localReviewBranchExists = $localBranchExists
        reviewBranchSha = $branchSha
        reviewPullRequestNumber = if ($reviewPullRequestExists) { [int]$reviewPullRequest.number } else { $null }
        reviewPullRequestState = if ($reviewPullRequestExists) { [string]$reviewPullRequest.state } else { $null }
        reviewPullRequestUrl = if ($reviewPullRequestExists) { [string]$reviewPullRequest.url } else { $null }
        worktreePath = $worktreePath
        copilotReviewCount = $copilotReviews.Count
        latestCopilotReviewAt = $latestCopilotReviewAt
        newestCommitAt = $newestCommitAt
        unresolvedCopilotThreads = [int]$unresolvedThreadCount
        resumeAction = $resumeAction
        mustRebuild = $resumeAction -ne 'fresh-mirror'
    })
}

if ($AsJson) {
    $results.ToArray() | ConvertTo-Json -Depth 10
}
else {
    $results.ToArray()
}
