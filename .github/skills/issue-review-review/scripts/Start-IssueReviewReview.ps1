<#
.SYNOPSIS
    Meta-review of issue-review outputs to validate scoring and implementation plan quality.

.DESCRIPTION
    Reads the existing overview.md and implementation-plan.md from issue-review,
    cross-checks scores against evidence, validates file paths and patterns,
    and produces a reviewTheReview.md with a quality score (0-100).

    If the quality score is < 90, the signal file indicates that issue-review
    should re-run with the feedback.

.PARAMETER IssueNumber
    GitHub issue number whose review to validate.

.PARAMETER CLIType
    AI CLI to use: copilot or claude. Default: copilot.

.PARAMETER Model
    Copilot CLI model to use (e.g., gpt-5.2-codex).

.PARAMETER Force
    Skip confirmation prompts.

.PARAMETER DryRun
    Show what would be done without executing.

.EXAMPLE
    ./Start-IssueReviewReview.ps1 -IssueNumber 44044

.EXAMPLE
    ./Start-IssueReviewReview.ps1 -IssueNumber 44044 -CLIType copilot -Model gpt-5.2-codex -Force
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [int]$IssueNumber,

    [ValidateSet('copilot', 'claude')]
    [string]$CLIType = 'copilot',

    [string]$Model,

    [switch]$Force,

    [switch]$DryRun,

    [switch]$Help
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptDir 'IssueReviewLib.ps1')

if ($Help) {
    Get-Help $MyInvocation.MyCommand.Path -Full
    return
}

