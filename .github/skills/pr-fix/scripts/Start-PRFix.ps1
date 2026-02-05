<#
.SYNOPSIS
    Fix active PR review comments using AI CLI.

.DESCRIPTION
    Kicks off Copilot/Claude CLI to address active review comments on a PR.
    Does NOT resolve threads - that must be done by VS Code agent via GraphQL.

.PARAMETER PRNumber
    PR number to fix.

.PARAMETER CLIType
    AI CLI to use: copilot or claude. Default: copilot.

.PARAMETER Model
    Copilot CLI model to use (e.g., gpt-5.2-codex).

.PARAMETER WorktreePath
    Path to the worktree containing the PR branch. Auto-detected if not specified.

.PARAMETER DryRun
    Show what would be done without executing.

.PARAMETER Force
    Skip confirmation prompts.

.EXAMPLE
    ./Start-PRFix.ps1 -PRNumber 45286 -CLIType copilot -Force

.NOTES
    After this script completes, use VS Code agent to resolve threads via GraphQL.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [int]$PRNumber,
    
    [ValidateSet('copilot', 'claude')]
    [string]$CLIType = 'copilot',

    [string]$Model,
    
    [string]$WorktreePath,
    
    [switch]$DryRun,
    
    [switch]$Force,
    
    [switch]$Help
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptDir 'IssueReviewLib.ps1')

$repoRoot = Get-RepoRoot
$worktreeLib = Join-Path $repoRoot 'tools/build/WorktreeLib.ps1'
if (Test-Path $worktreeLib) {
    . $worktreeLib
}

if ($Help) {
    Get-Help $MyInvocation.MyCommand.Path -Full
    return
}

function Get-PRBranch {
    param([int]$PRNumber)
    
    $prInfo = gh pr view $PRNumber --json headRefName 2>$null | ConvertFrom-Json
    if ($prInfo) {
        return $prInfo.headRefName
    }
    return $null
}

function Find-WorktreeForPR {
    param([int]$PRNumber)
    
    $branch = Get-PRBranch -PRNumber $PRNumber
    if (-not $branch) {
        return $null
    }
    
    $worktrees = Get-WorktreeEntries
    $wt = $worktrees | Where-Object { $_.Branch -eq $branch } | Select-Object -First 1
    
    if ($wt) {
        return $wt.Path
    }
    
    # If no dedicated worktree, check if we're on that branch in main repo
    Push-Location $repoRoot
    try {
        $currentBranch = git branch --show-current 2>$null
        if ($currentBranch -eq $branch) {
            return $repoRoot
        }
    }
    finally {
        Pop-Location
    }
    
    return $null
}

function Get-ActiveComments {
    param([int]$PRNumber)
    
    try {
        $comments = gh api "repos/microsoft/PowerToys/pulls/$PRNumber/comments" 2>$null | ConvertFrom-Json
        # Filter to root comments (not replies)
        $rootComments = $comments | Where-Object { $null -eq $_.in_reply_to_id }
        return $rootComments
    }
    catch {
        return @()
    }
}

function Get-UnresolvedThreadCount {
    param([int]$PRNumber)
    
    try {
        $result = gh api graphql -f query="query { repository(owner: `"microsoft`", name: `"PowerToys`") { pullRequest(number: $PRNumber) { reviewThreads(first: 100) { nodes { isResolved } } } } }" 2>$null | ConvertFrom-Json
        $threads = $result.data.repository.pullRequest.reviewThreads.nodes
        $unresolved = $threads | Where-Object { -not $_.isResolved }
        return @($unresolved).Count
    }
    catch {
        return 0
    }
}

