<#!
.SYNOPSIS
    Run the complete issue-to-PR cycle: fix issues, create PRs, review, and fix comments.

.DESCRIPTION
    Orchestrates the full workflow:
    1. Find high-confidence issues matching criteria
    2. Create worktrees and run auto-fix for each issue
    3. Commit changes and create PRs
    4. Run PR review workflow (assign Copilot, review, fix comments)

.PARAMETER MinFeasibilityScore
    Minimum Technical Feasibility score. Default: 70.

.PARAMETER MinClarityScore
    Minimum Requirement Clarity score. Default: 70.

.PARAMETER MaxEffortDays
    Maximum effort in days. Default: 10.

.PARAMETER ExcludeIssues
    Array of issue numbers to exclude (already processed).

.PARAMETER CLIType
    AI CLI to use: copilot or claude. Default: copilot.

.PARAMETER DryRun
    Show what would be done without executing.

.PARAMETER SkipExisting
    Skip issues that already have worktrees or PRs.

.EXAMPLE
    ./Start-FullIssueCycle.ps1 -MinFeasibilityScore 70 -MinClarityScore 70 -MaxEffortDays 10

.EXAMPLE
    ./Start-FullIssueCycle.ps1 -ExcludeIssues 44044,45029,32950,35703,44480 -DryRun
#>

