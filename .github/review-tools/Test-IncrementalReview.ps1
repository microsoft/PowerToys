<#
.SYNOPSIS
    Tests and previews incremental review detection for a pull request.

.DESCRIPTION
    This helper script validates the incremental review detection logic by analyzing an existing
    PR review folder. It reads the last reviewed SHA from the overview file, compares it with
    the current PR state, and displays detailed information about what has changed.
    
    This is useful for:
    - Testing the incremental review system before running a full review
    - Understanding what changed since the last review iteration
    - Verifying that review metadata was properly recorded

.PARAMETER PullRequestNumber
    The pull request number to test incremental review detection for.

.PARAMETER RepositoryOwner
    The GitHub repository owner. Defaults to "microsoft".

.PARAMETER RepositoryName
    The GitHub repository name. Defaults to "PowerToys".

.OUTPUTS
    Colored console output displaying:
    - Current and last reviewed commit SHAs
    - Whether incremental review is possible
    - List of new commits since last review
    - List of changed files with status indicators
    - Recommended review strategy

.EXAMPLE
    .\Test-IncrementalReview.ps1 -PullRequestNumber 42374
    Tests incremental review detection for PR #42374.

.EXAMPLE
    .\Test-IncrementalReview.ps1 -PullRequestNumber 42374 -RepositoryOwner "myorg" -RepositoryName "myrepo"
    Tests incremental review for a PR in a different repository.

.NOTES
    Requires GitHub CLI (gh) to be installed and authenticated.
    Run 'gh auth login' if not already authenticated.
    
    Prerequisites:
    - PR review folder must exist at "Generated Files\prReview\{PRNumber}"
    - 00-OVERVIEW.md must exist in the review folder
    - For incremental detection, overview must contain "Last reviewed SHA" metadata

.LINK
    https://cli.github.com/
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = "Pull request number to test")]
    [int]$PullRequestNumber,

    [Parameter(Mandatory = $false, HelpMessage = "Repository owner")]
    [string]$RepositoryOwner = "microsoft",

    [Parameter(Mandatory = $false, HelpMessage = "Repository name")]
    [string]$RepositoryName = "PowerToys"
)

# Resolve paths to review folder and overview file
$repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$reviewFolderPath = Join-Path $repositoryRoot "Generated Files\prReview\$PullRequestNumber"
$overviewFilePath = Join-Path $reviewFolderPath "00-OVERVIEW.md"

Write-Host "=== Testing Incremental Review for PR #$PullRequestNumber ===" -ForegroundColor Cyan
Write-Host ""

# Check if review folder exists
if (-not (Test-Path $reviewFolderPath)) {
    Write-Host "❌ Review folder not found: $reviewFolderPath" -ForegroundColor Red
    Write-Host "This appears to be a new review (iteration 1)" -ForegroundColor Yellow
    exit 0
}

# Check if overview file exists
if (-not (Test-Path $overviewFilePath)) {
    Write-Host "❌ Overview file not found: $overviewFilePath" -ForegroundColor Red
    Write-Host "This appears to be an incomplete review" -ForegroundColor Yellow
    exit 0
}

# Read overview file and extract last reviewed SHA
Write-Host "📄 Reading overview file..." -ForegroundColor Green
$overviewFileContent = Get-Content $overviewFilePath -Raw

if ($overviewFileContent -match '\*\*Last reviewed SHA:\*\*\s+(\w+)') {
    $lastReviewedSha = $Matches[1]
    Write-Host "✅ Found last reviewed SHA: $lastReviewedSha" -ForegroundColor Green
} else {
    Write-Host "⚠️  No 'Last reviewed SHA' found in overview - this may be an old format" -ForegroundColor Yellow
    Write-Host "Proceeding without incremental detection (full review will be needed)" -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "🔍 Running incremental change detection..." -ForegroundColor Cyan

# Call the incremental changes detection script
$incrementalChangesScriptPath = Join-Path $PSScriptRoot "Get-PrIncrementalChanges.ps1"
if (-not (Test-Path $incrementalChangesScriptPath)) {
    Write-Host "❌ Script not found: $incrementalChangesScriptPath" -ForegroundColor Red
    exit 1
}

try {
    $analysisResult = & $incrementalChangesScriptPath `
        -PullRequestNumber $PullRequestNumber `
        -LastReviewedCommitSha $lastReviewedSha `
        -RepositoryOwner $RepositoryOwner `
        -RepositoryName $RepositoryName | ConvertFrom-Json
    
    # Display analysis results
    Write-Host ""
    Write-Host "=== Incremental Review Analysis ===" -ForegroundColor Cyan
    Write-Host "Current HEAD SHA: $($analysisResult.CurrentHeadSha)" -ForegroundColor White
    Write-Host "Last reviewed SHA: $($analysisResult.LastReviewedSha)" -ForegroundColor White
    Write-Host "Base branch: $($analysisResult.BaseRefName)" -ForegroundColor White
    Write-Host "Head branch: $($analysisResult.HeadRefName)" -ForegroundColor White
    Write-Host ""
    Write-Host "Is incremental? $($analysisResult.IsIncremental)" -ForegroundColor $(if ($analysisResult.IsIncremental) { "Green" } else { "Yellow" })
    Write-Host "Need full review? $($analysisResult.NeedFullReview)" -ForegroundColor $(if ($analysisResult.NeedFullReview) { "Yellow" } else { "Green" })
    Write-Host ""
    Write-Host "Summary: $($analysisResult.Summary)" -ForegroundColor Cyan
    Write-Host ""
    
    # Display new commits if any
    if ($analysisResult.NewCommits -and $analysisResult.NewCommits.Count -gt 0) {
        Write-Host "📝 New commits ($($analysisResult.NewCommits.Count)):" -ForegroundColor Green
        foreach ($commit in $analysisResult.NewCommits) {
            Write-Host "  - $($commit.Sha): $($commit.Message)" -ForegroundColor Gray
        }
        Write-Host ""
    }
    
    # Display changed files if any
    if ($analysisResult.ChangedFiles -and $analysisResult.ChangedFiles.Count -gt 0) {
        Write-Host "📁 Changed files ($($analysisResult.ChangedFiles.Count)):" -ForegroundColor Green
        foreach ($file in $analysisResult.ChangedFiles) {
            $statusDisplayColor = switch ($file.Status) {
                "added"    { "Green" }
                "removed"  { "Red" }
                "modified" { "Yellow" }
                "renamed"  { "Cyan" }
                default    { "White" }
            }
            Write-Host "  - [$($file.Status)] $($file.Filename) (+$($file.Additions)/-$($file.Deletions))" -ForegroundColor $statusDisplayColor
        }
        Write-Host ""
    }
    
    # Suggest review strategy based on analysis
    Write-Host "=== Recommended Review Strategy ===" -ForegroundColor Cyan
    if ($analysisResult.NeedFullReview) {
        Write-Host "🔄 Full review recommended" -ForegroundColor Yellow
    } elseif ($analysisResult.IsIncremental -and ($analysisResult.ChangedFiles.Count -eq 0)) {
        Write-Host "✅ No changes detected - no review needed" -ForegroundColor Green
    } elseif ($analysisResult.IsIncremental) {
        Write-Host "⚡ Incremental review possible - review only changed files" -ForegroundColor Green
        Write-Host "💡 Consider applying smart step filtering based on file types" -ForegroundColor Cyan
    }
    
} catch {
    Write-Host "❌ Error running incremental change detection: $_" -ForegroundColor Red
    exit 1
}