#region Main
try {
    Info "=" * 60
    Info "PR FIX - PR #$PRNumber"
    Info "=" * 60
    
    # Get PR info
    $prInfo = gh pr view $PRNumber --json state,headRefName,url 2>$null | ConvertFrom-Json
    if (-not $prInfo) {
        throw "PR #$PRNumber not found"
    }
    
    if ($prInfo.state -ne 'OPEN') {
        Warn "PR #$PRNumber is $($prInfo.state), not OPEN"
        return
    }
    
    Info "PR URL: $($prInfo.url)"
    Info "Branch: $($prInfo.headRefName)"
    Info "CLI: $CLIType"
    
    # Find worktree
    if (-not $WorktreePath) {
        $WorktreePath = Find-WorktreeForPR -PRNumber $PRNumber
    }
    
    if (-not $WorktreePath -or -not (Test-Path $WorktreePath)) {
        Warn "No worktree found for PR #$PRNumber"
        Warn "Using main repo root. Make sure the PR branch is checked out."
        $WorktreePath = $repoRoot
    }
    
    Info "Working directory: $WorktreePath"
    
    # Check for active comments
    $comments = Get-ActiveComments -PRNumber $PRNumber
    $unresolvedCount = Get-UnresolvedThreadCount -PRNumber $PRNumber
    
    Info ""
    Info "Active review comments: $($comments.Count)"
    Info "Unresolved threads: $unresolvedCount"
    
    if ($comments.Count -eq 0 -and $unresolvedCount -eq 0) {
        Success "No active comments or unresolved threads to fix!"
        return @{ PRNumber = $PRNumber; Status = 'NothingToFix' }
    }
    
    if ($DryRun) {
        Info ""
        Warn "[DRY RUN] Would run AI CLI to fix comments"
        Info "Comments to address:"
        foreach ($c in $comments | Select-Object -First 5) {
            Info "  - $($c.path):$($c.line) - $($c.body.Substring(0, [Math]::Min(80, $c.body.Length)))..."
        }
        return @{ PRNumber = $PRNumber; Status = 'DryRun' }
    }
    
    # Confirm
    if (-not $Force) {
        $confirm = Read-Host "Fix $($comments.Count) comments on PR #$PRNumber? (y/N)"
        if ($confirm -notmatch '^[yY]') {
            Info "Cancelled."
            return
        }
    }
    
    # Build prompt
    $prompt = @"
You are fixing review comments on PR #$PRNumber.

Read the active review comments using GitHub tools and address each one:
1. Fetch the PR review comments
2. For each comment, understand what change is requested
3. Make the code changes to address the feedback
4. Build and verify your changes work

Focus on the reviewer's feedback and make targeted fixes.
"@

    # MCP config
    $mcpConfig = '@.github/skills/pr-fix/references/mcp-config.json'
    
    Info ""
    Info "Starting AI fix..."
    
    Push-Location $WorktreePath
    try {
        switch ($CLIType) {
            'copilot' {
                $copilotArgs = @('--additional-mcp-config', $mcpConfig, '-p', $prompt, '--yolo')
                if ($Model) {
                    $copilotArgs += @('--model', $Model)
                }
                $output = & copilot @copilotArgs 2>&1
                # Log output
                $logPath = Join-Path $repoRoot "Generated Files/prReview/$PRNumber"
                if (-not (Test-Path $logPath)) {
                    New-Item -ItemType Directory -Path $logPath -Force | Out-Null
                }
                $output | Out-File -FilePath (Join-Path $logPath "_fix.log") -Force
            }
            'claude' {
                $output = & claude --print --dangerously-skip-permissions --prompt $prompt 2>&1
                $logPath = Join-Path $repoRoot "Generated Files/prReview/$PRNumber"
                if (-not (Test-Path $logPath)) {
                    New-Item -ItemType Directory -Path $logPath -Force | Out-Null
                }
                $output | Out-File -FilePath (Join-Path $logPath "_fix.log") -Force
            }
        }
    }
    finally {
        Pop-Location
    }
    
    # Check results
    $newUnresolvedCount = Get-UnresolvedThreadCount -PRNumber $PRNumber
    
    Info ""
    Info "Fix complete."
    Info "Unresolved threads before: $unresolvedCount"
    Info "Unresolved threads after: $newUnresolvedCount"
    
    if ($newUnresolvedCount -gt 0) {
        Warn ""
        Warn "⚠️ $newUnresolvedCount threads still unresolved."
        Warn "Use VS Code agent to resolve them via GraphQL:"
        Warn "  gh api graphql -f query='mutation { resolveReviewThread(input: {threadId: \"THREAD_ID\"}) { thread { isResolved } } }'"
    }
    else {
        Success "✓ All threads resolved!"
    }
    
    # Write signal file
    $signalDir = Join-Path $repoRoot "Generated Files/prFix/$PRNumber"
    if (-not (Test-Path $signalDir)) { New-Item -ItemType Directory -Path $signalDir -Force | Out-Null }
    @{
        status = if ($newUnresolvedCount -eq 0) { "success" } else { "partial" }
        prNumber = $PRNumber
        timestamp = (Get-Date).ToString("o")
        unresolvedBefore = $unresolvedCount
        unresolvedAfter = $newUnresolvedCount
    } | ConvertTo-Json | Set-Content "$signalDir/.signal" -Force

    return @{
        PRNumber = $PRNumber
        Status = 'FixApplied'
        UnresolvedBefore = $unresolvedCount
        UnresolvedAfter = $newUnresolvedCount
    }
}
catch {
    Err "Error: $($_.Exception.Message)"
    
    # Write failure signal
    $signalDir = Join-Path $repoRoot "Generated Files/prFix/$PRNumber"
    if (-not (Test-Path $signalDir)) { New-Item -ItemType Directory -Path $signalDir -Force | Out-Null }
    @{
        status = "failure"
        prNumber = $PRNumber
        timestamp = (Get-Date).ToString("o")
        error = $_.Exception.Message
    } | ConvertTo-Json | Set-Content "$signalDir/.signal" -Force
    
