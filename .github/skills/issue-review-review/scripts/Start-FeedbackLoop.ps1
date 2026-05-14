<#
.SYNOPSIS
    Orchestrate the feedback loop: re-run issue-review with corrections, then re-review.

.DESCRIPTION
    For each issue whose review-review score is below the threshold:
    1. Re-run issue-review with the corrective feedback from reviewTheReview.md
    2. Re-run review-review on the updated review files
    3. Repeat up to MaxIterations times or until the score passes

.PARAMETER ThrottleLimit
    Maximum parallel tasks. Default: 3.

.PARAMETER QualityThreshold
    Score threshold for PASS. Default: 90.

.PARAMETER MaxIterations
    Maximum feedback loop iterations per issue. Default: 3.

.PARAMETER CLIType
    AI CLI type (copilot/claude). Default: copilot.

.PARAMETER Model
    Copilot CLI model override (e.g., claude-sonnet-4).

.PARAMETER IssueNumbers
    Optional: specific issue numbers to process. If omitted, processes all issues with needsReReview=true.

.PARAMETER Force
    Skip confirmation prompts.

.EXAMPLE
    ./Start-FeedbackLoop.ps1 -CLIType copilot -Model claude-sonnet-4 -ThrottleLimit 3 -Force

.EXAMPLE
    # Process specific issues only
    ./Start-FeedbackLoop.ps1 -IssueNumbers @(1929, 1934) -CLIType copilot -Model claude-sonnet-4 -Force
#>
[CmdletBinding()]
param(
    [int]$ThrottleLimit = 3,

    [int]$QualityThreshold = 90,

    [int]$MaxIterations = 3,

    [ValidateSet('copilot', 'claude')]
    [string]$CLIType = 'copilot',

    [string]$Model,

    [int[]]$IssueNumbers,

    [switch]$Force
)

$ErrorActionPreference = 'Continue'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')

# Resolve config directory name (.github or .claude) from script location
$_cfgDir = if ($PSScriptRoot -match '[\\/](\.github|\.claude)[\\/]') { $Matches[1] } else { '.github' }
$genFiles = Join-Path $repoRoot 'Generated Files'
$reviewReviewDir = Join-Path $genFiles 'issueReviewReview'
$issueReviewDir = Join-Path $genFiles 'issueReview'

$bulkReviewScript = Join-Path $repoRoot "$_cfgDir\skills\issue-review\scripts\Start-BulkIssueReview.ps1"
$reviewReviewScript = Join-Path $repoRoot "$_cfgDir\skills\issue-review-review\scripts\Start-IssueReviewReview.ps1"

Write-Host "=== FEEDBACK LOOP ORCHESTRATOR ===" -ForegroundColor Cyan
Write-Host "Repository root: $repoRoot"
Write-Host "Quality threshold: $QualityThreshold"
Write-Host "Max iterations: $MaxIterations"
Write-Host "Throttle limit: $ThrottleLimit"
Write-Host "CLI: $CLIType $(if ($Model) { "(model: $Model)" })"
Write-Host ""

# ------------------------------------------------------------------
# Step 1: Identify issues that need re-review
# ------------------------------------------------------------------
if ($IssueNumbers -and $IssueNumbers.Count -gt 0) {
    # Use explicit list
    $needsWork = $IssueNumbers | ForEach-Object {
        $signalPath = Join-Path $reviewReviewDir "$_\.signal"
        if (Test-Path $signalPath) {
            $signal = Get-Content $signalPath -Raw | ConvertFrom-Json
            [PSCustomObject]@{
                IssueNumber   = $_
                CurrentScore  = [int]$signal.qualityScore
                Iteration     = [int]$signal.iteration
                FeedbackFile  = Join-Path $reviewReviewDir "$_\reviewTheReview.md"
            }
        }
        else {
            Write-Host "  Warning: No signal for issue #$_ — skipping" -ForegroundColor Yellow
        }
    } | Where-Object { $_ }
}
else {
    # Auto-discover from signals with needsReReview = true
    $needsWork = Get-ChildItem $reviewReviewDir -Directory -ErrorAction SilentlyContinue |
        Where-Object { Test-Path (Join-Path $_.FullName '.signal') } |
        ForEach-Object {
            $signal = Get-Content (Join-Path $_.FullName '.signal') -Raw | ConvertFrom-Json
            if ($signal.needsReReview -eq $true -and [int]$signal.iteration -lt $MaxIterations) {
                [PSCustomObject]@{
                    IssueNumber   = [int]$signal.issueNumber
                    CurrentScore  = [int]$signal.qualityScore
                    Iteration     = [int]$signal.iteration
                    FeedbackFile  = Join-Path $_.FullName 'reviewTheReview.md'
                }
            }
        } | Sort-Object IssueNumber
}

