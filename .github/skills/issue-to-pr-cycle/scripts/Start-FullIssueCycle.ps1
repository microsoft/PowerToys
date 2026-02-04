<#!
.SYNOPSIS
    Run the complete issue-to-PR cycle: fix issues, create PRs, review, and fix comments.

.DESCRIPTION
    Orchestrates the full workflow:
    1. Find high-confidence issues matching criteria
    2. Create worktrees and run auto-fix for each issue
    3. Commit changes and create PRs
    4. Run PR review workflow in a loop until no issues remain:
       a. Review PR and post comments
       b. Fix PR comments
       c. Re-review to check for remaining issues
       d. Repeat until clean or max iterations reached

.PARAMETER MinFeasibilityScore
    Minimum Technical Feasibility score. Default: 70.

.PARAMETER MinClarityScore
    Minimum Requirement Clarity score. Default: 70.

.PARAMETER MaxEffortDays
    Maximum effort in days. Default: 10.

.PARAMETER MaxReviewIterations
    Maximum review/fix iterations per PR before giving up. Default: 3.

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
    [int]$MaxReviewIterations = 3,
    [int[]]$ExcludeIssues = @(),
    [ValidateSet('copilot', 'claude')]
    [string]$CLIType = 'copilot',
    [int]$FixThrottleLimit = 5,
    [int]$PRThrottleLimit = 5,
    [int]$ReviewThrottleLimit = 3,
    [ValidateSet('high', 'medium', 'low', 'info')]
    [string]$MinSeverityForLoop = 'medium',
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
$submitPRScript = Join-Path $skillsDir 'issue-fix/scripts/Submit-IssueFix.ps1'
$prReviewScript = Join-Path $skillsDir 'pr-review/scripts/Start-PRReviewWorkflow.ps1'
$prFixScript = Join-Path $skillsDir 'pr-fix/scripts/Start-PRFix.ps1'

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

function Get-PRReviewIssueCount {
    <#
    .SYNOPSIS
        Count high/medium severity issues from the review overview file.
    #>
    param(
        [int]$PRNumber,
        [string]$MinSeverity = 'medium'
    )
    
    $overviewPath = Join-Path $repoRoot "Generated Files/prReview/$PRNumber/00-OVERVIEW.md"
    
    if (-not (Test-Path $overviewPath)) {
        return -1  # No review yet
    }
    
    $content = Get-Content $overviewPath -Raw
    
    # Parse "High severity issues: <count>" from the overview
    $highCount = 0
    $mediumCount = 0
    
    if ($content -match 'High severity issues:\s*(\d+)') {
        $highCount = [int]$Matches[1]
    }
    
    # Also check step files for medium severity
    $stepFiles = Get-ChildItem -Path (Split-Path $overviewPath) -Filter "*.md" | Where-Object { $_.Name -match '^\d{2}-' }
    foreach ($stepFile in $stepFiles) {
        $stepContent = Get-Content $stepFile.FullName -Raw
        # Count severity markers
        $mediumCount += ([regex]::Matches($stepContent, '\*\*Severity:\s*medium\*\*', 'IgnoreCase')).Count
        $mediumCount += ([regex]::Matches($stepContent, '🟡\s*Medium', 'IgnoreCase')).Count
    }
    
    switch ($MinSeverity) {
        'high' { return $highCount }
        'medium' { return $highCount + $mediumCount }
        default { return $highCount + $mediumCount }
    }
}

function Get-PRActiveCommentCount {
    <#
    .SYNOPSIS
        Count active (unresolved) review comments on a PR.
    #>
    param(
        [int]$PRNumber
    )
    
    try {
        # Get all review comments
        $comments = gh api "repos/microsoft/PowerToys/pulls/$PRNumber/comments" --jq '[.[] | select(.in_reply_to_id == null)] | length' 2>$null
        if ($comments) {
            return [int]$comments
        }
        return 0
    }
    catch {
        return 0
    }
}

function Clear-PRReviewCache {
    <#
    .SYNOPSIS
        Clear the review cache to force a fresh review.
    #>
    param(
        [int]$PRNumber
    )
    
    $reviewPath = Join-Path $repoRoot "Generated Files/prReview/$PRNumber"
    if (Test-Path $reviewPath) {
        # Keep logs but remove review files
        Get-ChildItem $reviewPath -Filter "*.md" | Remove-Item -Force
    }
}

