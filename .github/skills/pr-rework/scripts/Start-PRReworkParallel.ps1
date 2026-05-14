<#
.SYNOPSIS
    Rework multiple PRs in parallel using Start-PRRework.ps1.

.DESCRIPTION
    Accepts a list of PR numbers and runs Start-PRRework.ps1 for each one in
    parallel using ForEach-Object -Parallel. Each PR gets its own worktree,
    state file, and iteration loop.

    Results are collected and displayed as a summary table.

.PARAMETER PRNumbers
    Array of PR numbers to rework.

.PARAMETER CLIType
    AI CLI to use: copilot or claude. Default: copilot.

.PARAMETER Model
    Copilot CLI model override. Default: claude-opus-4.6.

.PARAMETER MaxIterations
    Maximum review/fix loop iterations per PR. Default: 5.

.PARAMETER MinSeverity
    Minimum severity to fix: high, medium, low. Default: medium.

.PARAMETER ThrottleLimit
    Number of PRs to process in parallel. Default: 2.

.PARAMETER ReviewTimeoutMin
    Timeout in minutes for the review CLI call. Default: 10.

.PARAMETER FixTimeoutMin
    Timeout in minutes for the fix CLI call. Default: 15.

.PARAMETER Force
    Skip confirmation prompts.

.PARAMETER Fresh
    Discard previous state and start over for all PRs.

.PARAMETER SkipTests
    Skip the unit test phase after each fix.

.EXAMPLE
    ./Start-PRReworkParallel.ps1 -PRNumbers 45365,45370,45380 -CLIType copilot -Model claude-sonnet-4 -Force

.EXAMPLE
    # Resume with higher parallelism
    ./Start-PRReworkParallel.ps1 -PRNumbers 45365,45370 -ThrottleLimit 3 -Force
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [int[]]$PRNumbers,

    [ValidateSet('copilot', 'claude')]
    [string]$CLIType = 'copilot',

    [string]$Model = 'claude-opus-4.6',

    [int]$MaxIterations = 5,

    [ValidateSet('high', 'medium', 'low')]
    [string]$MinSeverity = 'medium',

    [int]$ThrottleLimit = 2,

    [int]$ReviewTimeoutMin = 10,

    [int]$FixTimeoutMin = 15,

    [switch]$Force,

    [switch]$Fresh,

    [switch]$SkipTests
)

$ErrorActionPreference = 'Continue'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$reworkScript = Join-Path $scriptDir 'Start-PRRework.ps1'

if (-not (Test-Path $reworkScript)) {
    Write-Error "Start-PRRework.ps1 not found at: $reworkScript"
    return
}

$uniquePRs = $PRNumbers | Sort-Object -Unique

Write-Host ""
Write-Host ("=" * 70) -ForegroundColor Cyan
Write-Host " PR REWORK — PARALLEL MODE" -ForegroundColor Cyan
Write-Host " PRs: $($uniquePRs -join ', ')" -ForegroundColor Cyan
Write-Host " Parallelism: $ThrottleLimit" -ForegroundColor Cyan
Write-Host " CLI: $CLIType $(if ($Model) { "(model: $Model)" })" -ForegroundColor Cyan
Write-Host ("=" * 70) -ForegroundColor Cyan
Write-Host ""

# ── Phase 1: Pre-validate PRs and create worktrees sequentially ─────────
# Git worktree operations are NOT safe for concurrent execution — they
# modify .git/worktrees and FETCH_HEAD which causes lock contention.
# We serialize this phase, then parallelize the CLI rework phase.

Write-Host "Phase 1: Validating PRs and creating worktrees sequentially..." -ForegroundColor Cyan
$openPRs = @()
$skippedResults = @()

$repoRoot = git rev-parse --show-toplevel 2>$null
. (Join-Path $scriptDir 'IssueReviewLib.ps1')
$worktreeLib = Join-Path $repoRoot 'tools/build/WorktreeLib.ps1'

