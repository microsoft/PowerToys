<#
.SYNOPSIS
    Inspect a pr-triage run and report progress without modifying anything.

.DESCRIPTION
    Reads the result files on disk for a given run date and reports:
      - Which tasks (PRs) have been started / completed / failed / timed out
      - Per-PR enrichment progress and log file status
      - Aggregate counts and overall pipeline status
      - Heartbeat and log-file liveness for running tasks

    This script is safe to call at any time — it only reads, never writes.
    Other skills or humans can call this to decide whether to wait, resume, or kill.

.PARAMETER RunDate
    Date folder to inspect (YYYY-MM-DD).  Default: today.

.PARAMETER RunRoot
    Override the run root directory.  Default: Generated Files/pr-triage/<RunDate>

.PARAMETER Detailed
    Show per-step status for every task.

.PARAMETER AsJson
    Output machine-readable JSON instead of human-readable text.

.EXAMPLE
    .\Get-TriageProgress.ps1
    Shows progress for today's run.

.EXAMPLE
    .\Get-TriageProgress.ps1 -RunDate 2026-02-10 -Detailed
    Shows per-step progress for the 2026-02-10 run.

.EXAMPLE
    .\Get-TriageProgress.ps1 -AsJson | ConvertFrom-Json
    Returns structured progress data.
#>
[CmdletBinding()]
param(
    [string]$RunDate,
    [string]$RunRoot,
    [string]$ReviewOutputRoot = 'Generated Files/prReview',
    [switch]$Detailed,
    [switch]$AsJson
)

$ErrorActionPreference = 'Stop'

# Load TaskRunner library
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptDir 'TaskRunner.ps1')

# Resolve paths
$repoRoot = git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) { $repoRoot = (Get-Location).Path }

$resolvedReviewOutputRoot = if ([System.IO.Path]::IsPathRooted($ReviewOutputRoot)) {
    $ReviewOutputRoot
} else {
    Join-Path $repoRoot $ReviewOutputRoot
}

if (-not $RunDate) { $RunDate = (Get-Date).ToString('yyyy-MM-dd') }
if (-not $RunRoot) { $RunRoot = Join-Path $repoRoot "Generated Files/pr-triage/$RunDate" }

if (-not (Test-Path $RunRoot)) {
    if ($AsJson) {
        @{ Error = "No run found at $RunRoot" } | ConvertTo-Json
    } else {
        Write-Host "No triage run found at: $RunRoot" -ForegroundColor Yellow
    }
    return
}

# Step definitions match the orchestrator

# ── Scan global output files ───────────────────────────────────────────────

$globalFiles = @(
    @{ Name = 'all-prs.json';         Path = Join-Path $RunRoot 'all-prs.json' }
    @{ Name = 'categorized-prs.json';  Path = Join-Path $RunRoot 'categorized-prs.json' }
    @{ Name = 'summary.md';            Path = Join-Path $RunRoot 'summary.md' }
)

# ── Scan review task folders (Step 2 — reviews) ──────────────────────────────

$reviewRunRoot = Join-Path $RunRoot 'reviews'
$reviewDirs = Get-ChildItem -Path $reviewRunRoot -Directory -ErrorAction SilentlyContinue
$prReviewRoot = $resolvedReviewOutputRoot

