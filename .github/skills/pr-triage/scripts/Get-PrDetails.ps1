<#
.SYNOPSIS
    Enriches a PR with detailed metadata including reviews, comments, and CI status.

.DESCRIPTION
    Fetches additional data for a single PR to enable accurate categorization:
    - Review history and states
    - Comment activity
    - CI/check status
    - Commit history
    - Linked issue details

.PARAMETER PullRequestNumber
    The PR number to enrich.

.PARAMETER Repository
    GitHub repository in owner/repo format. Default: microsoft/PowerToys

.PARAMETER OutputPath
    Path to save JSON output. If not specified, outputs to pipeline.

.EXAMPLE
    .\Get-PrDetails.ps1 -PullRequestNumber 12345
    Returns enriched PR data as JSON.

.EXAMPLE
    .\Get-PrDetails.ps1 -PullRequestNumber 12345 -OutputPath ".\pr-12345-details.json"
    Saves enriched PR data to file.

.NOTES
    Requires: gh CLI authenticated with repo access.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [int]$PullRequestNumber,
    
    [string]$Repository = "microsoft/PowerToys",
    
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

function Write-Info($msg) { Write-Host $msg -ForegroundColor Cyan }
function Write-Warn($msg) { Write-Host $msg -ForegroundColor Yellow }

$owner, $repo = $Repository -split "/"
$now = Get-Date

Write-Info "Fetching details for PR #$PullRequestNumber..."

# 1. Base PR data
$jsonFields = @(
    'number','title','author','createdAt','updatedAt',
    'headRefName','baseRefName','headRefOid',
    'labels','assignees','reviewRequests',
    'isDraft','mergeable','url',
    'additions','deletions','changedFiles',
    'closingIssuesReferences','files'
) -join ','
$prData = gh pr view $PullRequestNumber --repo $Repository --json $jsonFields | ConvertFrom-Json

# 2. Reviews
Write-Info "  Fetching reviews..."
$reviews = gh api "repos/$owner/$repo/pulls/$PullRequestNumber/reviews" 2>$null | ConvertFrom-Json
$reviewData = $reviews | ForEach-Object {
    [PSCustomObject]@{
        Id = $_.id
        User = $_.user.login
        State = $_.state
        SubmittedAt = $_.submitted_at
        BodyLength = if ($_.body) { $_.body.Length } else { 0 }
    }
}

# 3. Comments (issue comments)
Write-Info "  Fetching comments..."
$comments = gh api "repos/$owner/$repo/issues/$PullRequestNumber/comments" 2>$null | ConvertFrom-Json
$commentData = $comments | ForEach-Object {
    [PSCustomObject]@{
        Id = $_.id
        User = $_.user.login
        CreatedAt = $_.created_at
        BodyLength = if ($_.body) { $_.body.Length } else { 0 }
    }
}

# 4. Review comments (inline)
$reviewCommentCount = (gh api "repos/$owner/$repo/pulls/$PullRequestNumber/comments" 2>$null | ConvertFrom-Json).Count

# 5. CI Status
Write-Info "  Fetching CI status..."
$checksRaw = gh pr checks $PullRequestNumber --repo $Repository --json name,state,conclusion 2>$null
$checks = if ($checksRaw) { $checksRaw | ConvertFrom-Json } else { @() }

$checksDetail = $checks | ForEach-Object {
    [PSCustomObject]@{
        Name = $_.name
        State = $_.state
        Conclusion = $_.conclusion
    }
}

$failingChecks = $checks | Where-Object { $_.conclusion -eq "failure" } | ForEach-Object { $_.name }
$checksStatus = if ($checks.Count -eq 0) {
    "NONE"
} elseif ($failingChecks.Count -gt 0) {
    "FAILING"
} elseif ($checks | Where-Object { $_.state -eq "pending" }) {
    "PENDING"
} else {
    "PASSING"
}

# 6. Commits
Write-Info "  Fetching commits..."
$commits = gh api "repos/$owner/$repo/pulls/$PullRequestNumber/commits" 2>$null | ConvertFrom-Json
$commitData = $commits | ForEach-Object {
    [PSCustomObject]@{
        Sha = $_.sha.Substring(0, 7)
        Message = ($_.commit.message -split "`n")[0]
        Author = $_.commit.author.name
        Date = $_.commit.author.date
    }
}

# Calculate derived fields
$authorLogin = $prData.author.login
$lastReviewAt = ($reviewData | Sort-Object SubmittedAt -Descending | Select-Object -First 1).SubmittedAt
$lastCommentAt = ($commentData | Sort-Object CreatedAt -Descending | Select-Object -First 1).CreatedAt
$lastCommentBy = ($commentData | Sort-Object CreatedAt -Descending | Select-Object -First 1).User
$lastCommitAt = ($commitData | Sort-Object Date -Descending | Select-Object -First 1).Date

# Author's last activity (commit or comment)
$authorComments = $commentData | Where-Object { $_.User -eq $authorLogin }
$authorLastComment = ($authorComments | Sort-Object CreatedAt -Descending | Select-Object -First 1).CreatedAt
$authorLastCommit = $lastCommitAt  # Assuming author made commits
$authorLastActivityAt = @($authorLastComment, $authorLastCommit) | 
    Where-Object { $_ } | 
    Sort-Object -Descending | 
    Select-Object -First 1

# Review counts by state
$approvalCount = ($reviewData | Where-Object { $_.State -eq "APPROVED" }).Count
$changesRequestedCount = ($reviewData | Where-Object { $_.State -eq "CHANGES_REQUESTED" }).Count

# Build enrichment object
$enrichment = [PSCustomObject]@{
    Reviews = @($reviewData)
    LastReviewAt = $lastReviewAt
    ApprovalCount = $approvalCount
    ChangesRequestedCount = $changesRequestedCount
    CommentCount = $commentData.Count
    ReviewCommentCount = $reviewCommentCount
    LastCommentAt = $lastCommentAt
    LastCommentBy = $lastCommentBy
    AuthorLastActivityAt = $authorLastActivityAt
    CommitCount = $commitData.Count
    LastCommitAt = $lastCommitAt
    Commits = @($commitData)
    ChecksStatus = $checksStatus
    ChecksDetail = @($checksDetail)
    FailingChecks = @($failingChecks)
}

# Build output
$output = [PSCustomObject]@{
    Number = $prData.number
    Title = $prData.title
    Author = $authorLogin
    Url = $prData.url
    CreatedAt = $prData.createdAt
    UpdatedAt = $prData.updatedAt
    HeadRefOid = $prData.headRefOid
    BaseRefName = $prData.baseRefName
    HeadRefName = $prData.headRefName
    Labels = ($prData.labels | ForEach-Object { $_.name })
    Assignees = ($prData.assignees | ForEach-Object { $_.login })
    ReviewRequests = ($prData.reviewRequests | ForEach-Object { $_.login })
    IsDraft = $prData.isDraft
    Mergeable = $prData.mergeable
    Additions = $prData.additions
    Deletions = $prData.deletions
    ChangedFiles = $prData.changedFiles
    Files = ($prData.files | ForEach-Object { $_.path })
    LinkedIssues = ($prData.closingIssuesReferences | ForEach-Object { $_.number })
    Enrichment = $enrichment
    EnrichedAt = $now.ToString("o")
}

Write-Info "Enrichment complete for PR #$PullRequestNumber"

# Output
if ($OutputPath) {
    $output | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputPath -Encoding UTF8
    Write-Info "Saved to $OutputPath"
} else {
    $output | ConvertTo-Json -Depth 10
}
