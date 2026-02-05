<#!
.SYNOPSIS
    Review and fix PRs in parallel using GitHub Copilot and MCP.

.DESCRIPTION
    For each PR (from worktrees, specified, or fetched from repo), runs:
    1. Assigns GitHub Copilot as reviewer via GitHub MCP
    2. Runs review-pr.prompt.md to generate review and post comments
    3. Runs fix-pr-active-comments.prompt.md to fix issues

.PARAMETER PRNumbers
    Array of PR numbers to process. If not specified, finds PRs from issue worktrees.

.PARAMETER AllOpen
    Fetch and process ALL open non-draft PRs from the repository.

.PARAMETER Assigned
    Fetch and process PRs assigned to the current user.

.PARAMETER Limit
    Maximum number of PRs to fetch when using -AllOpen or -Assigned. Default: 100.

.PARAMETER SkipExisting
    Skip PRs that already have a completed review (00-OVERVIEW.md exists).

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

.PARAMETER GenerateBatchScript
    Instead of running reviews, generate a standalone batch script that can be run
    in background. The script will be saved to Generated Files/prReview/_batch-review.ps1.

.PARAMETER CLIType
    AI CLI to use: copilot or claude. Default: copilot.

.PARAMETER Model
    Copilot CLI model to use (e.g., gpt-5.2-codex).

.EXAMPLE
    # Process all PRs from issue worktrees
    ./Start-PRReviewWorkflow.ps1

.EXAMPLE
    # Process specific PRs
    ./Start-PRReviewWorkflow.ps1 -PRNumbers 45234, 45235

.EXAMPLE
    # Review ALL open PRs in the repo
    ./Start-PRReviewWorkflow.ps1 -AllOpen -SkipFix -SkipAssign

.EXAMPLE
    # Review PRs assigned to me, skip already reviewed
    ./Start-PRReviewWorkflow.ps1 -Assigned -SkipExisting

.EXAMPLE
    # Generate a batch script for background execution
    ./Start-PRReviewWorkflow.ps1 -AllOpen -SkipExisting -GenerateBatchScript

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
    
    [switch]$AllOpen,
    
    [switch]$Assigned,
    
    [int]$Limit = 100,
    
    [switch]$SkipExisting,
    
    [switch]$SkipAssign,
    
    [switch]$SkipReview,
    
    [switch]$SkipFix,
    
    [ValidateSet('high', 'medium', 'low', 'info')]
    [string]$MinSeverity = 'medium',
    
    [int]$MaxParallel = 3,
    
    [switch]$DryRun,
    
    [switch]$GenerateBatchScript,
    
    [ValidateSet('copilot', 'claude')]
    [string]$CLIType = 'copilot',

    [string]$Model,
    
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

function Get-AllOpenPRs {
    <#
    .SYNOPSIS
        Get all open non-draft PRs from the repository.
    #>
    param(
        [int]$Limit = 100
    )
    
    Info "Fetching all open PRs (limit: $Limit)..."
    $prList = gh pr list --state open --json number,url,headRefName,isDraft --limit $Limit 2>$null | ConvertFrom-Json
    
    if (-not $prList) {
        return @()
    }
    
    # Filter out drafts
    $prs = @()
    foreach ($pr in $prList | Where-Object { -not $_.isDraft }) {
        $prs += @{
            PRNumber = $pr.number
            PRUrl = $pr.url
            Branch = $pr.headRefName
            WorktreePath = $repoRoot
        }
    }
    
    Info "Found $($prs.Count) non-draft open PRs"
    return $prs
}

function Get-AssignedPRs {
    <#
    .SYNOPSIS
        Get PRs assigned to the current user.
    #>
    param(
        [int]$Limit = 100
    )
    
    Info "Fetching PRs assigned to @me (limit: $Limit)..."
    $prList = gh pr list --assignee @me --state open --json number,url,headRefName,isDraft --limit $Limit 2>$null | ConvertFrom-Json
    
    if (-not $prList) {
        return @()
    }
    
    $prs = @()
    foreach ($pr in $prList | Where-Object { -not $_.isDraft }) {
        $prs += @{
            PRNumber = $pr.number
            PRUrl = $pr.url
            Branch = $pr.headRefName
            WorktreePath = $repoRoot
        }
    }
    
    Info "Found $($prs.Count) assigned PRs"
    return $prs
}

