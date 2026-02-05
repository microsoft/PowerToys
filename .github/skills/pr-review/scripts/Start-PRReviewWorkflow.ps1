<#!
.SYNOPSIS
    Review and fix PRs in parallel using GitHub Copilot and MCP.

.DESCRIPTION
    For each PR (from worktrees or specified), runs in parallel:
    1. Assigns GitHub Copilot as reviewer via GitHub MCP
    2. Runs review-pr.prompt.md to generate review and post comments
    3. Runs fix-pr-active-comments.prompt.md to fix issues

.PARAMETER PRNumbers
    Array of PR numbers to process. If not specified, finds PRs from issue worktrees.

.PARAMETER SkipAssign
    Skip assigning Copilot as reviewer.

.PARAMETER SkipReview
    Skip the review step.

.PARAMETER SkipFix
    Skip the fix step.

.PARAMETER MinSeverity
    Minimum severity to post as PR comments: high, medium, low, info. Default: medium.

.PARAMETER MaxParallel
    Maximum parallel jobs. Default: 3.

.PARAMETER DryRun
    Show what would be done without executing.

.PARAMETER CLIType
    AI CLI to use: copilot or claude. Default: copilot.

.EXAMPLE
    # Process all PRs from issue worktrees
    ./Start-PRReviewWorkflow.ps1

.EXAMPLE
    # Process specific PRs
    ./Start-PRReviewWorkflow.ps1 -PRNumbers 45234, 45235

.EXAMPLE
    # Only review, don't fix
    ./Start-PRReviewWorkflow.ps1 -SkipFix

.EXAMPLE
    # Dry run
    ./Start-PRReviewWorkflow.ps1 -DryRun

.NOTES
    Prerequisites:
    - GitHub CLI (gh) authenticated
    - Copilot CLI installed
    - GitHub MCP configured for posting comments
#>

[CmdletBinding()]
param(
    [int[]]$PRNumbers,
    
    [switch]$SkipAssign,
    
    [switch]$SkipReview,
    
    [switch]$SkipFix,
    
    [ValidateSet('high', 'medium', 'low', 'info')]
    [string]$MinSeverity = 'medium',
    
    [int]$MaxParallel = 3,
    
    [switch]$DryRun,
    
    [ValidateSet('copilot', 'claude')]
    [string]$CLIType = 'copilot',
    
    [switch]$Force,
    
    [switch]$Help
)

# Load libraries
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. "$scriptDir/IssueReviewLib.ps1"

# Load worktree library
$repoRoot = Get-RepoRoot
$worktreeLib = Join-Path $repoRoot 'tools/build/WorktreeLib.ps1'
if (Test-Path $worktreeLib) {
    . $worktreeLib
}

if ($Help) {
    Get-Help $MyInvocation.MyCommand.Path -Full
    return
}

function Get-PRsFromWorktrees {
    <#
    .SYNOPSIS
        Get PR numbers from issue worktrees by checking for open PRs on each branch.
    #>
    $worktrees = Get-WorktreeEntries | Where-Object { $_.Branch -like 'issue/*' }
    $prs = @()
    
    foreach ($wt in $worktrees) {
        $prInfo = gh pr list --head $wt.Branch --json number,url --state open 2>$null | ConvertFrom-Json
        if ($prInfo -and $prInfo.Count -gt 0) {
            $prs += @{
                PRNumber = $prInfo[0].number
                PRUrl = $prInfo[0].url
                Branch = $wt.Branch
                WorktreePath = $wt.Path
            }
        }
    }
    
    return $prs
}

function Invoke-AssignCopilotReviewer {
    <#
    .SYNOPSIS
        Assign GitHub Copilot as a reviewer to the PR using GitHub MCP.
    #>
    param(
        [Parameter(Mandatory)]
        [int]$PRNumber,
        [string]$CLIType = 'copilot',
        [switch]$DryRun
    )

    if ($DryRun) {
        Info "  [DRY RUN] Would request Copilot review for PR #$PRNumber"
        return $true
    }

    # Use a prompt that instructs Copilot to use GitHub MCP to assign Copilot as reviewer
    $prompt = @"
Use the GitHub MCP to request a review from GitHub Copilot for PR #$PRNumber.

Steps:
1. Use the GitHub MCP tool to add "Copilot" as a reviewer to pull request #$PRNumber in the microsoft/PowerToys repository
2. This should add Copilot to the "Reviewers" section of the PR

If GitHub MCP is not available, report that and skip this step.
"@

    # MCP config for github-artifacts tools (relative to repo root)
    $mcpConfig = '@.github/skills/pr-review/references/mcp-config.json'
    
    try {
        Info "  Requesting Copilot review via GitHub MCP..."
        
        switch ($CLIType) {
            'copilot' {
                & copilot --additional-mcp-config $mcpConfig -p $prompt --yolo -s 2>&1 | Out-Null
            }
            'claude' {
                & claude --print --dangerously-skip-permissions --prompt $prompt 2>&1 | Out-Null
            }
        }
        
        return $true
    }
    catch {
        Warn "  Could not assign Copilot reviewer: $($_.Exception.Message)"
        return $false
    }
}