[CmdletBinding()]
param(
    [string]$Labels = '',
    [int]$Limit = 500,  # GitHub API max is 1000, default to 500 to get most issues
    [int]$MinFeasibilityScore = 70,
    [int]$MinClarityScore = 70,
    [int]$MaxEffortDays = 10,
    [int[]]$ExcludeIssues = @(),
    [ValidateSet('copilot', 'claude')]
    [string]$CLIType = 'copilot',
    [int]$FixThrottleLimit = 5,
    [int]$PRThrottleLimit = 5,
    [int]$ReviewThrottleLimit = 3,
    [switch]$DryRun,
    [switch]$SkipExisting,
    [switch]$SkipReview,
    [switch]$Force,
    [switch]$Help
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$skillsDir = Split-Path -Parent (Split-Path -Parent $scriptDir)  # .github/skills
. (Join-Path $scriptDir 'IssueReviewLib.ps1')

# Paths to other skills' scripts
$issueFixScript = Join-Path $skillsDir 'issue-fix/scripts/Start-IssueAutoFix.ps1'
$submitPRScript = Join-Path $skillsDir 'submit-pr/scripts/Submit-IssueFixes.ps1'
$prReviewScript = Join-Path $skillsDir 'pr-review/scripts/Start-PRReviewWorkflow.ps1'

$repoRoot = Get-RepoRoot
$worktreeLib = Join-Path $repoRoot 'tools/build/WorktreeLib.ps1'
if (Test-Path $worktreeLib) {
    . $worktreeLib
}

if ($Help) {
    Get-Help $MyInvocation.MyCommand.Path -Full
    return
}

#region Helper Functions
function Get-ExistingIssuePRs {
    <#
    .SYNOPSIS
        Get ALL issues that already have PRs (open, closed, or merged) - checking GitHub directly.
    #>
    param(
        [int[]]$IssueNumbers
    )
    
    $existingPRs = @{}
    
    foreach ($issueNum in $IssueNumbers) {
        # Check if there's a PR that mentions this issue (any state: open, closed, merged)
        $prs = gh pr list --search "fixes #$issueNum OR closes #$issueNum OR resolves #$issueNum" --state all --json number,url,headRefName,state 2>$null | ConvertFrom-Json
        if ($prs -and $prs.Count -gt 0) {
            $existingPRs[$issueNum] = @{
                PRNumber = $prs[0].number
                PRUrl = $prs[0].url
                Branch = $prs[0].headRefName
                State = $prs[0].state
            }
            continue
        }
        
        # Also check for branch pattern issue/<number>* (any state)
        $branchPrs = gh pr list --head "issue/$issueNum" --state all --json number,url,headRefName,state 2>$null | ConvertFrom-Json
        if (-not $branchPrs -or $branchPrs.Count -eq 0) {
            # Try with wildcard search via gh api
            $branchPrs = gh pr list --state all --json number,url,headRefName,state 2>$null | ConvertFrom-Json | Where-Object { $_.headRefName -like "issue/$issueNum*" }
        }
        if ($branchPrs -and $branchPrs.Count -gt 0) {
            $existingPRs[$issueNum] = @{
                PRNumber = $branchPrs[0].number
                PRUrl = $branchPrs[0].url
                Branch = $branchPrs[0].headRefName
                State = $branchPrs[0].state
            }
        }
    }
    
    return $existingPRs
}

function Get-ExistingWorktrees {
    <#
    .SYNOPSIS
        Get issues that already have worktrees.
    #>
    $existingWorktrees = @{}
    $worktrees = Get-WorktreeEntries | Where-Object { $_.Branch -like 'issue/*' }
    
    foreach ($wt in $worktrees) {
        if ($wt.Branch -match 'issue/(\d+)') {
            $issueNum = [int]$Matches[1]
            $existingWorktrees[$issueNum] = $wt.Path
        }
    }
    
    return $existingWorktrees
}
#endregion

#region Main Script
try {
    $startTime = Get-Date
    
    Info "=" * 80
    Info "FULL ISSUE-TO-PR CYCLE"
    Info "=" * 80
    Info "Repository root: $repoRoot"
    Info "CLI type: $CLIType"
    if ($Labels) {
        Info "Labels filter: $Labels"
    }
    Info "Criteria: Feasibility >= $MinFeasibilityScore, Clarity >= $MinClarityScore, Effort <= $MaxEffortDays days"
    
    # Step 0: Review issues first (if labels specified and not skipping review)
    if ($Labels -and -not $SkipReview) {
        Info "`n" + ("=" * 60)
        Info "STEP 0: Reviewing issues with label '$Labels'"
        Info ("=" * 60)
        
        $reviewScript = Join-Path $scriptDir '../../issue-review/scripts/Start-BulkIssueReview.ps1'
        if (Test-Path $reviewScript) {
            $reviewArgs = @{
                Labels = $Labels
                Limit = $Limit
                CLIType = $CLIType
                Force = $Force
            }
            if ($DryRun) {
                Info "[DRY RUN] Would run: Start-BulkIssueReview.ps1 -Labels '$Labels' -Limit $Limit -CLIType $CLIType -Force"
            } else {
                Info "Running bulk issue review..."
                & $reviewScript @reviewArgs
            }
        } else {
            Warn "Review script not found at: $reviewScript"
            Warn "Proceeding with existing review data..."
        }
    }
    
    # Step 1: Find high-confidence issues
    Info "`n" + ("=" * 60)
    Info "STEP 1: Finding high-confidence issues"
    Info ("=" * 60)
    
    # If labels specified, get the list of issue numbers with that label first
    # This ensures we ONLY look at issues with the specified label, not all reviewed issues
    $filterIssueNumbers = @()
    if ($Labels) {
        Info "Fetching issues with label '$Labels' from GitHub..."
        $labeledIssues = gh issue list --repo microsoft/PowerToys --label "$Labels" --state open --limit $Limit --json number 2>$null | ConvertFrom-Json
        $filterIssueNumbers = @($labeledIssues | ForEach-Object { $_.number })
        Info "Found $($filterIssueNumbers.Count) issues with label '$Labels'"
    }
    
    $highConfidence = Get-HighConfidenceIssues `
        -RepoRoot $repoRoot `
        -MinFeasibilityScore $MinFeasibilityScore `
        -MinClarityScore $MinClarityScore `
        -MaxEffortDays $MaxEffortDays `
        -FilterIssueNumbers $filterIssueNumbers

    Info "Found $($highConfidence.Count) high-confidence issues matching criteria"

    if ($highConfidence.Count -eq 0) {
        Warn "No issues found matching criteria."
        return
    }

    # Get issue numbers for checking
    $issueNumbers = $highConfidence | ForEach-Object { $_.IssueNumber }
    
    # Get existing PRs to skip (check GitHub directly)
    Info "Checking for existing PRs..."
    $existingPRs = Get-ExistingIssuePRs -IssueNumbers $issueNumbers
    Info "Found $($existingPRs.Count) issues with existing PRs"

    # Filter out excluded issues and those with existing PRs
    $issuesToProcess = $highConfidence | Where-Object {
        $issueNum = $_.IssueNumber
        $excluded = $issueNum -in $ExcludeIssues
        $hasPR = $existingPRs.ContainsKey($issueNum)
        
        if ($excluded) {
            Info "  Excluding #$issueNum (in exclude list)"
        }
        if ($hasPR -and $SkipExisting) {
            $prState = $existingPRs[$issueNum].State
            Info "  Skipping #$issueNum (has $prState PR #$($existingPRs[$issueNum].PRNumber))"
        }
        
        -not $excluded -and (-not $hasPR -or -not $SkipExisting)
    }

    if ($issuesToProcess.Count -eq 0) {
        Warn "No new issues to process after filtering."
        return
    }

    Info "`nIssues to process: $($issuesToProcess.Count)"
    Info ("-" * 80)
    foreach ($issue in $issuesToProcess) {
        $prInfo = if ($existingPRs.ContainsKey($issue.IssueNumber)) { 
            $state = $existingPRs[$issue.IssueNumber].State
            " [has $state PR #$($existingPRs[$issue.IssueNumber].PRNumber)]" 
        } else { "" }
        Info ("#{0,-6} [F:{1}, C:{2}, E:{3}d]{4}" -f $issue.IssueNumber, $issue.FeasibilityScore, $issue.ClarityScore, $issue.EffortDays, $prInfo)
    }
    Info ("-" * 80)

    if ($DryRun) {
        Warn "`nDry run mode - showing what would be done:"
        Info "  1. Create worktrees for $($issuesToProcess.Count) issues (parallel)"
        Info "  2. Run Copilot auto-fix in each worktree (parallel)"
        Info "  3. Commit and create PRs (parallel)"
        Info "  4. Run PR review workflow (parallel)"
        return
    }

    # Confirm
    if (-not $Force) {
        $confirm = Read-Host "`nProceed with full cycle for $($issuesToProcess.Count) issues? (y/N)"
        if ($confirm -notmatch '^[yY]') {
            Info "Cancelled."
            return
        }
    }

    # Track results
    $results = @{
        FixSucceeded = [System.Collections.Concurrent.ConcurrentBag[object]]::new()
        FixFailed = [System.Collections.Concurrent.ConcurrentBag[object]]::new()
        PRCreated = [System.Collections.Concurrent.ConcurrentBag[object]]::new()
        PRFailed = [System.Collections.Concurrent.ConcurrentBag[object]]::new()
        PRSkipped = [System.Collections.Concurrent.ConcurrentBag[object]]::new()
        ReviewSucceeded = [System.Collections.Concurrent.ConcurrentBag[object]]::new()
        ReviewFailed = [System.Collections.Concurrent.ConcurrentBag[object]]::new()
    }

    # ========================================
    # PHASE 1: Create worktrees and fix issues (PARALLEL)
    # ========================================
    Info "`n" + ("=" * 60)
    Info "PHASE 1: Auto-Fix Issues (Parallel)"
    Info ("=" * 60)
    
    $issuesNeedingFix = $issuesToProcess | Where-Object { -not $existingPRs.ContainsKey($_.IssueNumber) }
    $issuesWithPR = $issuesToProcess | Where-Object { $existingPRs.ContainsKey($_.IssueNumber) }
    
    Info "Issues needing fix: $($issuesNeedingFix.Count)"
    Info "Issues with existing PR (skip to review): $($issuesWithPR.Count)"
    
    if ($issuesNeedingFix.Count -gt 0) {
        $issuesNeedingFix | ForEach-Object -ThrottleLimit $FixThrottleLimit -Parallel {
            $issue = $_
            $issueNum = $issue.IssueNumber
            $issueFixScript = $using:issueFixScript
            $CLIType = $using:CLIType
            $results = $using:results
            
            try {
                Write-Host "[Issue #$issueNum] Starting auto-fix..." -ForegroundColor Cyan
                & $issueFixScript -IssueNumber $issueNum -CLIType $CLIType -Force 2>&1 | Out-Null
                $results.FixSucceeded.Add($issueNum)
                Write-Host "[Issue #$issueNum] ✓ Fix completed" -ForegroundColor Green
            }
            catch {
                $results.FixFailed.Add(@{ IssueNumber = $issueNum; Error = $_.Exception.Message })
                Write-Host "[Issue #$issueNum] ✗ Fix failed: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }
    
    Info "`nPhase 1 complete: $($results.FixSucceeded.Count) succeeded, $($results.FixFailed.Count) failed"

    # ========================================
    # PHASE 2: Commit and create PRs (PARALLEL)
    # ========================================
    Info "`n" + ("=" * 60)
    Info "PHASE 2: Submit PRs (Parallel)"
    Info ("=" * 60)
    
    $fixedIssues = $results.FixSucceeded.ToArray()
    
    if ($fixedIssues.Count -gt 0) {
        $fixedIssues | ForEach-Object -ThrottleLimit $PRThrottleLimit -Parallel {
            $issueNum = $_
            $submitPRScript = $using:submitPRScript
            $CLIType = $using:CLIType
            $results = $using:results
            
            try {
                Write-Host "[Issue #$issueNum] Creating PR..." -ForegroundColor Cyan
                $submitResult = & $submitPRScript -IssueNumbers $issueNum -CLIType $CLIType -Force 2>&1
                
                # Parse output to find PR URL
                $prUrl = $null
                $prNum = 0
                
                if ($submitResult -match 'https://github.com/[^/]+/[^/]+/pull/(\d+)') {
                    $prUrl = $Matches[0]
                    $prNum = [int]$Matches[1]
                }
                
                if ($prNum -gt 0) {
                    $results.PRCreated.Add(@{ IssueNumber = $issueNum; PRNumber = $prNum; PRUrl = $prUrl })
                    Write-Host "[Issue #$issueNum] ✓ PR #$prNum created" -ForegroundColor Green
                } else {
                    # Check if PR was already created
                    $existingPr = gh pr list --head "issue/$issueNum" --state open --json number,url 2>$null | ConvertFrom-Json
                    if ($existingPr -and $existingPr.Count -gt 0) {
                        $results.PRSkipped.Add(@{ IssueNumber = $issueNum; PRNumber = $existingPr[0].number; PRUrl = $existingPr[0].url; Reason = "Already exists" })
                        Write-Host "[Issue #$issueNum] PR already exists: #$($existingPr[0].number)" -ForegroundColor Yellow
                    } else {
                        $results.PRFailed.Add(@{ IssueNumber = $issueNum; Error = "No PR created" })
                        Write-Host "[Issue #$issueNum] ✗ PR creation failed" -ForegroundColor Red
                    }
                }
            }
            catch {
                $results.PRFailed.Add(@{ IssueNumber = $issueNum; Error = $_.Exception.Message })
                Write-Host "[Issue #$issueNum] ✗ PR failed: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }
    
    Info "`nPhase 2 complete: $($results.PRCreated.Count) created, $($results.PRSkipped.Count) skipped, $($results.PRFailed.Count) failed"

    # ========================================
    # PHASE 3: Review PRs (PARALLEL)
    # ========================================
    Info "`n" + ("=" * 60)
    Info "PHASE 3: Review PRs (Parallel)"
    Info ("=" * 60)
    
    # Collect all PRs to review (newly created + existing)
    $prsToReview = @()
    
    foreach ($pr in $results.PRCreated.ToArray()) {
        $prsToReview += @{ IssueNumber = $pr.IssueNumber; PRNumber = $pr.PRNumber }
    }
    foreach ($pr in $results.PRSkipped.ToArray()) {
        $prsToReview += @{ IssueNumber = $pr.IssueNumber; PRNumber = $pr.PRNumber }
    }
    foreach ($issue in $issuesWithPR) {
        $prInfo = $existingPRs[$issue.IssueNumber]
        $prsToReview += @{ IssueNumber = $issue.IssueNumber; PRNumber = $prInfo.PRNumber }
    }
    
    Info "PRs to review: $($prsToReview.Count)"
    
    if ($prsToReview.Count -gt 0) {
        $prsToReview | ForEach-Object -ThrottleLimit $ReviewThrottleLimit -Parallel {
            $pr = $_
            $issueNum = $pr.IssueNumber
            $prNum = $pr.PRNumber
            $prReviewScript = $using:prReviewScript
            $CLIType = $using:CLIType
            $results = $using:results
            
            try {
                Write-Host "[PR #$prNum] Starting review workflow..." -ForegroundColor Cyan
                & $prReviewScript -PRNumbers $prNum -CLIType $CLIType -Force 2>&1 | Out-Null
                $results.ReviewSucceeded.Add(@{ IssueNumber = $issueNum; PRNumber = $prNum })
                Write-Host "[PR #$prNum] ✓ Review completed" -ForegroundColor Green
            }
            catch {
                $results.ReviewFailed.Add(@{ IssueNumber = $issueNum; PRNumber = $prNum; Error = $_.Exception.Message })
                Write-Host "[PR #$prNum] ✗ Review failed: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }
    
    Info "`nPhase 3 complete: $($results.ReviewSucceeded.Count) succeeded, $($results.ReviewFailed.Count) failed"

    # Final Summary
    $duration = (Get-Date) - $startTime
    
    Info "`n" + ("=" * 80)
    Info "FULL CYCLE COMPLETE"
    Info ("=" * 80)
    Info "Duration: $($duration.ToString('hh\:mm\:ss'))"
    Info ""
    Info "Issues processed:    $($issuesToProcess.Count)"
    Success "Fixes succeeded:     $($results.FixSucceeded.Count)"
    if ($results.FixFailed.Count -gt 0) {
        Err "Fixes failed:        $($results.FixFailed.Count)"
    }
    Success "PRs created:         $($results.PRCreated.Count)"
    if ($results.PRSkipped.Count -gt 0) {
        Warn "PRs skipped:         $($results.PRSkipped.Count) (already existed)"
    }
    if ($results.PRFailed.Count -gt 0) {
        Err "PRs failed:          $($results.PRFailed.Count)"
    }
    Success "Reviews completed:   $($results.ReviewSucceeded.Count)"
    if ($results.ReviewFailed.Count -gt 0) {
        Err "Reviews failed:      $($results.ReviewFailed.Count)"
    }
    
    Info ""
    Info "Summary by issue:"
    foreach ($issue in $issuesToProcess) {
        $issueNum = $issue.IssueNumber
        $prInfo = $results.PRCreated.ToArray() | Where-Object { $_.IssueNumber -eq $issueNum } | Select-Object -First 1
        if (-not $prInfo) {
            $prInfo = $results.PRSkipped.ToArray() | Where-Object { $_.IssueNumber -eq $issueNum } | Select-Object -First 1
        }
        if (-not $prInfo -and $existingPRs.ContainsKey($issueNum)) {
            $prInfo = @{ PRNumber = $existingPRs[$issueNum].PRNumber }
        }
        
        $prNum = if ($prInfo) { "PR #$($prInfo.PRNumber)" } else { "No PR" }
        $fixStatus = if ($results.FixSucceeded.ToArray() -contains $issueNum) { "✓" } elseif ($results.FixFailed.ToArray().IssueNumber -contains $issueNum) { "✗" } else { "-" }
        $reviewStatus = if ($results.ReviewSucceeded.ToArray().IssueNumber -contains $issueNum -or $results.ReviewSucceeded.ToArray().PRNumber -contains $prInfo.PRNumber) { "✓" } else { "-" }
        
        Info ("  Issue #{0,-6} [{1}Fix] [{2}Review] -> {3}" -f $issueNum, $fixStatus, $reviewStatus, $prNum)
    }
    
    Info ("=" * 80)

    return @{
        FixSucceeded = $results.FixSucceeded.ToArray()
        FixFailed = $results.FixFailed.ToArray()
        PRCreated = $results.PRCreated.ToArray()
        PRSkipped = $results.PRSkipped.ToArray()
        PRFailed = $results.PRFailed.ToArray()
        ReviewSucceeded = $results.ReviewSucceeded.ToArray()
        ReviewFailed = $results.ReviewFailed.ToArray()
    }
}
catch {
    Err "Error: $($_.Exception.Message)"
    exit 1
}
#endregion