$reviewSummaries = @()
if ($reviewDirs) {
    foreach ($d in $reviewDirs) {
        $dir = $d.FullName
        $prNum = $d.Name
        $completed  = Test-Path (Join-Path $dir '.completed')
        $failed     = Test-Path (Join-Path $dir '.failed')
        $timedOut   = Test-Path (Join-Path $dir '.timeout')
        $terminal   = $completed -or $failed -or $timedOut
        $logSummary = Get-TaskLogSummary -TaskDir $dir
        $hbAlive    = Test-HeartbeatAlive -TaskDir $dir
        $alive      = (-not $terminal) -and ($hbAlive -or $logSummary.LogAlive)

        # Check actual review output (step files in prReview/<PR>/)
        $reviewOutDir = Join-Path $prReviewRoot $prNum
        $stepFileCount = 0
        $hasOverview = $false
        $hasSignal = $false
        $signalStatus = $null
        $signalCompletedCount = 0
        $signalSkippedCount = 0
        $signalLastStep = $null
        if (Test-Path $reviewOutDir) {
            $stepFiles = Get-ChildItem -Path $reviewOutDir -Filter '*.md' -ErrorAction SilentlyContinue
            $stepFileCount = ($stepFiles | Where-Object { $_.Name -match '^\d{2}-' }).Count
            $hasOverview = Test-Path (Join-Path $reviewOutDir '00-OVERVIEW.md')
            $signalPath = Join-Path $reviewOutDir '.signal'
            $hasSignal = Test-Path $signalPath
            if ($hasSignal) {
                try {
                    $sig = Get-Content $signalPath -Raw | ConvertFrom-Json
                    $signalStatus = $sig.status
                    $signalCompletedCount = @($sig.completedSteps).Count
                    $signalSkippedCount = @($sig.skippedSteps).Count
                    $signalLastStep = $sig.lastStep
                } catch { }
            }
        }

        $reviewSummaries += [PSCustomObject]@{
            PR             = $prNum
            Completed      = $completed
            Failed         = $failed
            TimedOut       = $timedOut
            Alive          = $alive
            LogAlive       = (-not $terminal) -and $logSummary.LogAlive
            LogCount       = $logSummary.LogCount
            LatestLog      = $logSummary.LatestLog
            LogSizeKB      = $logSummary.LatestSizeKB
            StepFiles      = $stepFileCount
            HasOverview    = $hasOverview
            HasSignal      = $hasSignal
            SignalStatus   = $signalStatus
            SignalStepsDone = $signalCompletedCount
            SignalStepsSkip = $signalSkippedCount
            SignalLastStep = $signalLastStep
        }
    }
}

$reviewCompleted = ($reviewSummaries | Where-Object { $_.Completed }).Count
$reviewFailed    = ($reviewSummaries | Where-Object { $_.Failed }).Count
$reviewTimedOut  = ($reviewSummaries | Where-Object { $_.TimedOut }).Count
$reviewAlive     = ($reviewSummaries | Where-Object { $_.Alive }).Count
$reviewTotal     = $reviewSummaries.Count
$hasReviewStep   = $reviewTotal -gt 0

# Count PRs from all-prs.json for overall totals
$allPrsFile = Join-Path $RunRoot 'all-prs.json'
$totalPRs = 0
if (Test-Path $allPrsFile) {
    try {
        $allPrsData = Get-Content $allPrsFile -Raw | ConvertFrom-Json
        $totalPRs = $allPrsData.TotalCount
    } catch { }
}

$globalStatus = [ordered]@{}
foreach ($gf in $globalFiles) {
    $globalStatus[$gf.Name] = (Test-Path $gf.Path) -and ((Get-Item $gf.Path -ErrorAction SilentlyContinue).Length -gt 0)
}

# ── Build result ────────────────────────────────────────────────────────────

$result = [PSCustomObject]@{
    RunDate        = $RunDate
    RunRoot        = $RunRoot
    TotalPRs       = $totalPRs
    GlobalFiles    = $globalStatus
    HasReviewStep  = $hasReviewStep
    Reviews        = if ($hasReviewStep) {
        [PSCustomObject]@{
            Total     = $reviewTotal
            Completed = $reviewCompleted
            Failed    = $reviewFailed
            TimedOut  = $reviewTimedOut
            Running   = $reviewAlive
            Pending   = $reviewTotal - $reviewCompleted - $reviewFailed - $reviewTimedOut - $reviewAlive
        }
    } else { $null }
    AllDone        = ($globalStatus.Values | Where-Object { -not $_ }).Count -eq 0 -and
                     $totalPRs -gt 0 -and
                     (-not $hasReviewStep -or $reviewCompleted -eq $reviewTotal)
}

if ($Detailed) {
    if ($hasReviewStep) {
        $result | Add-Member -NotePropertyName 'ReviewTasks' -NotePropertyValue $reviewSummaries
    }
}

# ── Output ──────────────────────────────────────────────────────────────────

if ($AsJson) {
    $result | ConvertTo-Json -Depth 5
    return
}