function Invoke-PRReview {
    <#
    .SYNOPSIS
        Run review-pr.prompt.md using Copilot CLI.
    #>
    param(
        [Parameter(Mandatory)]
        [int]$PRNumber,
        [string]$CLIType = 'copilot',
        [string]$MinSeverity = 'medium',
        [switch]$DryRun
    )

    # Simple prompt - let the prompt file define all the details
    $prompt = @"
Follow exactly what at .github/prompts/review-pr.prompt.md to do with PR #$PRNumber.
Post findings with severity >= $MinSeverity as PR review comments via GitHub MCP.
"@

    if ($DryRun) {
        Info "  [DRY RUN] Would run PR review for #$PRNumber"
        return @{ Success = $true; ReviewPath = "Generated Files/prReview/$PRNumber" }
    }

    $reviewPath = Join-Path $repoRoot "Generated Files/prReview/$PRNumber"
    
    # Ensure the review directory exists
    if (-not (Test-Path $reviewPath)) {
        New-Item -ItemType Directory -Path $reviewPath -Force | Out-Null
    }
    
    # MCP config for github-artifacts tools (relative to repo root)
    $mcpConfig = '@.github/skills/pr-review/references/mcp-config.json'
    
    Push-Location $repoRoot
    try {
        switch ($CLIType) {
            'copilot' {
                Info "  Running Copilot review (this may take several minutes)..."
                $output = & copilot --additional-mcp-config $mcpConfig -p $prompt --yolo 2>&1
                # Log output for debugging
                $logFile = Join-Path $reviewPath "_copilot-review.log"
                $output | Out-File -FilePath $logFile -Force
            }
            'claude' {
                Info "  Running Claude review (this may take several minutes)..."
                $output = & claude --print --dangerously-skip-permissions --prompt $prompt 2>&1
                $logFile = Join-Path $reviewPath "_claude-review.log"
                $output | Out-File -FilePath $logFile -Force
            }
        }

        # Check if review files were created (at minimum, check for multiple step files)
        $overviewPath = Join-Path $reviewPath '00-OVERVIEW.md'
        $stepFiles = Get-ChildItem -Path $reviewPath -Filter "*.md" -ErrorAction SilentlyContinue
        $stepCount = ($stepFiles | Where-Object { $_.Name -match '^\d{2}-' }).Count
        
        if ($stepCount -ge 5) {
            return @{ Success = $true; ReviewPath = $reviewPath; StepFilesCreated = $stepCount }
        } elseif (Test-Path $overviewPath) {
            Warn "  Only overview created, step files may be incomplete ($stepCount step files)"
            return @{ Success = $true; ReviewPath = $reviewPath; StepFilesCreated = $stepCount; Partial = $true }
        } else {
            return @{ Success = $false; Error = "Review files not created (found $stepCount step files)" }
        }
    }
    catch {
        return @{ Success = $false; Error = $_.Exception.Message }
    }
    finally {
        Pop-Location
    }
}

function Invoke-FixPRComments {
    <#
    .SYNOPSIS
        Run fix-pr-active-comments.prompt.md to fix issues.
    #>
    param(
        [Parameter(Mandatory)]
        [int]$PRNumber,
        [string]$WorktreePath,
        [string]$CLIType = 'copilot',
        [switch]$DryRun
    )

    # Simple prompt - let the prompt file define all the details
    $prompt = "Follow .github/prompts/fix-pr-active-comments.prompt.md for PR #$PRNumber."

    if ($DryRun) {
        Info "  [DRY RUN] Would fix PR comments for #$PRNumber"
        return @{ Success = $true }
    }

    $workDir = if ($WorktreePath -and (Test-Path $WorktreePath)) { $WorktreePath } else { $repoRoot }

    # MCP config for github-artifacts tools (relative to repo root)
    $mcpConfig = '@.github/skills/pr-review/references/mcp-config.json'
    
    Push-Location $workDir
    try {
        switch ($CLIType) {
            'copilot' {
                Info "  Running Copilot to fix comments..."
                $output = & copilot --additional-mcp-config $mcpConfig -p $prompt --yolo 2>&1
                # Log output for debugging
                $logPath = Join-Path $repoRoot "Generated Files/prReview/$PRNumber"
                if (-not (Test-Path $logPath)) {
                    New-Item -ItemType Directory -Path $logPath -Force | Out-Null
                }
                $logFile = Join-Path $logPath "_copilot-fix.log"
                $output | Out-File -FilePath $logFile -Force
            }
            'claude' {
                Info "  Running Claude to fix comments..."
                $output = & claude --print --dangerously-skip-permissions --prompt $prompt 2>&1
                $logPath = Join-Path $repoRoot "Generated Files/prReview/$PRNumber"
                if (-not (Test-Path $logPath)) {
                    New-Item -ItemType Directory -Path $logPath -Force | Out-Null
                }
                $logFile = Join-Path $logPath "_claude-fix.log"
                $output | Out-File -FilePath $logFile -Force
            }
        }

        return @{ Success = $true }
    }
    catch {
        return @{ Success = $false; Error = $_.Exception.Message }
    }
    finally {
        Pop-Location
    }
}