if (-not $needsWork -or $needsWork.Count -eq 0) {
    Write-Host "No issues need re-review. All passed or reached max iterations." -ForegroundColor Green
    return
}

Write-Host "Issues needing feedback loop: $($needsWork.Count)" -ForegroundColor Yellow
Write-Host ("-" * 70)
$needsWork | Format-Table IssueNumber, CurrentScore, Iteration -AutoSize | Out-String | Write-Host
Write-Host ("-" * 70)

if (-not $Force) {
    $confirm = Read-Host "Proceed with feedback loop for $($needsWork.Count) issues? (y/N)"
    if ($confirm -notmatch '^[yY]') {
        Write-Host "Cancelled."
        return
    }
}

# ------------------------------------------------------------------
# Step 2: Run feedback loop in parallel
# ------------------------------------------------------------------
$startTime = Get-Date

$results = $needsWork | ForEach-Object -Parallel {
    $item = $PSItem
    $repoRoot        = $using:repoRoot
    $bulkScript       = $using:bulkReviewScript
    $reviewScript     = $using:reviewReviewScript
    $cliType          = $using:CLIType
    $model            = $using:Model
    $qualityThreshold = $using:QualityThreshold
    $maxIter          = $using:MaxIterations

    Set-Location $repoRoot

    $issueNum      = $item.IssueNumber
    $currentScore  = $item.CurrentScore
    $currentIter   = $item.Iteration
    $feedbackFile  = $item.FeedbackFile

    Write-Host "[#$issueNum] Starting feedback loop (current score: $currentScore, iteration: $currentIter)" -ForegroundColor Cyan

    # Phase A: Re-run issue-review with corrective feedback
    Write-Host "[#$issueNum] Phase A: Re-running issue-review with feedback..." -ForegroundColor Yellow
    $bulkParams = @{
        IssueNumber = $issueNum
        CLIType     = $cliType
        Force       = $true
    }
    if ($model) { $bulkParams.Model = $model }
    if (Test-Path $feedbackFile) {
        $bulkParams.FeedbackFile = $feedbackFile
    }

    try {
        & $bulkScript @bulkParams 2>&1 | ForEach-Object { Write-Host "[#$issueNum] $_" }
    }
    catch {
        Write-Host "[#$issueNum] Phase A error: $($_.Exception.Message)" -ForegroundColor Red
        return [PSCustomObject]@{
            IssueNumber   = $issueNum
            OldScore      = $currentScore
            NewScore      = 0
            Iteration     = $currentIter
            Status        = 'FAILED_REVIEW'
            Error         = $_.Exception.Message
        }
    }

    # Phase B: Re-run review-review on the updated files
    Write-Host "[#$issueNum] Phase B: Re-running review-review..." -ForegroundColor Yellow
    $rrParams = @{
        IssueNumber = $issueNum
        CLIType     = $cliType
        Force       = $true
    }
    if ($model) { $rrParams.Model = $model }

    try {
        & $reviewScript @rrParams 2>&1 | ForEach-Object { Write-Host "[#$issueNum] $_" }
    }
    catch {
        Write-Host "[#$issueNum] Phase B error: $($_.Exception.Message)" -ForegroundColor Red
        return [PSCustomObject]@{
            IssueNumber   = $issueNum
            OldScore      = $currentScore
            NewScore      = 0
            Iteration     = $currentIter + 1
            Status        = 'FAILED_REVIEW_REVIEW'
            Error         = $_.Exception.Message
        }
    }

    # Read updated signal
    $signalPath = Join-Path $using:reviewReviewDir "$issueNum\.signal"
    if (Test-Path $signalPath) {
        $newSignal = Get-Content $signalPath -Raw | ConvertFrom-Json
        $newScore = [int]$newSignal.qualityScore
        $newIter  = [int]$newSignal.iteration
        $verdict  = $newSignal.verdict

        $status = if ($newScore -ge $qualityThreshold) { 'IMPROVED_TO_PASS' }
                  elseif ($newScore -gt $currentScore)  { 'IMPROVED' }
                  elseif ($newScore -eq $currentScore)  { 'NO_CHANGE' }
                  else                                  { 'REGRESSED' }

        Write-Host "[#$issueNum] Done: $currentScore → $newScore ($status)" -ForegroundColor $(
            if ($status -eq 'IMPROVED_TO_PASS') { 'Green' }
            elseif ($status -eq 'IMPROVED')     { 'Yellow' }
            else                                { 'Red' }
        )

        [PSCustomObject]@{
            IssueNumber   = $issueNum
            OldScore      = $currentScore
            NewScore      = $newScore
            Iteration     = $newIter
            Status        = $status
            Verdict       = $verdict
        }
    }
    else {
        [PSCustomObject]@{
            IssueNumber   = $issueNum
            OldScore      = $currentScore
            NewScore      = 0
            Iteration     = $currentIter + 1
            Status        = 'NO_SIGNAL'
            Error         = 'No signal file after review-review'
        }
    }
} -ThrottleLimit $ThrottleLimit