foreach ($prNum in $uniquePRs) {
    $prInfo = $null
    try {
        $prInfo = gh pr view $prNum --json state,headRefName,url,title 2>$null | ConvertFrom-Json
    } catch { }
    if (-not $prInfo) {
        Write-Host "  PR #$prNum — NOT FOUND (skipping)" -ForegroundColor Red
        $skippedResults += [PSCustomObject]@{
            PRNumber = $prNum; Status = 'Skipped'; Iterations = 0
            FinalFindings = -1; WorktreePath = ''; SummaryPath = ''
            Error = 'PR not found'
        }
        continue
    }
    if ($prInfo.state -ne 'OPEN') {
        Write-Host "  PR #$prNum — $($prInfo.state) (skipping)" -ForegroundColor DarkGray
        $skippedResults += [PSCustomObject]@{
            PRNumber = $prNum; Status = 'Skipped'; Iterations = 0
            FinalFindings = -1; WorktreePath = ''; SummaryPath = ''
            Error = "PR is $($prInfo.state), not OPEN"
        }
        continue
    }

    # Create worktree sequentially to avoid git lock contention.
    # We inline the git commands instead of calling New-WorktreeFromBranch.ps1
    # because that script: (1) calls `code --new-window` which opens unwanted
    # VS Code windows, and (2) has `exit 1` in its catch block which can
    # terminate callers unpredictably depending on invocation method.
    $branch = $prInfo.headRefName
    $currentBranch = git branch --show-current 2>$null
    if ($currentBranch -ne $branch) {
        . $worktreeLib
        $existingWt = Get-WorktreeEntries | Where-Object { $_.Branch -eq $branch } | Select-Object -First 1
        if ($existingWt) {
            Write-Host "  PR #$prNum — reusing worktree at $($existingWt.Path)" -ForegroundColor DarkCyan
        } else {
            Write-Host "  PR #$prNum — creating worktree for $branch..." -ForegroundColor White
            try {
                # Ensure local tracking branch exists
                git show-ref --verify --quiet "refs/heads/$branch"
                if ($LASTEXITCODE -ne 0) {
                    git fetch origin "$branch" 2>&1 | Out-Null
                    git branch --track $branch "origin/$branch" 2>&1 | Out-Null
                    if ($LASTEXITCODE -ne 0) { throw "Failed to create tracking branch '$branch'" }
                }
                # Create the worktree using WorktreeLib naming convention
                $safeBranch = ($branch -replace '[\\/:*?"<>|]','-')
                $hash = Get-ShortHashFromString -Text $safeBranch
                $folderName = "$(Split-Path -Leaf $repoRoot)-$hash"
                $base = Get-WorktreeBasePath -RepoRoot $repoRoot
                $folder = Join-Path $base $folderName
                if (Test-Path $folder) {
                    # Orphaned directory from a previous failed run — remove it
                    Write-Host "  PR #$prNum — removing orphaned directory $folder" -ForegroundColor Yellow
                    Remove-Item $folder -Recurse -Force -ErrorAction SilentlyContinue
                    git worktree prune 2>$null
                    if (Test-Path $folder) {
                        # Still locked — use an alternate path with timestamp suffix
                        $ts = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
                        $folder = Join-Path $base "$folderName-$ts"
                        Write-Host "  PR #$prNum — orphan locked, using $folder" -ForegroundColor Yellow
                    }
                }
                $wtAddOutput = git worktree add $folder $branch 2>&1
                if ($LASTEXITCODE -ne 0) { throw "git worktree add failed for '$branch' (exit $LASTEXITCODE): $wtAddOutput" }
                # Skip submodule init here — Start-PRRework.ps1's own
                # Get-OrCreateWorktree handles it, and calling it here can
                # crash the process due to git stderr interaction with pwsh.
                Write-Host "  PR #$prNum — worktree created at $folder" -ForegroundColor DarkCyan
            }
            catch {
                Write-Host "  PR #$prNum — worktree creation FAILED: $($_.Exception.Message)" -ForegroundColor Red
                $skippedResults += [PSCustomObject]@{
                    PRNumber = $prNum; Status = 'Failed'; Iterations = 0
                    FinalFindings = -1; WorktreePath = ''; SummaryPath = ''
                    Error = "Worktree creation failed: $($_.Exception.Message)"
                }
                continue
            }
        }
    }

    $openPRs += $prNum
    Write-Host "  PR #$prNum — $($prInfo.title)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Phase 1 complete: $($openPRs.Count) open PRs ready, $($skippedResults.Count) skipped" -ForegroundColor Cyan

if ($openPRs.Count -eq 0) {
    Write-Host "No open PRs to process." -ForegroundColor Yellow
    return $skippedResults
}

# ── Phase 2: Run CLI rework in parallel ─────────────────────────────────
Write-Host ""
Write-Host "Phase 2: Running CLI rework in parallel (ThrottleLimit=$ThrottleLimit)..." -ForegroundColor Cyan
Write-Host ""

$startTime = Get-Date

$parallelResults = $openPRs | ForEach-Object -ThrottleLimit $ThrottleLimit -Parallel {
    $prNum = $_
    $script = $using:reworkScript
    $cli = $using:CLIType
    $mdl = $using:Model
    $maxIter = $using:MaxIterations
    $minSev = $using:MinSeverity
    $rvTimeout = $using:ReviewTimeoutMin
    $fxTimeout = $using:FixTimeoutMin
    $doForce = $using:Force
    $doFresh = $using:Fresh
    $doSkipTests = $using:SkipTests

    try {
        $params = @{
            PRNumber         = $prNum
            CLIType          = $cli
            MaxIterations    = $maxIter
            MinSeverity      = $minSev
            ReviewTimeoutMin = $rvTimeout
            FixTimeoutMin    = $fxTimeout
        }
        if ($mdl) { $params['Model'] = $mdl }
        if ($doForce) { $params['Force'] = $true }
        if ($doFresh) { $params['Fresh'] = $true }
        if ($doSkipTests) { $params['SkipTests'] = $true }

        $result = & $script @params
        $result
    }
    catch {
        [PSCustomObject]@{
            PRNumber      = $prNum
            Status        = 'Failed'
            Iterations    = 0
            FinalFindings = -1
            WorktreePath  = ''
            SummaryPath   = ''
            Error         = $_.Exception.Message
        }
    }
}

$elapsed = (Get-Date) - $startTime

# Merge skipped + parallel results
$results = @($skippedResults) + @($parallelResults) | Sort-Object PRNumber

Write-Host ""
Write-Host ("=" * 70) -ForegroundColor Cyan
Write-Host " PR REWORK PARALLEL — RESULTS" -ForegroundColor Cyan
Write-Host ("=" * 70) -ForegroundColor Cyan
Write-Host ""
Write-Host "Elapsed: $($elapsed.ToString('hh\:mm\:ss'))"
Write-Host ""

# Display results table
$results | Format-Table @(
    @{Label = 'PR'; Expression = { $_.PRNumber }; Width = 8}
    @{Label = 'Status'; Expression = { $_.Status }; Width = 15}
    @{Label = 'Iters'; Expression = { $_.Iterations }; Width = 6}
    @{Label = 'Findings'; Expression = { $_.FinalFindings }; Width = 10}
    @{Label = 'Worktree'; Expression = { $_.WorktreePath }; Width = 50}
) -AutoSize

# Summary stats
$clean = ($results | Where-Object Status -eq 'Clean').Count
$maxed = ($results | Where-Object Status -eq 'MaxIterations').Count
$failed = ($results | Where-Object Status -eq 'Failed').Count
$skipped = ($results | Where-Object Status -eq 'Skipped').Count

Write-Host ""
Write-Host "Summary: $clean clean, $maxed max-iterations, $failed failed, $skipped skipped (of $($uniquePRs.Count) total)" -ForegroundColor $(if ($failed -gt 0) { 'Yellow' } elseif ($maxed -gt 0) { 'DarkYellow' } else { 'Green' })
Write-Host ""

if ($clean -gt 0) {
    Write-Host "Clean PRs — ready for review and push:" -ForegroundColor Green
    $results | Where-Object Status -eq 'Clean' | ForEach-Object {
        Write-Host "  PR #$($_.PRNumber): $($_.SummaryPath)" -ForegroundColor White
    }
}
if ($maxed -gt 0) {
    Write-Host "Max-iteration PRs — review summaries for remaining findings:" -ForegroundColor Yellow
    $results | Where-Object Status -eq 'MaxIterations' | ForEach-Object {
        Write-Host "  PR #$($_.PRNumber): $($_.FinalFindings) findings remaining — $($_.SummaryPath)" -ForegroundColor White
    }
}

return $results