function Start-PRWorkflowJob {
    <#
    .SYNOPSIS
        Process a single PR through the workflow.
    #>
    param(
        [Parameter(Mandatory)]
        [int]$PRNumber,
        [string]$WorktreePath,
        [string]$CLIType = 'copilot',
        [string]$MinSeverity = 'medium',
        [switch]$SkipAssign,
        [switch]$SkipReview,
        [switch]$SkipFix,
        [switch]$DryRun
    )

    $result = @{
        PRNumber = $PRNumber
        AssignResult = $null
        ReviewResult = $null
        FixResult = $null
        Success = $true
    }

    # Step 1: Assign Copilot as reviewer
    if (-not $SkipAssign) {
        Info "  Step 1: Assigning Copilot reviewer..."
        $result.AssignResult = Invoke-AssignCopilotReviewer -PRNumber $PRNumber -CLIType $CLIType -DryRun:$DryRun
        if (-not $result.AssignResult) {
            Warn "  Assignment step had issues (continuing...)"
        }
    } else {
        Info "  Step 1: Skipped (assign)"
    }

    # Step 2: Run PR review
    if (-not $SkipReview) {
        Info "  Step 2: Running PR review..."
        $result.ReviewResult = Invoke-PRReview -PRNumber $PRNumber -CLIType $CLIType -MinSeverity $MinSeverity -DryRun:$DryRun
        if (-not $result.ReviewResult.Success) {
            Warn "  Review step failed: $($result.ReviewResult.Error)"
            $result.Success = $false
        } else {
            $stepInfo = if ($result.ReviewResult.StepFilesCreated) { " ($($result.ReviewResult.StepFilesCreated) step files)" } else { "" }
            $partialInfo = if ($result.ReviewResult.Partial) { " [PARTIAL]" } else { "" }
            Success "  Review completed: $($result.ReviewResult.ReviewPath)$stepInfo$partialInfo"
        }
    } else {
        Info "  Step 2: Skipped (review)"
    }

    # Step 3: Fix PR comments
    if (-not $SkipFix) {
        Info "  Step 3: Fixing PR comments..."
        $result.FixResult = Invoke-FixPRComments -PRNumber $PRNumber -WorktreePath $WorktreePath -CLIType $CLIType -DryRun:$DryRun
        if (-not $result.FixResult.Success) {
            Warn "  Fix step failed: $($result.FixResult.Error)"
            $result.Success = $false
        } else {
            Success "  Fix step completed"
        }
    } else {
        Info "  Step 3: Skipped (fix)"
    }

    return $result
}

