<#!
.SYNOPSIS
    Auto-fix high-confidence issues using worktrees and AI CLI.

.DESCRIPTION
    Finds issues with high confidence scores from the review results, creates worktrees
    for each, copies the Generated Files, and kicks off the FixIssue agent to implement fixes.

.PARAMETER IssueNumber
    Specific issue number to fix. If not specified, finds high-confidence issues automatically.

.PARAMETER MinFeasibilityScore
    Minimum Technical Feasibility score (0-100). Default: 70.

.PARAMETER MinClarityScore
    Minimum Requirement Clarity score (0-100). Default: 60.

.PARAMETER MaxEffortDays
    Maximum effort estimate in days. Default: 2 (Small fixes).

.PARAMETER MaxParallel
    Maximum parallel fix jobs. Default: 5 (worktrees are resource-intensive).

.PARAMETER CLIType
    AI CLI to use: claude, gh-copilot, or vscode. Auto-detected if not specified.

.PARAMETER Model
    Copilot CLI model to use (e.g., gpt-5.2-codex).

.PARAMETER DryRun
    List issues without starting fixes.

.PARAMETER SkipWorktree
    Fix in the current repository instead of creating worktrees (useful for single issue).

.PARAMETER VSCodeProfile
    VS Code profile to use when opening worktrees. Default: Default.

.PARAMETER AutoCommit
    Automatically commit changes after successful fix.

.PARAMETER CreatePR
    Automatically create a pull request after successful fix.

.EXAMPLE
    # Fix a specific issue
    ./Start-IssueAutoFix.ps1 -IssueNumber 12345

.EXAMPLE
    # Find and fix all high-confidence issues (dry run)
    ./Start-IssueAutoFix.ps1 -DryRun

.EXAMPLE
    # Fix issues with very high confidence
    ./Start-IssueAutoFix.ps1 -MinFeasibilityScore 80 -MinClarityScore 70 -MaxEffortDays 1

.EXAMPLE
    # Fix single issue in current repo (no worktree)
    ./Start-IssueAutoFix.ps1 -IssueNumber 12345 -SkipWorktree

.NOTES
    Prerequisites:
    - Run Start-BulkIssueReview.ps1 first to generate review files
    - GitHub CLI (gh) authenticated
    - Claude Code CLI or VS Code with Copilot
    
    Results:
    - Worktrees created at ../<RepoName>-<hash>/
    - Generated Files copied to each worktree
    - Fix agent invoked in each worktree
#>

[CmdletBinding()]
param(
    [int]$IssueNumber,
    
    [int]$MinFeasibilityScore = 70,
    
    [int]$MinClarityScore = 60,
    
    [int]$MaxEffortDays = 2,
    
    [int]$MaxParallel = 5,
    
    [ValidateSet('claude', 'copilot', 'gh-copilot', 'vscode', 'auto')]
    [string]$CLIType = 'auto',

    [string]$Model,
    
    [switch]$DryRun,
    
    [switch]$SkipWorktree,
    
    [Alias('Profile')]
    [string]$VSCodeProfile = 'Default',
    
    [switch]$AutoCommit,
    
    [switch]$CreatePR,
    
    [switch]$Force,
    
    [switch]$Help
)

# Load libraries
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. "$scriptDir/IssueReviewLib.ps1"

# Load worktree library from tools/build
$repoRoot = Get-RepoRoot
$worktreeLib = Join-Path $repoRoot 'tools/build/WorktreeLib.ps1'
if (Test-Path $worktreeLib) {
    . $worktreeLib
}

# Show help
if ($Help) {
    Get-Help $MyInvocation.MyCommand.Path -Full
    return
}