function Test-ReviewExists {
    <#
    .SYNOPSIS
        Check if a PR review already exists (has 00-OVERVIEW.md).
    #>
    param(
        [int]$PRNumber
    )
    
    $reviewPath = Join-Path $repoRoot "Generated Files/prReview/$PRNumber/00-OVERVIEW.md"
    return Test-Path $reviewPath
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
        [string]$Model,
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

    # MCP config for github-artifacts tools - use absolute path from main repo
    $mcpConfigPath = Join-Path $repoRoot '.github/skills/pr-review/references/mcp-config.json'
    $mcpConfig = "@$mcpConfigPath"
    
    try {
        Info "  Requesting Copilot review via GitHub MCP..."
        
        switch ($CLIType) {
            'copilot' {
                $copilotArgs = @('--additional-mcp-config', $mcpConfig, '-p', $prompt, '--yolo', '-s')
                if ($Model) {
                    $copilotArgs += @('--model', $Model)
                }
                & copilot @copilotArgs 2>&1 | Out-Null
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
        [string]$Model,
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
    
    # MCP config for github-artifacts tools - use absolute path from main repo
    $mcpConfigPath = Join-Path $repoRoot '.github/skills/pr-review/references/mcp-config.json'
    $mcpConfig = "@$mcpConfigPath"
    
    Push-Location $repoRoot
    try {
        switch ($CLIType) {
            'copilot' {
                Info "  Running Copilot review (this may take several minutes)..."
                $copilotArgs = @('--additional-mcp-config', $mcpConfig, '-p', $prompt, '--yolo')
                if ($Model) {
                    $copilotArgs += @('--model', $Model)
                }
                $output = & copilot @copilotArgs 2>&1
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
        [string]$Model,
        [switch]$DryRun
    )

    # Simple prompt - let the prompt file define all the details
    $prompt = "Follow .github/prompts/fix-pr-active-comments.prompt.md for PR #$PRNumber."

    if ($DryRun) {
        Info "  [DRY RUN] Would fix PR comments for #$PRNumber"
        return @{ Success = $true }
    }

    $workDir = if ($WorktreePath -and (Test-Path $WorktreePath)) { $WorktreePath } else { $repoRoot }

    # MCP config for github-artifacts tools - use absolute path from main repo
    # This is needed because worktrees don't have .github folder
    $mcpConfigPath = Join-Path $repoRoot '.github/skills/pr-review/references/mcp-config.json'
    $mcpConfig = "@$mcpConfigPath"
    
    Push-Location $workDir
    try {
        switch ($CLIType) {
            'copilot' {
                Info "  Running Copilot to fix comments..."
                $copilotArgs = @('--additional-mcp-config', $mcpConfig, '-p', $prompt, '--yolo')
                if ($Model) {
                    $copilotArgs += @('--model', $Model)
                }
                $output = & copilot @copilotArgs 2>&1
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
        [string]$Model,
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
        $result.AssignResult = Invoke-AssignCopilotReviewer -PRNumber $PRNumber -CLIType $CLIType -Model $Model -DryRun:$DryRun
        if (-not $result.AssignResult) {
            Warn "  Assignment step had issues (continuing...)"
        }
    } else {
        Info "  Step 1: Skipped (assign)"
    }

    # Step 2: Run PR review
    if (-not $SkipReview) {
        Info "  Step 2: Running PR review..."
        $result.ReviewResult = Invoke-PRReview -PRNumber $PRNumber -CLIType $CLIType -Model $Model -MinSeverity $MinSeverity -DryRun:$DryRun
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
        $result.FixResult = Invoke-FixPRComments -PRNumber $PRNumber -WorktreePath $WorktreePath -CLIType $CLIType -Model $Model -DryRun:$DryRun
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
    } elseif ($AllOpen) {
        # Fetch all open PRs from repository
        $prsToProcess = Get-AllOpenPRs -Limit $Limit
    } elseif ($Assigned) {
        # Fetch PRs assigned to current user
        $prsToProcess = Get-AssignedPRs -Limit $Limit
    } else {
        # Get PRs from worktrees
        Info "`nFinding PRs from issue worktrees..."
        $prsToProcess = Get-PRsFromWorktrees
    }

    # Filter out already reviewed PRs if requested
    if ($SkipExisting -and $prsToProcess.Count -gt 0) {
        $beforeCount = $prsToProcess.Count
        $prsToProcess = $prsToProcess | Where-Object { -not (Test-ReviewExists -PRNumber $_.PRNumber) }
        $skippedCount = $beforeCount - $prsToProcess.Count
        if ($skippedCount -gt 0) {
            Info "Skipped $skippedCount PRs with existing reviews"
        }
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

    # Generate batch script mode - creates a standalone script for background execution
    if ($GenerateBatchScript) {
        $batchPath = Join-Path $repoRoot "Generated Files/prReview/_batch-review.ps1"
        $prNumbers = $prsToProcess | ForEach-Object { $_.PRNumber }
        
        $batchContent = @"
# Auto-generated batch review script
# Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
# PRs to review: $($prNumbers.Count)
#
# Run this script in a PowerShell terminal to review all PRs sequentially.
# Each review takes 2-5 minutes. Total estimated time: $([math]::Ceiling($prNumbers.Count * 3)) minutes.
#
# Usage: pwsh -File "$batchPath"

`$ErrorActionPreference = 'Continue'
`$repoRoot = '$repoRoot'
Set-Location `$repoRoot

`$prNumbers = @($($prNumbers -join ', '))
`$total = `$prNumbers.Count
`$completed = 0
`$failed = @()

Write-Host "Starting batch review of `$total PRs" -ForegroundColor Cyan
Write-Host "Estimated time: $([math]::Ceiling($prNumbers.Count * 3)) minutes" -ForegroundColor Yellow
Write-Host ""

foreach (`$pr in `$prNumbers) {
    `$completed++
    `$reviewPath = Join-Path `$repoRoot "Generated Files/prReview/`$pr"
    
    # Skip if already reviewed
    if (Test-Path (Join-Path `$reviewPath "00-OVERVIEW.md")) {
        Write-Host "[`$completed/`$total] PR #`$pr - Already reviewed, skipping" -ForegroundColor DarkGray
        continue
    }
    
    Write-Host "[`$completed/`$total] PR #`$pr - Starting review..." -ForegroundColor Cyan
    
    try {
        # Create output directory
        if (-not (Test-Path `$reviewPath)) {
            New-Item -ItemType Directory -Path `$reviewPath -Force | Out-Null
        }
        
        # Run copilot review
        `$prompt = "Follow exactly what at .github/skills/pr-review/references/review-pr.prompt.md to do with PR #`$pr. Write output to Generated Files/prReview/`$pr/. Do not post comments to GitHub."
        
        & copilot -p `$prompt --yolo 2>&1 | Out-File -FilePath (Join-Path `$reviewPath "_copilot.log") -Force
        
        # Check if review completed
        if (Test-Path (Join-Path `$reviewPath "00-OVERVIEW.md")) {
            Write-Host "[`$completed/`$total] PR #`$pr - Review completed" -ForegroundColor Green
        } else {
            Write-Host "[`$completed/`$total] PR #`$pr - Review may be incomplete (no overview file)" -ForegroundColor Yellow
            `$failed += `$pr
        }
    }
    catch {
        Write-Host "[`$completed/`$total] PR #`$pr - FAILED: `$(`$_.Exception.Message)" -ForegroundColor Red
        `$failed += `$pr
    }
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Batch review complete!" -ForegroundColor Cyan
Write-Host "Total: `$total | Completed: `$(`$total - `$failed.Count) | Failed: `$(`$failed.Count)" -ForegroundColor Cyan
if (`$failed.Count -gt 0) {
    Write-Host "Failed PRs: `$(`$failed -join ', ')" -ForegroundColor Red
}
Write-Host "======================================" -ForegroundColor Cyan
"@

        $batchContent | Out-File -FilePath $batchPath -Encoding UTF8 -Force
        
        Success "`nBatch script generated: $batchPath"
        Info "PRs included: $($prNumbers.Count)"
        Info ""
        Info "To run the batch review in background:"
        Info "  Start-Process pwsh -ArgumentList '-File',`"$batchPath`" -WindowStyle Minimized"
        Info ""
        Info "Or run interactively to see progress:"
        Info "  pwsh -File `"$batchPath`""
        
        return @{
            BatchScript = $batchPath
            PRCount = $prNumbers.Count
            PRNumbers = $prNumbers
        }
    }

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
                    -Model $Model `
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
                -Model $Model `
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

    # Write signal files for orchestrator
    foreach ($r in $results.Success) {
        $signalPath = Join-Path $repoRoot "Generated Files/prReview/$($r.PRNumber)/.signal"
        @{
            status = "success"
            prNumber = $r.PRNumber
            timestamp = (Get-Date).ToString("o")
        } | ConvertTo-Json | Set-Content $signalPath -Force
    }
    foreach ($r in $results.Failed) {
        $signalPath = Join-Path $repoRoot "Generated Files/prReview/$($r.PRNumber)/.signal"
        @{
            status = "failure"
            prNumber = $r.PRNumber
            timestamp = (Get-Date).ToString("o")
        } | ConvertTo-Json | Set-Content $signalPath -Force
    }

    return $results
}
catch {
    Err "Error: $($_.Exception.Message)"
    exit 1
}
#endregion