#region Main Script
try {
    Info "Repository root: $repoRoot"
    Info "CLI type: $CLIType"
    Info "Min severity for comments: $MinSeverity"
    Info "Max parallel: $MaxParallel"

    # Determine PRs to process
    $prsToProcess = @()

    if ($PRNumbers -and $PRNumbers.Count -gt 0) {
        # Use specified PR numbers
        foreach ($prNum in $PRNumbers) {
            $prInfo = gh pr view $prNum --json number,url,headRefName 2>$null | ConvertFrom-Json
            if ($prInfo) {
                # Try to find matching worktree
                $wt = Get-WorktreeEntries | Where-Object { $_.Branch -eq $prInfo.headRefName } | Select-Object -First 1
                $prsToProcess += @{
                    PRNumber = $prInfo.number
                    PRUrl = $prInfo.url
                    Branch = $prInfo.headRefName
                    WorktreePath = if ($wt) { $wt.Path } else { $repoRoot }
                }
            } else {
                Warn "PR #$prNum not found"
            }
        }
    } else {
        # Get PRs from worktrees
        Info "`nFinding PRs from issue worktrees..."
        $prsToProcess = Get-PRsFromWorktrees
    }

    if ($prsToProcess.Count -eq 0) {
        Warn "No PRs found to process."
        return
    }

    # Display PRs
    Info "`nPRs to process:"
    Info ("-" * 80)
    foreach ($pr in $prsToProcess) {
        Info ("  #{0,-6} {1}" -f $pr.PRNumber, $pr.PRUrl)
    }
    Info ("-" * 80)

    if ($DryRun) {
        Warn "`nDry run mode - no changes will be made."
    }

    # Confirm
    if (-not $Force -and -not $DryRun) {
        $stepsDesc = @()
        if (-not $SkipAssign) { $stepsDesc += "assign Copilot" }
        if (-not $SkipReview) { $stepsDesc += "review" }
        if (-not $SkipFix) { $stepsDesc += "fix comments" }
        
        $confirm = Read-Host "`nProceed with $($prsToProcess.Count) PRs ($($stepsDesc -join ', '))? (y/N)"
        if ($confirm -notmatch '^[yY]') {
            Info "Cancelled."
            return
        }
    }

    # Process PRs (using jobs for parallelization)
    $results = @{
        Success = @()
        Failed = @()
    }

    if ($MaxParallel -gt 1 -and $prsToProcess.Count -gt 1) {
        # Parallel processing using PowerShell jobs
        Info "`nStarting parallel processing (max $MaxParallel concurrent)..."
        
        $jobs = @()
        $prQueue = [System.Collections.Queue]::new($prsToProcess)

        while ($prQueue.Count -gt 0 -or $jobs.Count -gt 0) {
            # Start new jobs up to MaxParallel
            while ($jobs.Count -lt $MaxParallel -and $prQueue.Count -gt 0) {
                $pr = $prQueue.Dequeue()
                
                Info "`n" + ("=" * 60)
                Info "PROCESSING PR #$($pr.PRNumber)"
                Info ("=" * 60)

                # For simplicity, process sequentially within each PR but PRs in parallel
                # Since copilot CLI might have issues with true parallel execution
                $jobResult = Start-PRWorkflowJob `
                    -PRNumber $pr.PRNumber `
                    -WorktreePath $pr.WorktreePath `
                    -CLIType $CLIType `
                    -MinSeverity $MinSeverity `
                    -SkipAssign:$SkipAssign `
                    -SkipReview:$SkipReview `
                    -SkipFix:$SkipFix `
                    -DryRun:$DryRun

                if ($jobResult.Success) {
                    $results.Success += $jobResult
                    Success "✓ PR #$($pr.PRNumber) workflow completed"
                } else {
                    $results.Failed += $jobResult
                    Err "✗ PR #$($pr.PRNumber) workflow had failures"
                }
            }
        }
    } else {
        # Sequential processing
        foreach ($pr in $prsToProcess) {
            Info "`n" + ("=" * 60)
            Info "PROCESSING PR #$($pr.PRNumber)"
            Info ("=" * 60)

            $jobResult = Start-PRWorkflowJob `
                -PRNumber $pr.PRNumber `
                -WorktreePath $pr.WorktreePath `
                -CLIType $CLIType `
                -MinSeverity $MinSeverity `
                -SkipAssign:$SkipAssign `
                -SkipReview:$SkipReview `
                -SkipFix:$SkipFix `
                -DryRun:$DryRun

            if ($jobResult.Success) {
                $results.Success += $jobResult
                Success "✓ PR #$($pr.PRNumber) workflow completed"
            } else {
                $results.Failed += $jobResult
                Err "✗ PR #$($pr.PRNumber) workflow had failures"
            }
        }
    }

    # Summary
    Info "`n" + ("=" * 80)
    Info "PR REVIEW WORKFLOW COMPLETE"
    Info ("=" * 80)
    Info "Total PRs:       $($prsToProcess.Count)"
    
    if ($results.Success.Count -gt 0) {
        Success "Succeeded:       $($results.Success.Count)"
        foreach ($r in $results.Success) {
            Success "  PR #$($r.PRNumber)"
        }
    }
    
    if ($results.Failed.Count -gt 0) {
        Err "Had issues:      $($results.Failed.Count)"
        foreach ($r in $results.Failed) {
            Err "  PR #$($r.PRNumber)"
        }
    }

    Info "`nReview files location: Generated Files/prReview/<PR_NUMBER>/"
    Info ("=" * 80)

    return $results
}
catch {
    Err "Error: $($_.Exception.Message)"
    exit 1
}
#endregion
