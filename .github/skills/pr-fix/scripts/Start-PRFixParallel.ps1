<#
.SYNOPSIS
    Run pr-fix in parallel via the parallel-job-orchestrator skill.

.DESCRIPTION
    Builds one job definition per PR and delegates to the shared
    parallel-job-orchestrator.  Each job invokes Start-PRFix.ps1 for a
    single PR in its worktree.

    DO NOT add [CmdletBinding()], [Parameter(Mandatory)], or [ValidateSet()]
    here — those attributes make the script "advanced" which propagates
    ErrorActionPreference and can crash the orchestrator's monitoring loop.

.PARAMETER PRNumbers
    PR numbers to fix (required).

.PARAMETER MaxConcurrent
    Maximum parallel fix jobs. Default: 3.

.PARAMETER CLIType
    AI CLI type: copilot or claude. Default: copilot.

.PARAMETER Model
    Copilot CLI model to use (e.g., gpt-5.2-codex).

.PARAMETER InactivityTimeoutSeconds
    Kill job if log doesn't grow for this many seconds. Default: 120.

.PARAMETER MaxRetryCount
    Retry attempts after inactivity kill. Default: 2.

.PARAMETER Force
    Skip confirmation prompts in Start-PRFix.ps1.

.EXAMPLE
    ./Start-PRFixParallel.ps1 -PRNumbers 45286, 45287, 45288 -MaxConcurrent 4
#>
param(
    [int[]]$PRNumbers,

    [int]$MaxConcurrent = 3,

    [string]$CLIType = 'copilot',

    [string]$Model,

    [int]$InactivityTimeoutSeconds = 120,

    [int]$MaxRetryCount = 2,

    [switch]$Force
)

$ErrorActionPreference = 'Stop'

# Manual validation
if (-not $PRNumbers -or $PRNumbers.Count -eq 0) {
    Write-Error 'Start-PRFixParallel: -PRNumbers is required.'
    return
}
if ($CLIType -notin 'copilot', 'claude') {
    Write-Error "Start-PRFixParallel: Invalid -CLIType '$CLIType'. Must be 'copilot' or 'claude'."
    return
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot  = Resolve-Path (Join-Path $scriptDir '..\..\..\..')
$fixScript = Join-Path $scriptDir 'Start-PRFix.ps1'
$orchPath  = Join-Path $scriptDir '..\..\parallel-job-orchestrator\scripts\Invoke-SimpleJobOrchestrator.ps1'

if (-not (Test-Path $fixScript)) {
    Write-Error "Start-PRFix.ps1 not found: $fixScript"
    return
}
if (-not (Test-Path $orchPath)) {
    Write-Error "Orchestrator not found: $orchPath"
    return
}

# Output root for logs
$outputRoot = Join-Path $repoRoot 'Generated Files' 'prFix'
if (-not (Test-Path $outputRoot)) {
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
}

# Build job definitions
$jobDefs = @(foreach ($pr in $PRNumbers) {
    # Resolve worktree for this PR
    $branch = $null
    try { $branch = (gh pr view $pr --json headRefName -q .headRefName 2>$null) } catch { }

    $worktree = $null
    if ($branch) {
        $wtLine = git worktree list 2>$null | Select-String $branch | Select-Object -First 1
        if ($wtLine) { $worktree = ($wtLine -split '\s+')[0] }
    }

    if (-not $worktree) {
        Write-Host "[pr-$pr] No worktree found for branch '$branch' — using repo root" -ForegroundColor Yellow
        $worktree = $repoRoot
    }

    $prOutputDir = Join-Path $outputRoot "$pr"
    New-Item -ItemType Directory -Path $prOutputDir -Force | Out-Null
    $logFile = Join-Path $prOutputDir "_fix.log"

    # Build the command arguments for Start-PRFix.ps1
    $fixArgs = @(
        '-File', $fixScript,
        '-PRNumber', $pr,
        '-CLIType', $CLIType,
        '-WorktreePath', $worktree,
        '-Force'
    )
    if ($Model) { $fixArgs += @('-Model', $Model) }

    @{
        Label               = "fix-pr-$pr"
        ExecutionParameters = @{
            JobName    = "fix-pr-$pr"
            Command    = 'pwsh'
            Arguments  = $fixArgs
            WorkingDir = [string]$worktree
            OutputDir  = $prOutputDir
            LogPath    = $logFile
        }
        MonitorFiles = @($logFile)
        CleanupTask  = $null
    }
})

Write-Host "`nBuilt $($jobDefs.Count) fix job(s):" -ForegroundColor Cyan
$jobDefs | ForEach-Object { Write-Host "  $($_.Label)" -ForegroundColor Gray }

# Run via orchestrator
$savedEAP = $ErrorActionPreference
$ErrorActionPreference = 'Continue'

$results = & $orchPath `
    -JobDefinitions $jobDefs `
    -MaxConcurrent $MaxConcurrent `
    -InactivityTimeoutSeconds $InactivityTimeoutSeconds `
    -MaxRetryCount $MaxRetryCount `
    -PollIntervalSeconds 5 `
    -LogDir $outputRoot

$ErrorActionPreference = $savedEAP

# Summary
$succeeded = @($results | Where-Object { $_.Status -eq 'Completed' })
$failed    = @($results | Where-Object { $_.Status -ne 'Completed' })

Write-Host "`n$("=" * 60)" -ForegroundColor Cyan
Write-Host "PR FIX PARALLEL COMPLETE" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host "Total:     $($results.Count)"
Write-Host "Succeeded: $($succeeded.Count)" -ForegroundColor Green
if ($failed.Count -gt 0) {
    Write-Host "Failed:    $($failed.Count)" -ForegroundColor Red
    foreach ($r in $failed) { Write-Host "  $($r.Label) — $($r.Status)" -ForegroundColor Red }
}

$results | Format-Table Label, Status, JobState, ExitCode, RetryCount -AutoSize

return $results