function Start-IssueFixInWorktree {
    <#
    .SYNOPSIS
        Analyze implementation plan and either take action or create worktree for fix.
    .DESCRIPTION
        First analyzes the implementation plan to determine if:
        - Issue is already resolved (close it)
        - Issue needs clarification (add comment)
        - Issue is a duplicate (close as duplicate)
        - Issue is ready to implement (create worktree and fix)
    #>
    param(
        [Parameter(Mandatory)]
        [int]$IssueNumber,
        [Parameter(Mandatory)]
        [string]$SourceRepoRoot,
        [string]$CLIType = 'claude',
        [string]$Model,
        [string]$VSCodeProfile = 'Default',
        [switch]$SkipWorktree,
        [switch]$DryRun
    )

    $issueReviewPath = Get-IssueReviewPath -RepoRoot $SourceRepoRoot -IssueNumber $IssueNumber
    $overviewPath = Join-Path $issueReviewPath 'overview.md'
    $implPlanPath = Join-Path $issueReviewPath 'implementation-plan.md'
    
    # Verify review files exist
    if (-not (Test-Path $overviewPath)) {
        throw "No overview.md found for issue #$IssueNumber. Run Start-BulkIssueReview.ps1 first."
    }
    if (-not (Test-Path $implPlanPath)) {
        throw "No implementation-plan.md found for issue #$IssueNumber. Run Start-BulkIssueReview.ps1 first."
    }

    # =====================================
    # STEP 1: Analyze the implementation plan
    # =====================================
    Info "Analyzing implementation plan for issue #$IssueNumber..."
    $planStatus = Get-ImplementationPlanStatus -ImplementationPlanPath $implPlanPath
    
    # =====================================
    # STEP 2: Execute the recommended action
    # =====================================
    $actionResult = Invoke-ImplementationPlanAction -IssueNumber $IssueNumber -PlanStatus $planStatus -DryRun:$DryRun
    
    # If we shouldn't proceed with fix, return early
    if (-not $actionResult.ShouldProceedWithFix) {
        return @{
            IssueNumber = $IssueNumber
            WorktreePath = $null
            Success = $actionResult.Success
            ActionTaken = $actionResult.ActionTaken
            SkippedCodeFix = $true
        }
    }

    # =====================================
    # STEP 3: Proceed with code fix
    # =====================================

    $workingDir = $SourceRepoRoot

    if (-not $SkipWorktree) {
        # Use the simplified New-WorktreeFromIssue.cmd which only needs issue number
        $worktreeCmd = Join-Path $SourceRepoRoot 'tools/build/New-WorktreeFromIssue.cmd'
        
        Info "Creating worktree for issue #$IssueNumber..."
        
        # Call the cmd script with issue number and -NoVSCode for automation
        & cmd /c $worktreeCmd $IssueNumber -NoVSCode
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create worktree for issue #$IssueNumber"
        }

        # Find the created worktree
        $entries = Get-WorktreeEntries
        $worktreeEntry = $entries | Where-Object { $_.Branch -like "issue/$IssueNumber*" } | Select-Object -First 1
        
        if (-not $worktreeEntry) {
            throw "Failed to find worktree for issue #$IssueNumber"
        }

        $workingDir = $worktreeEntry.Path
        Info "Worktree created at: $workingDir"

        # Copy Generated Files to worktree
        Info "Copying review files to worktree..."
        $destReviewPath = Copy-IssueReviewToWorktree -IssueNumber $IssueNumber -SourceRepoRoot $SourceRepoRoot -WorktreePath $workingDir
        Info "Review files copied to: $destReviewPath"
        
        # Copy .github/skills folder to worktree (needed for MCP config)
        $sourceSkillsPath = Join-Path $SourceRepoRoot '.github/skills'
        $destSkillsPath = Join-Path $workingDir '.github/skills'
        if (Test-Path $sourceSkillsPath) {
            $destGithubPath = Join-Path $workingDir '.github'
            if (-not (Test-Path $destGithubPath)) {
                New-Item -ItemType Directory -Path $destGithubPath -Force | Out-Null
            }
            Copy-Item -Path $sourceSkillsPath -Destination $destGithubPath -Recurse -Force
            Info "Copied .github/skills to worktree"
        }
    }

    # Build the prompt for the fix agent
    $prompt = @"
You are the FixIssue agent. Fix GitHub issue #$IssueNumber.

The implementation plan is at: Generated Files/issueReview/$IssueNumber/implementation-plan.md
The overview is at: Generated Files/issueReview/$IssueNumber/overview.md

