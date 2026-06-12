<#
.SYNOPSIS
    Suggests reviewers for a PR based on file ownership and history.

.DESCRIPTION
    Analyzes changed files in a PR and suggests appropriate reviewers using:
    1. CODEOWNERS file matches
    2. Recent reviewers of similar PRs (by area label)
    3. Recent committers to the changed files
    4. Fallback to team defaults

.PARAMETER PullRequestNumber
    The PR number to analyze.

.PARAMETER Repository
    GitHub repository in owner/repo format. Default: microsoft/PowerToys

.PARAMETER ChangedFiles
    Array of file paths changed in the PR. If not provided, fetches from GitHub.

.PARAMETER CacheDir
    Directory for caching reviewer history. Default: __cache

.PARAMETER MaxSuggestions
    Maximum number of reviewers to suggest. Default: 5

.EXAMPLE
    .\Get-ReviewerSuggestions.ps1 -PullRequestNumber 12345
    Suggests reviewers for PR #12345.

.EXAMPLE
    .\Get-ReviewerSuggestions.ps1 -PullRequestNumber 12345 -ChangedFiles @("src/modules/FancyZones/file.cpp")
    Suggests reviewers based on provided file list.

.NOTES
    Requires: gh CLI authenticated with repo access.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [int]$PullRequestNumber,
    
    [string]$Repository = "microsoft/PowerToys",
    
    [string[]]$ChangedFiles,
    
    [string]$CacheDir = "__cache",
    
    [int]$MaxSuggestions = 5,
    
    [string]$PrAuthor
)

$ErrorActionPreference = "Stop"

function Write-Info($msg) { Write-Host $msg -ForegroundColor Cyan }

$owner, $repo = $Repository -split "/"

# Get PR author and changed files if not provided
if (-not $PrAuthor -or -not $ChangedFiles) {
    Write-Info "Fetching PR details..."
    $prData = gh pr view $PullRequestNumber --repo $Repository --json author,files | ConvertFrom-Json
    if (-not $PrAuthor) { $PrAuthor = $prData.author.login }
    if (-not $ChangedFiles) { $ChangedFiles = $prData.files | ForEach-Object { $_.path } }
}

$suggestions = @()

# 1. Check CODEOWNERS
Write-Info "Checking CODEOWNERS..."
$codeownersContent = $null
try {
    $codeownersContent = gh api "repos/$owner/$repo/contents/.github/CODEOWNERS" --jq ".content" 2>$null
    if ($codeownersContent) {
        $codeownersContent = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($codeownersContent))
    }
} catch {
    # CODEOWNERS may not exist
}

if ($codeownersContent) {
    $codeownersLines = $codeownersContent -split "`n" | Where-Object { $_ -and $_ -notmatch "^\s*#" }
    
    foreach ($file in $ChangedFiles) {
        foreach ($line in $codeownersLines) {
            $parts = $line -split "\s+"
            if ($parts.Count -ge 2) {
                $pattern = $parts[0]
                $owners = $parts[1..($parts.Count - 1)] | ForEach-Object { $_ -replace "^@", "" }
                
                # Simple pattern matching (not full glob support)
                $regexPattern = $pattern -replace "\*\*", ".*" -replace "\*", "[^/]*"
                if ($file -match $regexPattern) {
                    foreach ($ownerEntry in $owners) {
                        if ($ownerEntry -ne $PrAuthor) {
                            $suggestions += [PSCustomObject]@{
                                User = $ownerEntry
                                Reason = "CODEOWNERS for $pattern"
                                Confidence = "High"
                                Source = "CODEOWNERS"
                            }
                        }
                    }
                }
            }
        }
    }
}

# 2. Find area labels and recent reviewers
Write-Info "Finding recent reviewers by area..."
$prLabels = gh pr view $PullRequestNumber --repo $Repository --json labels --jq ".labels[].name" 2>$null
$areaLabels = $prLabels | Where-Object { $_ -match "^Area-" }

foreach ($areaLabel in $areaLabels) {
    # Get recent PRs with this label that have been reviewed
    $recentPrs = gh pr list --repo $Repository --state merged --label $areaLabel --limit 10 --json number,reviews 2>$null | ConvertFrom-Json
    
    $recentReviewers = @{}
    foreach ($rpr in $recentPrs) {
        foreach ($review in $rpr.reviews) {
            if ($review.author.login -and $review.author.login -ne $PrAuthor) {
                $reviewer = $review.author.login
                if (-not $recentReviewers[$reviewer]) {
                    $recentReviewers[$reviewer] = 0
                }
                $recentReviewers[$reviewer]++
            }
        }
    }
    
    $topReviewers = $recentReviewers.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 3
    foreach ($entry in $topReviewers) {
        $suggestions += [PSCustomObject]@{
            User = $entry.Key
            Reason = "Recently reviewed $($entry.Value) PRs with label $areaLabel"
            Confidence = "Medium"
            Source = "RecentReviewer"
        }
    }
}

# 3. Git blame for changed files (recent committers)
Write-Info "Finding recent committers to changed files..."
$topFiles = $ChangedFiles | Select-Object -First 5  # Limit to avoid too many API calls

foreach ($file in $topFiles) {
    try {
        $commits = gh api "repos/$owner/$repo/commits?path=$([Uri]::EscapeDataString($file))&per_page=10" 2>$null | ConvertFrom-Json
        $committers = @{}
        foreach ($commit in $commits) {
            if ($commit.author -and $commit.author.login -and $commit.author.login -ne $PrAuthor) {
                $committer = $commit.author.login
                if (-not $committers[$committer]) {
                    $committers[$committer] = 0
                }
                $committers[$committer]++
            }
        }
        
        $topCommitter = $committers.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1
        if ($topCommitter) {
            $suggestions += [PSCustomObject]@{
                User = $topCommitter.Key
                Reason = "Frequent committer to $file"
                Confidence = "Medium"
                Source = "GitHistory"
            }
        }
    } catch {
        # File may be new or API error
    }
}

# Deduplicate and rank
$uniqueSuggestions = @{}
foreach ($s in $suggestions) {
    if (-not $uniqueSuggestions[$s.User]) {
        $uniqueSuggestions[$s.User] = $s
    } else {
        # Keep the higher confidence one
        $existing = $uniqueSuggestions[$s.User]
        $confOrder = @{ "High" = 3; "Medium" = 2; "Low" = 1 }
        if ($confOrder[$s.Confidence] -gt $confOrder[$existing.Confidence]) {
            $uniqueSuggestions[$s.User] = $s
        }
    }
}

# Sort by confidence and return top N
$confOrder = @{ "High" = 3; "Medium" = 2; "Low" = 1 }
$finalSuggestions = $uniqueSuggestions.Values | 
    Sort-Object { $confOrder[$_.Confidence] } -Descending |
    Select-Object -First $MaxSuggestions

# Output
$output = [PSCustomObject]@{
    PullRequestNumber = $PullRequestNumber
    Author = $PrAuthor
    ChangedFilesCount = $ChangedFiles.Count
    Suggestions = @($finalSuggestions)
}

$output | ConvertTo-Json -Depth 5

Write-Info "`nTop suggestions:"
foreach ($s in $finalSuggestions) {
    Write-Info "  @$($s.User) - $($s.Reason) [$($s.Confidence)]"
}