function Invoke-PRReviewFixLoop {
    <#
    .SYNOPSIS
        Run the review/fix loop until no issues remain or max iterations reached.
    #>
    param(
        [int]$PRNumber,
        [int]$IssueNumber,
        [string]$WorktreePath,
        [string]$CLIType = 'copilot',
        [string]$MinSeverity = 'medium',
        [int]$MaxIterations = 3
    )
    
    $iteration = 0
    $issuesRemaining = $true
    
    while ($issuesRemaining -and $iteration -lt $MaxIterations) {
        $iteration++
        Info "    [PR #$PRNumber] Review/Fix iteration $iteration of $MaxIterations"
        
        # Step 1: Run PR review (assign Copilot, review, post comments)
        Info "    [PR #$PRNumber] Running review..."
        try {
            # Clear previous review to force fresh analysis
            if ($iteration -gt 1) {
                Clear-PRReviewCache -PRNumber $PRNumber
            }
            
            & $prReviewScript -PRNumbers $PRNumber -CLIType $CLIType -SkipFix -Force 2>&1 | Out-Null
        }
        catch {
            Warn "    [PR #$PRNumber] Review failed: $($_.Exception.Message)"
            break
        }
        
        # Step 2: Check if there are issues found
        $issueCount = Get-PRReviewIssueCount -PRNumber $PRNumber -MinSeverity $MinSeverity
        $activeComments = Get-PRActiveCommentCount -PRNumber $PRNumber
        
        Info "    [PR #$PRNumber] Found $issueCount issues (severity >= $MinSeverity), $activeComments active comments"
        
        if ($issueCount -le 0 -and $activeComments -le 0) {
            Info "    [PR #$PRNumber] ✓ No issues remaining!"
            $issuesRemaining = $false
            break
        }
        
        # Step 3: Run fix for active comments
        if ($activeComments -gt 0 -or $issueCount -gt 0) {
            Info "    [PR #$PRNumber] Fixing $activeComments active comments..."
            try {
                # Run fix-pr-active-comments
                & $prReviewScript -PRNumbers $PRNumber -CLIType $CLIType -SkipAssign -SkipReview -Force 2>&1 | Out-Null
            }
            catch {
                Warn "    [PR #$PRNumber] Fix failed: $($_.Exception.Message)"
            }
        }
        
        # Brief pause to let GitHub sync
        Start-Sleep -Seconds 2
    }
    
    if ($issuesRemaining) {
        Warn "    [PR #$PRNumber] Max iterations reached, some issues may remain"
    }
    
    return @{
        PRNumber = $PRNumber
        IssueNumber = $IssueNumber
        Iterations = $iteration
        IssuesRemaining = $issuesRemaining
        FinalIssueCount = (Get-PRReviewIssueCount -PRNumber $PRNumber -MinSeverity $MinSeverity)
    }
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
        Info "  4. Run PR review/fix loop (up to $MaxReviewIterations iterations per PR)"
        Info "     - Review PR and post comments (severity >= $MinSeverityForLoop)"
        Info "     - Fix active comments"
        Info "     - Repeat until clean or max iterations"
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
    # PHASE 3: Review and Fix PRs (ITERATIVE LOOP)
    # ========================================
    Info "`n" + ("=" * 60)
    Info "PHASE 3: Review & Fix PRs (Iterative Loop)"
    Info ("=" * 60)
    Info "Max iterations per PR: $MaxReviewIterations"
    Info "Min severity to fix: $MinSeverityForLoop"
    
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
        # Only include open PRs
        if ($prInfo.State -eq 'OPEN') {
            $prsToReview += @{ IssueNumber = $issue.IssueNumber; PRNumber = $prInfo.PRNumber }
        }
    }
    
    Info "PRs to review: $($prsToReview.Count)"
    
    # Track review loop results
    $reviewLoopResults = [System.Collections.Concurrent.ConcurrentBag[object]]::new()
    
    if ($prsToReview.Count -gt 0) {
        # Process sequentially to avoid overwhelming the AI CLI
        foreach ($pr in $prsToReview) {
            $issueNum = $pr.IssueNumber
            $prNum = $pr.PRNumber
            
            Info "`n  [PR #$prNum for Issue #$issueNum] Starting review/fix loop..."
            
            try {
                $loopResult = Invoke-PRReviewFixLoop `
                    -PRNumber $prNum `
                    -IssueNumber $issueNum `
                    -CLIType $CLIType `
                    -MinSeverity $MinSeverityForLoop `
                    -MaxIterations $MaxReviewIterations
                
                $reviewLoopResults.Add($loopResult)
                
                if (-not $loopResult.IssuesRemaining) {
                    $results.ReviewSucceeded.Add(@{ IssueNumber = $issueNum; PRNumber = $prNum; Iterations = $loopResult.Iterations })
                    Success "  [PR #$prNum] ✓ Clean after $($loopResult.Iterations) iteration(s)"
                } else {
                    $results.ReviewFailed.Add(@{ IssueNumber = $issueNum; PRNumber = $prNum; Iterations = $loopResult.Iterations; RemainingIssues = $loopResult.FinalIssueCount })
                    Warn "  [PR #$prNum] ⚠ $($loopResult.FinalIssueCount) issues remain after $($loopResult.Iterations) iterations"
                }
            }
            catch {
                $results.ReviewFailed.Add(@{ IssueNumber = $issueNum; PRNumber = $prNum; Error = $_.Exception.Message })
                Err "  [PR #$prNum] ✗ Review loop failed: $($_.Exception.Message)"
            }
        }
    }
    
    Info "`nPhase 3 complete: $($results.ReviewSucceeded.Count) clean, $($results.ReviewFailed.Count) with remaining issues"

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
    Success "PRs clean (no issues): $($results.ReviewSucceeded.Count)"
    if ($results.ReviewFailed.Count -gt 0) {
        Warn "PRs with remaining issues: $($results.ReviewFailed.Count)"
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
        
        # Check review status with iteration count
        $reviewResult = $results.ReviewSucceeded.ToArray() | Where-Object { $_.IssueNumber -eq $issueNum -or $_.PRNumber -eq $prInfo.PRNumber } | Select-Object -First 1
        $reviewFailResult = $results.ReviewFailed.ToArray() | Where-Object { $_.IssueNumber -eq $issueNum -or $_.PRNumber -eq $prInfo.PRNumber } | Select-Object -First 1
        
        if ($reviewResult) {
            $reviewStatus = "✓($($reviewResult.Iterations))"
        } elseif ($reviewFailResult) {
            $reviewStatus = "⚠($($reviewFailResult.RemainingIssues) left)"
        } else {
            $reviewStatus = "-"
        }
        
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