Follow the implementation plan exactly. Build and verify after each change.
"@

    # Start the fix agent
    Info "Starting fix agent for issue #$IssueNumber in $workingDir..."
    
    # MCP config for github-artifacts tools (relative to repo root)
    $mcpConfig = '@.github/skills/issue-fix/references/mcp-config.json'
    
    switch ($CLIType) {
        'copilot' {
            # GitHub Copilot CLI (standalone copilot command)
            # -p: Non-interactive prompt mode (exits after completion)
            # --yolo: Enable all permissions for automated execution
            # -s: Silent mode - output only agent response
            # --additional-mcp-config: Load github-artifacts MCP for image/attachment analysis
            $copilotArgs = @(
                '--additional-mcp-config', $mcpConfig,
                '-p', $prompt,
                '--yolo',
                '-s'
            )
            if ($Model) {
                $copilotArgs += @('--model', $Model)
            }
            Info "Running: copilot $($copilotArgs -join ' ')"
            Push-Location $workingDir
            try {
                & copilot @copilotArgs
                if ($LASTEXITCODE -ne 0) {
                    Warn "Copilot exited with code $LASTEXITCODE"
                }
            } finally {
                Pop-Location
            }
        }
        'claude' {
            $claudeArgs = @(
                '--print',
                '--dangerously-skip-permissions',
                '--prompt', $prompt
            )
            Start-Process -FilePath 'claude' -ArgumentList $claudeArgs -WorkingDirectory $workingDir -Wait -NoNewWindow
        }
        'gh-copilot' {
            # Use GitHub Copilot CLI via gh extension
            # gh copilot suggest requires interactive mode, so we open VS Code with the prompt
            Info "GitHub Copilot CLI detected. Opening VS Code with prompt..."
            
            # Create a prompt file in the worktree for easy access
            $promptFile = Join-Path $workingDir "Generated Files/issueReview/$IssueNumber/fix-prompt.md"
            $promptContent = @"
# Fix Issue #$IssueNumber

## Instructions

$prompt

## Quick Start

1. Read the implementation plan: ``Generated Files/issueReview/$IssueNumber/implementation-plan.md``
2. Read the overview: ``Generated Files/issueReview/$IssueNumber/overview.md``
3. Follow the plan step by step
4. Build and test after each change
"@
            Set-Content -Path $promptFile -Value $promptContent -Force
            
            # Open VS Code with the worktree
            code --new-window $workingDir --profile $VSCodeProfile
            Info "VS Code opened at $workingDir"
            Info "Prompt file created at: $promptFile"
            Info "Use GitHub Copilot in VS Code to implement the fix."
        }
        'vscode' {
            # Open VS Code and let user manually trigger the fix
            code --new-window $workingDir --profile $VSCodeProfile
            Info "VS Code opened at $workingDir. Use Copilot to implement the fix."
        }
        default {
            Warn "CLI type '$CLIType' not fully supported for auto-fix. Opening VS Code..."
            code --new-window $workingDir --profile $VSCodeProfile
        }
    }

    # Check if any changes were actually made
    $hasChanges = $false
    Push-Location $workingDir
    try {
        $uncommitted = git status --porcelain 2>$null
        $commitsAhead = git rev-list main..HEAD --count 2>$null
        if ($uncommitted -or ($commitsAhead -gt 0)) {
            $hasChanges = $true
        }
    } finally {
        Pop-Location
    }

    return @{
        IssueNumber = $IssueNumber
        WorktreePath = $workingDir
        Success = $true
        ActionTaken = 'CodeFixAttempted'
        SkippedCodeFix = $false
        HasChanges = $hasChanges
    }
}