$duration = (Get-Date) - $startTime

# ------------------------------------------------------------------
# Step 3: Summary
# ------------------------------------------------------------------
Write-Host ""
Write-Host ("=" * 70) -ForegroundColor Cyan
Write-Host " FEEDBACK LOOP SUMMARY" -ForegroundColor Cyan
Write-Host ("=" * 70) -ForegroundColor Cyan

$improved     = @($results | Where-Object Status -eq 'IMPROVED_TO_PASS')
$partial      = @($results | Where-Object Status -eq 'IMPROVED')
$noChange     = @($results | Where-Object Status -eq 'NO_CHANGE')
$regressed    = @($results | Where-Object Status -eq 'REGRESSED')
$errors       = @($results | Where-Object { $_.Status -like 'FAILED*' -or $_.Status -eq 'NO_SIGNAL' })

Write-Host "Total processed:  $($results.Count)"
Write-Host "Improved to PASS: $($improved.Count)" -ForegroundColor Green
Write-Host "Improved (below): $($partial.Count)" -ForegroundColor Yellow
Write-Host "No change:        $($noChange.Count)" -ForegroundColor DarkYellow
Write-Host "Regressed:        $($regressed.Count)" -ForegroundColor Red
Write-Host "Errors:           $($errors.Count)" -ForegroundColor Red
Write-Host "Duration:         $($duration.ToString('hh\:mm\:ss'))"
Write-Host ("=" * 70) -ForegroundColor Cyan

# Show details
if ($results.Count -gt 0) {
    Write-Host ""
    Write-Host "Details:" -ForegroundColor White
    $results | Sort-Object NewScore -Descending | Format-Table IssueNumber, OldScore, NewScore, Status, Iteration -AutoSize | Out-String | Write-Host
}

# Count remaining issues that still need work
$stillNeedsWork = Get-ChildItem $reviewReviewDir -Directory -ErrorAction SilentlyContinue |
    Where-Object { Test-Path (Join-Path $_.FullName '.signal') } |
    ForEach-Object {
        $signal = Get-Content (Join-Path $_.FullName '.signal') -Raw | ConvertFrom-Json
        if ($signal.needsReReview -eq $true -and [int]$signal.iteration -lt $MaxIterations) { $signal }
    }

if ($stillNeedsWork.Count -gt 0) {
    Write-Host "`nStill needs improvement: $($stillNeedsWork.Count) issues" -ForegroundColor Yellow
    Write-Host "Run this script again for another iteration." -ForegroundColor Yellow
}
else {
    Write-Host "`nAll issues have either passed or reached max iterations!" -ForegroundColor Green
}

# Return results for pipeline
return $results
