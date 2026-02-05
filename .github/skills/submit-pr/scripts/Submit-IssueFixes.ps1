<#!
.SYNOPSIS
    Commit and create PRs for completed issue fixes in worktrees.

.DESCRIPTION
    For each specified issue (or all issue worktrees), commits changes using AI-generated
    commit messages and creates PRs with AI-generated summaries, linking to the original issue.

.PARAMETER IssueNumbers
    Array of issue numbers to submit. If not specified, processes all issue/* worktrees.

.PARAMETER DryRun
    Show what would be done without actually committing or creating PRs.

.PARAMETER SkipCommit
    Skip the commit step (assume changes are already committed).

.PARAMETER SkipPush
    Skip pushing to remote (useful for testing).

.PARAMETER TargetBranch
    Target branch for the PR. Default: main.

.PARAMETER CLIType
    AI CLI to use for generating messages: copilot, claude, or manual. Default: copilot.

.PARAMETER Draft
    Create PRs as drafts.

.EXAMPLE
    # Submit all issue worktrees
    ./Submit-IssueFixes.ps1

.EXAMPLE
    # Submit specific issues
    ./Submit-IssueFixes.ps1 -IssueNumbers 44044, 44480

.EXAMPLE
    # Dry run to see what would happen
    ./Submit-IssueFixes.ps1 -DryRun

.EXAMPLE
    # Create draft PRs
    ./Submit-IssueFixes.ps1 -Draft

.NOTES
    Prerequisites:
    - Worktrees created by Start-IssueAutoFix.ps1
    - Changes made in the worktrees
    - GitHub CLI (gh) authenticated
    - Copilot CLI or Claude Code CLI
#>

[CmdletBinding()]
param(
    [int[]]$IssueNumbers,
    
    [switch]$DryRun,
    
    [switch]$SkipCommit,
    
    [switch]$SkipPush,
    
    [string]$TargetBranch = 'main',
    
    [ValidateSet('copilot', 'claude', 'manual')]
    [string]$CLIType = 'copilot',
    
    [switch]$Draft,
    
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

function Get-AIGeneratedCommitTitle {
    <#
    .SYNOPSIS
        Generate commit title using AI CLI with create-commit-title prompt.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$WorktreePath,
        [string]$CLIType = 'copilot'
    )

    $promptFile = Join-Path $repoRoot '.github/prompts/create-commit-title.prompt.md'
    if (-not (Test-Path $promptFile)) {
        throw "Prompt file not found: $promptFile"
    }

    $prompt = "Follow the instructions in .github/prompts/create-commit-title.prompt.md to generate a commit title for the current changes. Output ONLY the commit title, nothing else."

    # MCP config for github-artifacts tools (relative to repo root)
    $mcpConfig = '@.github/skills/submit-pr/references/mcp-config.json'
    
    Push-Location $WorktreePath
    try {
        switch ($CLIType) {
            'copilot' {
                $result = & copilot --additional-mcp-config $mcpConfig -p $prompt --yolo -s 2>&1
                # Extract just the title line (last non-empty line that looks like a title)
                $lines = $result -split "`n" | Where-Object { $_.Trim() -and $_ -notmatch '^\s*```' -and $_ -notmatch '^\s*#' }
                $title = $lines | Select-Object -Last 1
                return $title.Trim()
            }
            'claude' {
                $result = & claude --print --dangerously-skip-permissions --prompt $prompt 2>&1
                $lines = $result -split "`n" | Where-Object { $_.Trim() -and $_ -notmatch '^\s*```' }
                $title = $lines | Select-Object -Last 1
                return $title.Trim()
            }
            'manual' {
                # Show diff and ask user for title
                git diff HEAD --stat
                return Read-Host "Enter commit title"
            }
        }
    } finally {
        Pop-Location
    }
}

function Get-AIGeneratedPRSummary {
    <#
    .SYNOPSIS
        Generate PR summary using AI CLI with create-pr-summary prompt.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$WorktreePath,
        [Parameter(Mandatory)]
        [int]$IssueNumber,
        [string]$TargetBranch = 'main',
        [string]$CLIType = 'copilot'
    )

    $prompt = @"
Follow the instructions in .github/prompts/create-pr-summary.prompt.md to generate a PR summary.
Target branch: $TargetBranch
This PR fixes issue #$IssueNumber.

IMPORTANT: 
1. Output the PR title on the first line
2. Then output the PR body in markdown format
3. Make sure to include "Fixes #$IssueNumber" in the body to auto-link the issue
"@

    # MCP config for github-artifacts tools (relative to repo root)
    $mcpConfig = '@.github/skills/submit-pr/references/mcp-config.json'
    
    Push-Location $WorktreePath
    try {
        switch ($CLIType) {
            'copilot' {
                $result = & copilot --additional-mcp-config $mcpConfig -p $prompt --yolo -s 2>&1
                return $result -join "`n"
            }
            'claude' {
                $result = & claude --print --dangerously-skip-permissions --prompt $prompt 2>&1
                return $result -join "`n"
            }
            'manual' {
                git diff "$TargetBranch...HEAD" --stat
                $title = Read-Host "Enter PR title"
                $body = Read-Host "Enter PR body (or press Enter for default)"
                if (-not $body) {
                    $body = "Fixes #$IssueNumber"
                }
                return "$title`n`n$body"
            }
        }
    } finally {
        Pop-Location
    }
}

function Parse-PRContent {
    <#
    .SYNOPSIS
        Parse AI output to extract PR title and body.
        Expected format:
          Line 1: feat(scope): title text
          Line 2+: ```markdown
                   ## Summary...
                   ```
    #>
    param(
        [Parameter(Mandatory)]
        [string]$Content,
        [int]$IssueNumber
    )

    $lines = $Content -split "`n"
    
    # Title is the FIRST line that looks like a conventional commit
    # Body is the content INSIDE the ```markdown ... ``` block
    $title = $null
    $body = $null
    
    # Find title - first line matching conventional commit format
    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        if ($trimmed -match '^(feat|fix|docs|refactor|perf|test|build|ci|chore)(\([^)]+\))?:') {
            $title = $trimmed -replace '^#+\s*', ''
            break
        }
    }
    
    # Fallback title
    if (-not $title) {
        $title = "fix: address issue #$IssueNumber"
    }
    
    # Extract body from markdown code block
    $fullContent = $Content
    if ($fullContent -match '```markdown\r?\n([\s\S]*?)\r?\n```') {
        $body = $Matches[1].Trim()
    } else {
        # No markdown block - use everything after the title line
        $titleIndex = [array]::IndexOf($lines, ($lines | Where-Object { $_.Trim() -eq $title } | Select-Object -First 1))
        if ($titleIndex -ge 0 -and $titleIndex -lt $lines.Count - 1) {
            $body = ($lines[($titleIndex + 1)..($lines.Count - 1)] -join "`n").Trim()
            # Clean up any remaining code fences
            $body = $body -replace '^```\w*\r?\n', '' -replace '\r?\n```\s*$', ''
        } else {
            $body = ""
        }
    }
    
    # Ensure issue link is present
    if ($body -notmatch "Fixes\s*#$IssueNumber" -and $body -notmatch "Closes\s*#$IssueNumber" -and $body -notmatch "Resolves\s*#$IssueNumber") {
        $body = "$body`n`nFixes #$IssueNumber"
    }

    return @{
        Title = $title
        Body = $body
    }
}

function Submit-IssueFix {
    <#
    .SYNOPSIS
        Commit changes, push, and create PR for a single issue.
    #>
    param(
        [Parameter(Mandatory)]
        [int]$IssueNumber,
        [Parameter(Mandatory)]
        [string]$WorktreePath,
        [Parameter(Mandatory)]
        [string]$Branch,
        [string]$TargetBranch = 'main',
        [string]$CLIType = 'copilot',
        [switch]$DryRun,
        [switch]$SkipCommit,
        [switch]$SkipPush,
        [switch]$Draft
    )

    Push-Location $WorktreePath
    try {
        # Check for changes
        $status = git status --porcelain
        $hasUncommitted = $status.Count -gt 0
        
        # Check for commits ahead of target
        git fetch origin $TargetBranch 2>$null
        $commitsAhead = git rev-list --count "origin/$TargetBranch..$Branch" 2>$null
        if (-not $commitsAhead) { $commitsAhead = 0 }

        Info "Issue #$IssueNumber in $WorktreePath"
        Info "  Branch: $Branch"
        Info "  Uncommitted changes: $hasUncommitted"
        Info "  Commits ahead of $TargetBranch`: $commitsAhead"

        if (-not $hasUncommitted -and $commitsAhead -eq 0) {
            Warn "  No changes to submit for issue #$IssueNumber"
            return @{ IssueNumber = $IssueNumber; Status = 'NoChanges' }
        }

        # Step 1: Commit if there are uncommitted changes
        if ($hasUncommitted -and -not $SkipCommit) {
            Info "  Generating commit title..."
            
            if ($DryRun) {
                Info "  [DRY RUN] Would generate commit title and commit changes"
            } else {
                $commitTitle = Get-AIGeneratedCommitTitle -WorktreePath $WorktreePath -CLIType $CLIType
                
                if (-not $commitTitle) {
                    throw "Failed to generate commit title"
                }
                
                Info "  Commit title: $commitTitle"
                
                # Stage all changes and commit
                git add -A
                git commit -m $commitTitle
                
                if ($LASTEXITCODE -ne 0) {
                    throw "Git commit failed"
                }
                
                Success "  ✓ Changes committed"
            }
        }

        # Step 2: Push to remote
        if (-not $SkipPush) {
            if ($DryRun) {
                Info "  [DRY RUN] Would push branch $Branch to origin"
            } else {
                Info "  Pushing to origin..."
                git push -u origin $Branch 2>&1 | Out-Null
                
                if ($LASTEXITCODE -ne 0) {
                    # Try force push if normal push fails (branch might have been reset)
                    Warn "  Normal push failed, trying force push..."
                    git push -u origin $Branch --force-with-lease 2>&1 | Out-Null
                    if ($LASTEXITCODE -ne 0) {
                        throw "Git push failed"
                    }
                }
                
                Success "  ✓ Pushed to origin"
            }
        }

        # Step 3: Create PR
        Info "  Generating PR summary..."
        
        if ($DryRun) {
            Info "  [DRY RUN] Would generate PR summary and create PR"
            Info "  [DRY RUN] PR would link to issue #$IssueNumber"
            return @{ IssueNumber = $IssueNumber; Status = 'DryRun' }
        }
        
        # Check if PR already exists
        $existingPR = gh pr list --head $Branch --json number,url 2>$null | ConvertFrom-Json
        if ($existingPR -and $existingPR.Count -gt 0) {
            Warn "  PR already exists: $($existingPR[0].url)"
            return @{ IssueNumber = $IssueNumber; Status = 'PRExists'; PRUrl = $existingPR[0].url }
        }

        $prContent = Get-AIGeneratedPRSummary -WorktreePath $WorktreePath -IssueNumber $IssueNumber -TargetBranch $TargetBranch -CLIType $CLIType
        $parsed = Parse-PRContent -Content $prContent -IssueNumber $IssueNumber

        if (-not $parsed.Title) {
            throw "Failed to generate PR title"
        }

        Info "  PR Title: $($parsed.Title)"

        # Create PR using gh CLI
        $ghArgs = @(
            'pr', 'create',
            '--base', $TargetBranch,
            '--head', $Branch,
            '--title', $parsed.Title,
            '--body', $parsed.Body
        )

        if ($Draft) {
            $ghArgs += '--draft'
        }

        $prResult = & gh @ghArgs 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create PR: $prResult"
        }

        # Extract PR URL from result
        $prUrl = $prResult | Select-String -Pattern 'https://github.com/[^\s]+' | ForEach-Object { $_.Matches[0].Value }
        
        Success "  ✓ PR created: $prUrl"

        return @{
            IssueNumber = $IssueNumber
            Status = 'Success'
            PRUrl = $prUrl
            CommitTitle = $commitTitle
            PRTitle = $parsed.Title
        }
    }
    catch {
        Err "  ✗ Failed: $($_.Exception.Message)"
        return @{
            IssueNumber = $IssueNumber
            Status = 'Failed'
            Error = $_.Exception.Message
        }
    }
    finally {
        Pop-Location
    }
}

#region Main Script
try {
    Info "Repository root: $repoRoot"
    Info "Target branch: $TargetBranch"
    Info "CLI type: $CLIType"

    # Get all issue worktrees
    $allWorktrees = Get-WorktreeEntries | Where-Object { $_.Branch -like 'issue/*' }

    if ($allWorktrees.Count -eq 0) {
        Warn "No issue worktrees found. Run Start-IssueAutoFix.ps1 first."
        return
    }

    # Filter to specified issues if provided
    $worktreesToProcess = @()
    
    if ($IssueNumbers -and $IssueNumbers.Count -gt 0) {
        foreach ($issueNum in $IssueNumbers) {
            $wt = $allWorktrees | Where-Object { $_.Branch -match "issue/$issueNum\b" }
            if ($wt) {
                $worktreesToProcess += $wt
            } else {
                Warn "No worktree found for issue #$issueNum"
            }
        }
    } else {
        $worktreesToProcess = $allWorktrees
    }

    if ($worktreesToProcess.Count -eq 0) {
        Warn "No worktrees to process."
        return
    }

    # Display worktrees to process
    Info "`nWorktrees to submit:"
    Info ("-" * 80)
    foreach ($wt in $worktreesToProcess) {
        # Extract issue number from branch name
        if ($wt.Branch -match 'issue/(\d+)') {
            $issueNum = $Matches[1]
            Info "  #$issueNum -> $($wt.Path) [$($wt.Branch)]"
        }
    }
    Info ("-" * 80)

    if ($DryRun) {
        Warn "`nDry run mode - no changes will be made."
    }

    # Confirm before proceeding
    if (-not $Force -and -not $DryRun) {
        $confirm = Read-Host "`nProceed with submitting $($worktreesToProcess.Count) fixes? (y/N)"
        if ($confirm -notmatch '^[yY]') {
            Info "Cancelled."
            return
        }
    }

    # Process each worktree
    $results = @{
        Success = @()
        Failed = @()
        NoChanges = @()
        PRExists = @()
        DryRun = @()
    }

    foreach ($wt in $worktreesToProcess) {
        if ($wt.Branch -match 'issue/(\d+)') {
            $issueNum = [int]$Matches[1]
            
            Info "`n" + ("=" * 60)
            Info "SUBMITTING ISSUE #$issueNum"
            Info ("=" * 60)

            $result = Submit-IssueFix `
                -IssueNumber $issueNum `
                -WorktreePath $wt.Path `
                -Branch $wt.Branch `
                -TargetBranch $TargetBranch `
                -CLIType $CLIType `
                -DryRun:$DryRun `
                -SkipCommit:$SkipCommit `
                -SkipPush:$SkipPush `
                -Draft:$Draft

            switch ($result.Status) {
                'Success' { $results.Success += $result }
                'Failed' { $results.Failed += $result }
                'NoChanges' { $results.NoChanges += $result }
                'PRExists' { $results.PRExists += $result }
                'DryRun' { $results.DryRun += $result }
            }
        }
    }

    # Summary
    Info "`n" + ("=" * 80)
    Info "SUBMISSION COMPLETE"
    Info ("=" * 80)
    Info "Total worktrees:     $($worktreesToProcess.Count)"
    
    if ($results.Success.Count -gt 0) {
        Success "PRs created:         $($results.Success.Count)"
        foreach ($r in $results.Success) {
            Success "  #$($r.IssueNumber): $($r.PRUrl)"
        }
    }
    
    if ($results.PRExists.Count -gt 0) {
        Warn "PRs already exist:   $($results.PRExists.Count)"
        foreach ($r in $results.PRExists) {
            Warn "  #$($r.IssueNumber): $($r.PRUrl)"
        }
    }
    
    if ($results.NoChanges.Count -gt 0) {
        Warn "No changes:          $($results.NoChanges.Count)"
        Warn "  Issues: $($results.NoChanges.IssueNumber -join ', ')"
    }
    
    if ($results.Failed.Count -gt 0) {
        Err "Failed:              $($results.Failed.Count)"
        foreach ($r in $results.Failed) {
            Err "  #$($r.IssueNumber): $($r.Error)"
        }
    }
    
    if ($results.DryRun.Count -gt 0) {
        Info "Dry run:             $($results.DryRun.Count)"
    }
    
    Info ("=" * 80)

    return $results
}
catch {
    Err "Error: $($_.Exception.Message)"
    exit 1
}
#endregion