# Human-readable output
Write-Host ''
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  PR Triage Progress — $RunDate" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ''

$pct = if ($totalPRs -gt 0) { [math]::Round(($completedPRs / $totalPRs) * 100) } else { 0 }
Write-Host "  PR enrichment: $completedPRs/$totalPRs ($pct%)" -ForegroundColor $(if ($pct -eq 100) { 'Green' } else { 'Yellow' })

if ($failedPRs -gt 0)   { Write-Host "    Failed:     $failedPRs"    -ForegroundColor Red }
if ($timedOutPRs -gt 0) { Write-Host "    Timed out:  $timedOutPRs"  -ForegroundColor Red }
if ($alivePRs -gt 0)    { Write-Host "    Running:    $alivePRs"     -ForegroundColor Cyan }
if ($logAlivePRs -gt 0) { Write-Host "    Log active: $logAlivePRs (CLI producing output)" -ForegroundColor Cyan }
Write-Host "    Log files:  $totalLogFiles total across all tasks" -ForegroundColor Gray

Write-Host ''
Write-Host '  Global files:' -ForegroundColor Gray
foreach ($gf in $globalFiles) {
    $exists = $globalStatus[$gf.Name]
    $icon = if ($exists) { '✓' } else { '○' }
    $color = if ($exists) { 'Green' } else { 'DarkGray' }
    Write-Host "    $icon $($gf.Name)" -ForegroundColor $color
}

if ($Detailed -and $hasReviewStep) {
    Write-Host ''
    $reviewPct = if ($reviewTotal -gt 0) { [math]::Round(($reviewCompleted / $reviewTotal) * 100) } else { 0 }
    Write-Host "  PR reviews (Step 2): $reviewCompleted/$reviewTotal ($reviewPct%)" -ForegroundColor $(if ($reviewPct -eq 100) { 'Green' } else { 'Yellow' })

    if ($reviewFailed -gt 0)   { Write-Host "    Failed:     $reviewFailed"    -ForegroundColor Red }
    if ($reviewTimedOut -gt 0) { Write-Host "    Timed out:  $reviewTimedOut"  -ForegroundColor Red }
    if ($reviewAlive -gt 0)    { Write-Host "    Running:    $reviewAlive"     -ForegroundColor Cyan }

    if ($Detailed -and $reviewSummaries.Count -gt 0) {
        Write-Host ''
        Write-Host '  Per-PR detail (reviews):' -ForegroundColor Gray
        foreach ($rs in $reviewSummaries | Sort-Object PR) {
            $icon = if ($rs.Completed) { '✓' } elseif ($rs.Failed) { '✗' } elseif ($rs.Alive) { '⟳' } else { '○' }
            $color = if ($rs.Completed) { 'Green' } elseif ($rs.Failed) { 'Red' } elseif ($rs.Alive) { 'Cyan' } else { 'DarkGray' }
            $stepInfo = if ($rs.StepFiles -gt 0) { " [$($rs.StepFiles) step files]" } else { '' }
            $signalInfo = if ($rs.HasSignal) {
                $done = $rs.SignalStepsDone
                $skip = $rs.SignalStepsSkip
                $last = if ($rs.SignalLastStep) { " → $($rs.SignalLastStep)" } else { '' }
                " ✔signal($($rs.SignalStatus): ${done}done/${skip}skip${last})"
            } else { '' }
            $logInfo = ''
            if ($rs.LogCount -gt 0) {
                $logAliveTag = if ($rs.LogAlive) { ' ✉️ active' } else { '' }
                $logInfo = "  [log: $($rs.LatestLog) $($rs.LogSizeKB)KB$logAliveTag]"
            }
            Write-Host "    $icon PR #$($rs.PR)$stepInfo$signalInfo$logInfo" -ForegroundColor $color
        }
    }
}

Write-Host ''
if ($result.AllDone) {
    Write-Host "  ✅ Triage run complete!  Open summary.md to review." -ForegroundColor Green
} else {
    Write-Host "  ⏳ Run in progress or incomplete.  Re-run orchestrator to resume." -ForegroundColor Yellow
}
Write-Host ''