#region Main
try {
    $repoRoot = Get-RepoRoot
    $genFiles = Get-GeneratedFilesPath -RepoRoot $repoRoot
    Info "Repository root: $repoRoot"

    #region Validate prerequisites
    $reviewDir = Join-Path $genFiles "issueReview/$IssueNumber"
    $overviewPath = Join-Path $reviewDir 'overview.md'
    $implPlanPath = Join-Path $reviewDir 'implementation-plan.md'

    if (-not (Test-Path $overviewPath)) {
        throw "overview.md not found for issue #$IssueNumber at: $overviewPath. Run issue-review first."
    }
    if (-not (Test-Path $implPlanPath)) {
        throw "implementation-plan.md not found for issue #$IssueNumber at: $implPlanPath. Run issue-review first."
    }

    Info "Found review files for issue #$IssueNumber"
    Info "  Overview:           $overviewPath"
    Info "  Implementation plan: $implPlanPath"
    #endregion

    #region Determine iteration
    $outputDir = Join-Path $genFiles "issueReviewReview/$IssueNumber"
    Ensure-DirectoryExists -Path $outputDir

    $existingSignalPath = Join-Path $outputDir '.signal'
    $iteration = 1
    if (Test-Path $existingSignalPath) {
        try {
            $existingSignal = Get-Content $existingSignalPath -Raw | ConvertFrom-Json
            $iteration = ([int]$existingSignal.iteration) + 1
            Info "Previous review-review found (iteration $($existingSignal.iteration), score: $($existingSignal.qualityScore))"

            # Archive previous output
            $archiveDir = Join-Path $outputDir "iteration-$($existingSignal.iteration)"
            Ensure-DirectoryExists -Path $archiveDir
            $prevReviewPath = Join-Path $outputDir 'reviewTheReview.md'
            if (Test-Path $prevReviewPath) {
                Copy-Item $prevReviewPath (Join-Path $archiveDir 'reviewTheReview.md') -Force
                Info "Archived previous review to: $archiveDir"
            }
        }
        catch {
            Warn "Could not parse existing signal, starting fresh"
        }
    }

    Info "Starting review-review iteration $iteration for issue #$IssueNumber"
    #endregion

    if ($DryRun) {
        Warn "Dry run mode - would review-review issue #$IssueNumber (iteration $iteration)"
        return
    }

    if (-not $Force) {
        $confirm = Read-Host "Proceed with review-review for issue #$IssueNumber? (y/N)"
        if ($confirm -notmatch '^[yY]') {
            Info "Cancelled."
            return
        }
    }

    #region Build and run AI prompt
    $promptText = @"
TASK: Write a meta-review file to 'Generated Files/issueReviewReview/$IssueNumber/reviewTheReview.md'.

You MUST create this file before finishing. This is your primary deliverable.

Issue number: $IssueNumber
Iteration: $iteration

STEP 1 - Read these inputs:
- Run: gh issue view $IssueNumber --json number,title,body,state,labels,comments
- Read file: Generated Files/issueReview/$IssueNumber/overview.md
- Read file: Generated Files/issueReview/$IssueNumber/implementation-plan.md
$(if ($iteration -gt 1) { "- Read file: Generated Files/issueReviewReview/$IssueNumber/iteration-$($iteration - 1)/reviewTheReview.md" })

STEP 2 - Verify file paths from the implementation plan exist using test -f or ls.
STEP 3 - Verify code patterns from the implementation plan using rg.

STEP 4 - Write the file 'Generated Files/issueReviewReview/$IssueNumber/reviewTheReview.md' with this structure:

# Meta-Review: Issue #$IssueNumber

## Score Validation
| Dimension | Original Score | Verified Score | Evidence |
|-----------|---------------|----------------|----------|
(validate each score dimension from overview.md against actual codebase evidence)

## Implementation Plan Verification
- File paths: which exist, which don't
- Patterns: which are correct, which are wrong
- Task breakdown: are tasks specific and executable?

## Quality Score Breakdown
| Dimension | Weight | Score | Weighted |
|-----------|--------|-------|----------|
| Score Accuracy | 30% | X/100 | X |
| Implementation Correctness | 25% | X/100 | X |
| Risk Assessment | 15% | X/100 | X |
| Completeness | 15% | X/100 | X |
| Actionability | 15% | X/100 | X |
| **Total** | | | **X/100** |

## Review Quality Score: X/100

## Verdict: PASS/NEEDS_IMPROVEMENT/FAIL

## Corrective Feedback
(specific items the review should fix, if any)

CRITICAL: You MUST write the output file. Do NOT just describe what you would do. Actually create the file.
"@

    $mcpConfig = "@$_cfgDir/skills/issue-review-review/references/mcp-config.json"

    switch ($CLIType) {
        'copilot' {
            $cliArgs = @(
                '--additional-mcp-config', $mcpConfig,
                '-p', $promptText,
                '--yolo',
                '-s',
                '--enable-all-github-mcp-tools',
                '--allow-tool', 'github-artifacts',
                '--agent', 'ReviewTheReview'
            )
            if ($Model) {
                $cliArgs += @('--model', $Model)
            }

            Info "Running Copilot CLI for review-review..."
            & copilot @cliArgs 2>&1 | Out-Default
            $exitCode = $LASTEXITCODE
        }
        'claude' {
            $cliArgs = @(
                '--print',
                '--dangerously-skip-permissions',
                '--agent', 'ReviewTheReview',
                '--prompt', $promptText
            )

            Info "Running Claude CLI for review-review..."
            & claude @cliArgs 2>&1 | Out-Default
            $exitCode = $LASTEXITCODE
        }
    }
    #endregion

    #region Parse result and write signal
    $reviewTheReviewPath = Join-Path $outputDir 'reviewTheReview.md'

    if (-not (Test-Path $reviewTheReviewPath)) {
        # CLI may have failed
        Err "reviewTheReview.md was not generated for issue #$IssueNumber"

        @{
            status       = 'failure'
            issueNumber  = $IssueNumber
            timestamp    = (Get-Date).ToString('o')
            qualityScore = 0
            iteration    = $iteration
            outputs      = @()
            needsReReview = $true
            error        = "Output file not generated (exit code: $exitCode)"
        } | ConvertTo-Json | Set-Content $existingSignalPath -Force

        return @{
            IssueNumber   = $IssueNumber
            Status        = 'failure'
            QualityScore  = 0
            Iteration     = $iteration
            NeedsReReview = $true
            Error         = "Output file not generated"
        }
    }

    # Parse quality score from the generated reviewTheReview.md
    $content = Get-Content $reviewTheReviewPath -Raw
    $qualityScore = 0

    # Try to extract "Review Quality Score: X/100"
    if ($content -match 'Review Quality Score:\s*(\d+)/100') {
        $qualityScore = [int]$Matches[1]
    }
    # Also try total from breakdown table: "| **Total** | | | **X/100** |"
    elseif ($content -match '\*\*Total\*\*[^|]*\|[^|]*\|[^|]*\|\s*\*\*(\d+)/100\*\*') {
        $qualityScore = [int]$Matches[1]
    }
    # Fallback: any line with "Quality Score" and a number
    elseif ($content -match 'Quality Score[^\d]*(\d+)') {
        $qualityScore = [int]$Matches[1]
    }

    $needsReReview = $qualityScore -lt 90

    # Determine verdict
    $verdict = if ($qualityScore -ge 90) { 'PASS' }
               elseif ($qualityScore -ge 50) { 'NEEDS_IMPROVEMENT' }
               else { 'FAIL' }

    # Write signal
    $signal = @{
        status        = 'success'
        issueNumber   = $IssueNumber
        timestamp     = (Get-Date).ToString('o')
        qualityScore  = $qualityScore
        iteration     = $iteration
        verdict       = $verdict
        outputs       = @('reviewTheReview.md')
        needsReReview = $needsReReview
    }
    $signal | ConvertTo-Json | Set-Content $existingSignalPath -Force

    if ($needsReReview) {
        Warn "Review-review score: $qualityScore/100 (iteration $iteration) — NEEDS RE-REVIEW"
        Warn "Feedback written to: $reviewTheReviewPath"
        Warn "Re-run issue-review with: -FeedbackFile `"$reviewTheReviewPath`""
    }
    else {
        Success "Review-review score: $qualityScore/100 (iteration $iteration) — PASS"
        Success "Review quality is sufficient. Proceed to issue-fix."
    }

    Info "Signal: $existingSignalPath"
    #endregion

    return @{
        IssueNumber   = $IssueNumber
        Status        = 'success'
        QualityScore  = $qualityScore
        Iteration     = $iteration
        Verdict       = $verdict
        NeedsReReview = $needsReReview
    }
}
catch {
    Err "Error: $($_.Exception.Message)"
    
    # Write failure signal
    $outputDir = Join-Path (Get-GeneratedFilesPath -RepoRoot (Get-RepoRoot)) "issueReviewReview/$IssueNumber"
    Ensure-DirectoryExists -Path $outputDir
    $signalPath = Join-Path $outputDir '.signal'
    @{
        status        = 'failure'
        issueNumber   = $IssueNumber
        timestamp     = (Get-Date).ToString('o')
        qualityScore  = 0
        iteration     = 1
        outputs       = @()
        needsReReview = $true
        error         = $_.Exception.Message
    } | ConvertTo-Json | Set-Content $signalPath -Force

    return @{
        IssueNumber   = $IssueNumber
        Status        = 'failure'
        QualityScore  = 0
        Iteration     = 1
        NeedsReReview = $true
        Error         = $_.Exception.Message
    }
}
#endregion