#region Main Script
try {
    Info "Repository root: $repoRoot"

    # Detect or validate CLI
    if ($CLIType -eq 'auto') {
        $cli = Get-AvailableCLI
        if ($cli) {
            $CLIType = $cli.Type
            Info "Auto-detected CLI: $($cli.Name)"
        } else {
            $CLIType = 'vscode'
            Info "No CLI detected, will use VS Code"
        }
    }

    # Find issues to fix
    $issuesToFix = @()

    if ($IssueNumber) {
        # Single issue specified
        $reviewResult = Get-IssueReviewResult -IssueNumber $IssueNumber -RepoRoot $repoRoot
        if (-not $reviewResult.HasOverview -or -not $reviewResult.HasImplementationPlan) {
            throw "Issue #$IssueNumber does not have review files. Run Start-BulkIssueReview.ps1 first."
        }
        $issuesToFix += @{
            IssueNumber = $IssueNumber
            OverviewPath = $reviewResult.OverviewPath
            ImplementationPlanPath = $reviewResult.ImplementationPlanPath
        }
    } else {
        # Find high-confidence issues
        Info "`nSearching for high-confidence issues..."
        Info "  Min Feasibility Score: $MinFeasibilityScore"
        Info "  Min Clarity Score: $MinClarityScore"
        Info "  Max Effort: $MaxEffortDays days"

        $highConfidence = Get-HighConfidenceIssues `
            -RepoRoot $repoRoot `
            -MinFeasibilityScore $MinFeasibilityScore `
            -MinClarityScore $MinClarityScore `
            -MaxEffortDays $MaxEffortDays

        if ($highConfidence.Count -eq 0) {
            Warn "No high-confidence issues found matching criteria."
            Info "Try lowering the score thresholds or increasing MaxEffortDays."
            return
        }

        $issuesToFix = $highConfidence
    }

    Info "`nIssues ready for auto-fix: $($issuesToFix.Count)"
    Info ("-" * 80)
    foreach ($issue in $issuesToFix) {
        $scores = ""
        if ($issue.FeasibilityScore) {
            $scores = " [Feasibility: $($issue.FeasibilityScore), Clarity: $($issue.ClarityScore), Effort: $($issue.EffortDays)d]"
        }
        Info ("#{0,-6}{1}" -f $issue.IssueNumber, $scores)
    }
    Info ("-" * 80)

    # In DryRun mode, still analyze plans but don't take action
    if ($DryRun) {
        Info "`nAnalyzing implementation plans (dry run)..."
        foreach ($issue in $issuesToFix) {
            $implPlanPath = Join-Path (Get-IssueReviewPath -RepoRoot $repoRoot -IssueNumber $issue.IssueNumber) 'implementation-plan.md'
            if (Test-Path $implPlanPath) {
                $planStatus = Get-ImplementationPlanStatus -ImplementationPlanPath $implPlanPath
                $color = switch ($planStatus.Action) {
                    'ImplementFix' { 'Green' }
                    'CloseIssue' { 'Yellow' }
                    'AddComment' { 'Cyan' }
                    'LinkDuplicate' { 'Magenta' }
                    default { 'Gray' }
                }
                Write-Host ("  #{0,-6} [{1,-20}] -> {2}" -f $issue.IssueNumber, $planStatus.Status, $planStatus.Action) -ForegroundColor $color
                if ($planStatus.RelatedPR) {
                    $prInfo = "PR #$($planStatus.RelatedPR)"
                    if ($planStatus.ReleasedIn) {
                        $prInfo += " (released in $($planStatus.ReleasedIn))"
                    } elseif ($planStatus.Status -eq 'FixedButUnreleased') {
                        $prInfo += " (merged, awaiting release)"
                    }
                    Write-Host "         $prInfo" -ForegroundColor DarkGray
                }
                if ($planStatus.DuplicateOf) {
                    Write-Host "         Duplicate of #$($planStatus.DuplicateOf)" -ForegroundColor DarkGray
                }
            }
        }
        Warn "`nDry run mode - no actions taken."
        return
    }

    # Confirm before proceeding (skip if -Force)
    if (-not $Force) {
        $confirm = Read-Host "`nProceed with fixing $($issuesToFix.Count) issues? (y/N)"
        if ($confirm -notmatch '^[yY]') {
            Info "Cancelled."
            return
        }
    }

    # Process issues
    $results = @{
        Succeeded = @()
        Failed = @()
        AlreadyResolved = @()
        AwaitingRelease = @()
        NeedsClarification = @()
        Duplicates = @()
        NoChanges = @()
    }

    foreach ($issue in $issuesToFix) {
        try {
            Info "`n" + ("=" * 60)
            Info "PROCESSING ISSUE #$($issue.IssueNumber)"
            Info ("=" * 60)

            $result = Start-IssueFixInWorktree `
                -IssueNumber $issue.IssueNumber `
                -SourceRepoRoot $repoRoot `
                -CLIType $CLIType `
                -Model $Model `
                -VSCodeProfile $VSCodeProfile `
                -SkipWorktree:$SkipWorktree `
                -DryRun:$DryRun

            if ($result.SkippedCodeFix) {
                # Action was taken but no code fix (e.g., closed issue, added comment)
                switch -Wildcard ($result.ActionTaken) {
                    '*Closing*' { $results.AlreadyResolved += $issue.IssueNumber }
                    '*clarification*' { $results.NeedsClarification += $issue.IssueNumber }
                    '*duplicate*' { $results.Duplicates += $issue.IssueNumber }
                    '*merged*awaiting*' { $results.AwaitingRelease += $issue.IssueNumber }
                    '*merged but not yet released*' { $results.AwaitingRelease += $issue.IssueNumber }
                    default { $results.Succeeded += $issue.IssueNumber }
                }
                Success "✓ Issue #$($issue.IssueNumber) handled: $($result.ActionTaken)"
            }
            elseif ($result.HasChanges) {
                $results.Succeeded += $issue.IssueNumber
                Success "✓ Issue #$($issue.IssueNumber) fix completed with changes"
            }
            else {
                $results.NoChanges += $issue.IssueNumber
                Warn "⚠ Issue #$($issue.IssueNumber) fix ran but no code changes were made"
            }
        }
        catch {
            Err "✗ Issue #$($issue.IssueNumber) failed: $($_.Exception.Message)"
            $results.Failed += $issue.IssueNumber
        }
    }

    # Summary
    Info "`n" + ("=" * 80)
    Info "AUTO-FIX COMPLETE"
    Info ("=" * 80)
    Info "Total issues:       $($issuesToFix.Count)"
    if ($results.Succeeded.Count -gt 0) {
        Success "Code fixes:         $($results.Succeeded.Count)"
    }
    if ($results.AlreadyResolved.Count -gt 0) {
        Success "Already resolved:   $($results.AlreadyResolved.Count) (issues closed)"
    }
    if ($results.AwaitingRelease.Count -gt 0) {
        Info "Awaiting release:   $($results.AwaitingRelease.Count) (fix merged, pending release)"
    }
    if ($results.NeedsClarification.Count -gt 0) {
        Warn "Need clarification: $($results.NeedsClarification.Count) (comments added)"
    }
    if ($results.Duplicates.Count -gt 0) {
        Warn "Duplicates:         $($results.Duplicates.Count) (issues closed)"
    }
    if ($results.NoChanges.Count -gt 0) {
        Warn "No changes made:    $($results.NoChanges.Count)"
    }
    if ($results.Failed.Count -gt 0) {
        Err "Failed:             $($results.Failed.Count)"
        Err "Failed issues:      $($results.Failed -join ', ')"
    }
    Info ("=" * 80)

    if (-not $SkipWorktree -and ($results.Succeeded.Count -gt 0 -or $results.NoChanges.Count -gt 0)) {
        Info "`nWorktrees created. Use 'git worktree list' to see all worktrees."
        Info "To clean up: Delete-Worktree.ps1 -Branch issue/<number>"
    }

    # Write signal files for orchestrator
    $genFiles = Get-GeneratedFilesPath -RepoRoot $repoRoot
    foreach ($issueNum in $results.Succeeded) {
        $signalDir = Join-Path $genFiles "issueFix/$issueNum"
        if (-not (Test-Path $signalDir)) { New-Item -ItemType Directory -Path $signalDir -Force | Out-Null }
        @{
            status = "success"
            issueNumber = $issueNum
            timestamp = (Get-Date).ToString("o")
            worktreePath = (git worktree list --porcelain | Select-String "worktree.*issue.$issueNum" | ForEach-Object { $_.Line -replace 'worktree ', '' })
        } | ConvertTo-Json | Set-Content "$signalDir/.signal" -Force
    }
    foreach ($issueNum in $results.Failed) {
        $signalDir = Join-Path $genFiles "issueFix/$issueNum"
        if (-not (Test-Path $signalDir)) { New-Item -ItemType Directory -Path $signalDir -Force | Out-Null }
        @{
            status = "failure"
            issueNumber = $issueNum
            timestamp = (Get-Date).ToString("o")
        } | ConvertTo-Json | Set-Content "$signalDir/.signal" -Force
    }

    return $results
}
catch {
    Err "Error: $($_.Exception.Message)"
    exit 1
}
#endregion