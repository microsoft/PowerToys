<#
.SYNOPSIS
    Detects changes between the last reviewed commit and current head of a pull request.

.DESCRIPTION
    This script compares a previously reviewed commit SHA with the current head of a pull request
    to determine what has changed. It helps enable incremental reviews by identifying new commits
    and modified files since the last review iteration.

    The script handles several scenarios:
    - First review (no previous SHA provided)
    - No changes (current SHA matches last reviewed SHA)
    - Force-push detected (last reviewed SHA no longer in history)
    - Incremental changes (new commits added since last review)

.PARAMETER PullRequestNumber
    The pull request number to analyze.

.PARAMETER LastReviewedCommitSha
    The commit SHA that was last reviewed. If omitted, this is treated as a first review.

.PARAMETER RepositoryOwner
    The GitHub repository owner. Defaults to "microsoft".

.PARAMETER RepositoryName
    The GitHub repository name. Defaults to "PowerToys".

.OUTPUTS
    JSON object containing:
    - PullRequestNumber: The PR number being analyzed
    - CurrentHeadSha: The current head commit SHA
    - LastReviewedSha: The last reviewed commit SHA (if provided)
    - BaseRefName: Base branch name
    - HeadRefName: Head branch name
    - IsIncremental: Boolean indicating if incremental review is possible
    - NeedFullReview: Boolean indicating if a full review is required
    - ChangedFiles: Array of files that changed (filename, status, additions, deletions)
    - NewCommits: Array of commits added since last review (sha, message, author, date)
    - Summary: Human-readable description of changes

.EXAMPLE
    .\Get-PrIncrementalChanges.ps1 -PullRequestNumber 42374
    Analyzes PR #42374 with no previous review (first review scenario).

.EXAMPLE
    .\Get-PrIncrementalChanges.ps1 -PullRequestNumber 42374 -LastReviewedCommitSha "abc123def456"
    Compares current PR state against the last reviewed commit to identify incremental changes.

.EXAMPLE
    $changes = .\Get-PrIncrementalChanges.ps1 -PullRequestNumber 42374 -LastReviewedCommitSha "abc123" | ConvertFrom-Json
    if ($changes.IsIncremental) { Write-Host "Can perform incremental review" }
    Captures the output as a PowerShell object for further processing.

.NOTES
    Requires GitHub CLI (gh) to be installed and authenticated.
    Run 'gh auth login' if not already authenticated.

.LINK
    https://cli.github.com/
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = "Pull request number")]
    [int]$PullRequestNumber,

    [Parameter(Mandatory = $false, HelpMessage = "Commit SHA that was last reviewed")]
    [string]$LastReviewedCommitSha,

    [Parameter(Mandatory = $false, HelpMessage = "Repository owner")]
    [string]$RepositoryOwner = "microsoft",

    [Parameter(Mandatory = $false, HelpMessage = "Repository name")]
    [string]$RepositoryName = "PowerToys"
)

# Fetch current pull request state from GitHub
try {
    $pullRequestData = gh pr view $PullRequestNumber --json headRefOid,headRefName,baseRefName,baseRefOid | ConvertFrom-Json
} catch {
    Write-Error "Failed to fetch PR #$PullRequestNumber details. Details: $_"
    exit 1
}

$currentHeadSha = $pullRequestData.headRefOid
$baseRefName = $pullRequestData.baseRefName
$headRefName = $pullRequestData.headRefName

# Initialize result object
$analysisResult = @{
    PullRequestNumber = $PullRequestNumber
    CurrentHeadSha = $currentHeadSha
    BaseRefName = $baseRefName
    HeadRefName = $headRefName
    LastReviewedSha = $LastReviewedCommitSha
    IsIncremental = $false
    NeedFullReview = $true
    ChangedFiles = @()
    NewCommits = @()
    Summary = ""
}

# Scenario 1: First review (no previous SHA provided)
if ([string]::IsNullOrWhiteSpace($LastReviewedCommitSha)) {
    $analysisResult.Summary = "Initial review - no previous iteration found"
    $analysisResult.NeedFullReview = $true
    return $analysisResult | ConvertTo-Json -Depth 10
}

# Scenario 2: No changes since last review
if ($currentHeadSha -eq $LastReviewedCommitSha) {
    $analysisResult.Summary = "No changes since last review (SHA: $currentHeadSha)"
    $analysisResult.NeedFullReview = $false
    $analysisResult.IsIncremental = $true
    return $analysisResult | ConvertTo-Json -Depth 10
}

# Scenario 3: Check for force-push (last reviewed SHA no longer exists in history)
try {
    $null = gh api "repos/$RepositoryOwner/$RepositoryName/commits/$LastReviewedCommitSha" 2>&1
    if ($LASTEXITCODE -ne 0) {
        # SHA not found - likely force-push or branch rewrite
        $analysisResult.Summary = "Force-push detected - last reviewed SHA $LastReviewedCommitSha no longer exists. Full review required."
        $analysisResult.NeedFullReview = $true
        return $analysisResult | ConvertTo-Json -Depth 10
    }
} catch {
    $analysisResult.Summary = "Cannot verify last reviewed SHA $LastReviewedCommitSha - assuming force-push. Full review required."
    $analysisResult.NeedFullReview = $true
    return $analysisResult | ConvertTo-Json -Depth 10
}

# Scenario 4: Get incremental changes between last reviewed SHA and current head
try {
    $compareApiPath = "repos/$RepositoryOwner/$RepositoryName/compare/$LastReviewedCommitSha...$currentHeadSha"
    $comparisonData = gh api $compareApiPath | ConvertFrom-Json
    
    # Extract new commits information
    $analysisResult.NewCommits = $comparisonData.commits | ForEach-Object {
        @{
            Sha = $_.sha.Substring(0, 7)
            Message = $_.commit.message.Split("`n")[0]  # First line only
            Author = $_.commit.author.name
            Date = $_.commit.author.date
        }
    }
    
    # Extract changed files information
    $analysisResult.ChangedFiles = $comparisonData.files | ForEach-Object {
        @{
            Filename = $_.filename
            Status = $_.status  # added, modified, removed, renamed
            Additions = $_.additions
            Deletions = $_.deletions
            Changes = $_.changes
        }
    }
    
    $fileCount = $analysisResult.ChangedFiles.Count
    $commitCount = $analysisResult.NewCommits.Count
    
    $analysisResult.IsIncremental = $true
    $analysisResult.NeedFullReview = $false
    $analysisResult.Summary = "Incremental review: $commitCount new commit(s), $fileCount file(s) changed since SHA $($LastReviewedCommitSha.Substring(0, 7))"
    
} catch {
    Write-Error "Failed to compare commits. Details: $_"
    $analysisResult.Summary = "Error comparing commits - defaulting to full review"
    $analysisResult.NeedFullReview = $true
}

# Return the analysis result as JSON
return $analysisResult | ConvertTo-Json -Depth 10
