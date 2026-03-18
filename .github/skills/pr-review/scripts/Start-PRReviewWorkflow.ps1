<#
.SYNOPSIS
    Review PRs using GitHub Copilot CLI or Claude CLI via the parallel job orchestrator.

.DESCRIPTION
    For each specified PR, builds a review prompt and delegates execution to the
    parallel-job-orchestrator skill for queuing, monitoring, retry, and cleanup.
    Completed reviews are skipped by default (resumable). Use -Force to re-review.

.PARAMETER PRNumbers
    Array of PR numbers to review (required).

.PARAMETER CLIType
    AI CLI to use: copilot or claude. Default: copilot.

.PARAMETER Model
    Copilot CLI model to use (e.g., gpt-5.2-codex).

.PARAMETER MinSeverity
    Minimum severity to post as PR comments: high, medium, low, info. Default: medium.

.PARAMETER MaxConcurrent
    Maximum concurrent review jobs. Default: 4.

.PARAMETER InactivityTimeoutSeconds
    Kill the CLI process if log file doesn't grow for this many seconds. Default: 60.

.PARAMETER MaxRetryCount
    Number of retry attempts after inactivity kill. Default: 3.

.PARAMETER PromptMode
    Prompt style: workflow (full review-pr.prompt.md) or minimal. Default: workflow.

.PARAMETER Force
    Re-review PRs that already have completed reviews (00-OVERVIEW.md).

.PARAMETER DryRun
    Show what would be done without executing.

.EXAMPLE
    # Review a single PR with copilot
    ./Start-PRReviewWorkflow.ps1 -PRNumbers 45234

.EXAMPLE
    # Review multiple PRs in parallel with claude
    ./Start-PRReviewWorkflow.ps1 -PRNumbers 45234, 45235, 45236 -CLIType claude -MaxConcurrent 4

.EXAMPLE
    # Re-review completed PRs
    ./Start-PRReviewWorkflow.ps1 -PRNumbers 45234 -Force

.NOTES
    Prerequisites:
    - GitHub CLI (gh) authenticated
    - Copilot CLI or Claude CLI installed
    - Uses parallel-job-orchestrator skill for execution
#>
# NOTE: Do NOT use [CmdletBinding()], [Parameter(Mandatory)], [ValidateSet()]
# or any attribute here. These make the script "advanced" which propagates
# ErrorActionPreference through PS7's plumbing and can silently kill the
# orchestrator's monitoring loop in a child scope.
param(
    [int[]]$PRNumbers,

    [string]$CLIType = 'copilot',

    [string]$Model,

    [string]$MinSeverity = 'medium',

    [int]$MaxConcurrent = 20,

    [int]$InactivityTimeoutSeconds = 120,

    [int]$MaxRetryCount = 3,

    [int]$PollIntervalSeconds = 5,

    [string]$PromptMode = 'workflow',

    [switch]$DisableMcpConfig,

    [string]$OutputRoot = 'Generated Files/prReview',

    [string]$LogPath,

    [switch]$DryRun,

    [switch]$Force,

    [switch]$Help
)

$ErrorActionPreference = 'Stop'

# Manual validation (replacing [Parameter(Mandatory)] and [ValidateSet()])
if (-not $PRNumbers -or $PRNumbers.Count -eq 0) {
    Write-Error 'Start-PRReviewWorkflow: -PRNumbers is required.'
    return
}
if ($CLIType -notin 'copilot', 'claude') {
    Write-Error "Start-PRReviewWorkflow: Invalid -CLIType '$CLIType'. Must be 'copilot' or 'claude'."
    return
}
if ($MinSeverity -notin 'high', 'medium', 'low', 'info') {
    Write-Error "Start-PRReviewWorkflow: Invalid -MinSeverity '$MinSeverity'. Must be 'high', 'medium', 'low', or 'info'."
    return
}
if ($PromptMode -notin 'workflow', 'minimal') {
    Write-Error "Start-PRReviewWorkflow: Invalid -PromptMode '$PromptMode'. Must be 'workflow' or 'minimal'."
    return
}

# ── logging ──────────────────────────────────────────────────────────────

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path (Get-Location) 'Start-PRReviewWorkflow.log'
}
$logDir = Split-Path -Parent $LogPath
if (-not [string]::IsNullOrWhiteSpace($logDir) -and -not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}
"[$(Get-Date -Format o)] Starting Start-PRReviewWorkflow" | Out-File -FilePath $LogPath -Encoding utf8 -Append

function Write-LogHost {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [object[]]$Object,
        [object]$ForegroundColor,
        [object]$BackgroundColor,
        [switch]$NoNewline,
        [Object]$Separator
    )
    $message = [string]::Join(' ', ($Object | ForEach-Object { [string]$_ }))
    "[$(Get-Date -Format o)] $message" | Out-File -FilePath $LogPath -Encoding utf8 -Append
    $invokeParams = @{}
    if ($PSBoundParameters.ContainsKey('ForegroundColor') -and -not [string]::IsNullOrWhiteSpace([string]$ForegroundColor)) { $invokeParams.ForegroundColor = $ForegroundColor }
    if ($PSBoundParameters.ContainsKey('BackgroundColor') -and -not [string]::IsNullOrWhiteSpace([string]$BackgroundColor)) { $invokeParams.BackgroundColor = $BackgroundColor }
    if ($NoNewline) { $invokeParams.NoNewline = $true }
    if ($PSBoundParameters.ContainsKey('Separator')) { $invokeParams.Separator = $Separator }
    Microsoft.PowerShell.Utility\Write-Host @invokeParams -Object $message
}
Set-Alias -Name Write-Host -Value Write-LogHost -Scope Script -Force

# Load libraries
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. "$scriptDir/IssueReviewLib.ps1"

$repoRoot = Get-RepoRoot

# Resolve config directory name (.github or .claude) from script location
$_cfgDir = if ($PSScriptRoot -match '[\\/](\.github|\.claude)[\\/]') { $Matches[1] } else { '.github' }

$reviewRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot
} else {
    Join-Path $repoRoot $OutputRoot
}
if (-not (Test-Path $reviewRoot)) {
    New-Item -ItemType Directory -Path $reviewRoot -Force | Out-Null
}

if ($Help) {
    Get-Help $MyInvocation.MyCommand.Path -Full
    return
}

# ── helpers ──────────────────────────────────────────────────────────────

function Test-ReviewExists {
    param([int]$PRNumber)
    $reviewPath = Join-Path $reviewRoot "$PRNumber/00-OVERVIEW.md"
    return Test-Path $reviewPath
}

function Get-CopilotExecutablePath {
    $copilotCmd = Get-Command copilot -ErrorAction SilentlyContinue
    if (-not $copilotCmd) { return 'copilot' }
    if ($copilotCmd.Source -match '\.ps1$') {
        $bootstrapDir = Split-Path $copilotCmd.Source -Parent
        $savedPath = $env:PATH
        $env:PATH = ($env:PATH -split ';' | Where-Object { $_ -ne $bootstrapDir }) -join ';'
        try {
            $realCmd = Get-Command copilot -ErrorAction SilentlyContinue
            if ($realCmd) { return $realCmd.Source }
        }
        finally { $env:PATH = $savedPath }
    }
    return $copilotCmd.Source
}

function New-ReviewPrompt {
    param(
        [int]$PRNumber,
        [string]$ReviewDir,
        [string]$MinSev,
        [string]$Mode
    )

    $reviewPathForPrompt = ($ReviewDir -replace '\\', '/')

    if ($Mode -eq 'minimal') {
        return @"
Review PR #$PRNumber and write outputs directly into $reviewPathForPrompt/.
Create 00-OVERVIEW.md and all step files 01-functionality.md through 13-copilot-guidance.md.
Do not execute unsupported CLI flags.
"@
    }

    $reviewPromptPath = Join-Path $repoRoot "$_cfgDir/skills/pr-review/references/review-pr.prompt.md"
    if (Test-Path $reviewPromptPath) {
        $rawWorkflowPrompt = Get-Content $reviewPromptPath -Raw
        $rawWorkflowPrompt = $rawWorkflowPrompt -replace '\{\{pr_number\}\}', [string]$PRNumber
        $rawWorkflowPrompt = $rawWorkflowPrompt -replace "Generated Files/prReview/$PRNumber", $reviewPathForPrompt
        $rawWorkflowPrompt = $rawWorkflowPrompt -replace 'Generated Files/prReview/\{\{pr_number\}\}', $reviewPathForPrompt
        return @"
You are running an automated pull-request review workflow for PR #$PRNumber.
Execute the workflow below exactly and write outputs to $reviewPathForPrompt/.

$rawWorkflowPrompt
"@
    }

    return @"
Follow exactly what at $_cfgDir/skills/pr-review/references/review-pr.prompt.md to do with PR #$PRNumber.
Post findings with severity >= $MinSev as PR review comments via GitHub MCP.
"@
}

# ── build job definitions ────────────────────────────────────────────────

Info "Repository root: $repoRoot"
Info "Review output root: $reviewRoot"
Info "CLI type: $CLIType"
Info "Max concurrent: $MaxConcurrent"
Info "Min severity for comments: $MinSeverity"
Info "Inactivity timeout (seconds): $InactivityTimeoutSeconds"
Info "Max retry count: $MaxRetryCount"
Info "Prompt mode: $PromptMode"

# Build PR list, skip completed reviews
$prsToProcess = @($PRNumbers | Where-Object { $_ } |
    ForEach-Object { [int]$_ } | Sort-Object -Unique)

if (-not $Force -and $prsToProcess.Count -gt 0) {
    $beforeCount = $prsToProcess.Count
    $prsToProcess = @($prsToProcess | Where-Object { -not (Test-ReviewExists -PRNumber $_) })
    $skippedCount = $beforeCount - $prsToProcess.Count
    if ($skippedCount -gt 0) {
        Info "Skipped $skippedCount PRs with existing reviews (use -Force to redo)"
    }
}

if ($prsToProcess.Count -eq 0) {
    Warn "No PRs to review."
    return
}

Info "`nPRs to review:"
Info ("-" * 80)
foreach ($pr in $prsToProcess) {
    Info ("  #{0,-6} https://github.com/microsoft/PowerToys/pull/{0}" -f $pr)
}
Info ("-" * 80)

if ($DryRun) {
    Warn "`nDry run mode - no reviews will be executed."
    return
}

# Resolve CLI executable
$copilotExe = if ($CLIType -eq 'copilot') { Get-CopilotExecutablePath } else { $null }
$mcpConfigPath = Join-Path $repoRoot "$_cfgDir/skills/pr-review/references/mcp-config.json"

$jobDefs = @(foreach ($pr in $prsToProcess) {
    $reviewPath = Join-Path $reviewRoot "$pr"
    $prompt = New-ReviewPrompt -PRNumber $pr -ReviewDir $reviewPath -MinSev $MinSeverity -Mode $PromptMode
    $flatPrompt = ($prompt -replace "[\r\n]+", ' ').Trim()

    # Write initial in-progress .signal
    New-Item -ItemType Directory -Path $reviewPath -Force | Out-Null
    $now = (Get-Date).ToString("o")
    $signalPath = Join-Path $reviewPath '.signal'
    [ordered]@{
        status         = "in-progress"
        prNumber       = $pr
        totalSteps     = 13
        completedSteps = @()
        skippedSteps   = @()
        lastStep       = $null
        lastUpdated    = $now
        startedAt      = $now
    } | ConvertTo-Json -Depth 3 | Set-Content $signalPath -Force

    if ($CLIType -eq 'copilot') {
        $cliArgs = @('-p', $flatPrompt, '--yolo', '--no-custom-instructions', '--agent', 'ReviewPR')
        if (-not $DisableMcpConfig) {
            $cliArgs = @('--additional-mcp-config', "@$mcpConfigPath") + $cliArgs
        }
        if ($Model) { $cliArgs += @('--model', $Model) }
        $logFile = Join-Path $reviewPath "_copilot-review.log"

        @{
            Label               = "copilot-pr-$pr"
            ExecutionParameters = @{
                JobName    = "copilot-pr-$pr"
                Command    = $copilotExe
                Arguments  = $cliArgs
                WorkingDir = $repoRoot
                OutputDir  = $reviewPath
                LogPath    = $logFile
            }
            MonitorFiles = @($logFile)
            CleanupTask  = $null
        }
    }
    else {
        $debugFile = Join-Path $reviewPath "_claude-debug.log"
        $logFile   = Join-Path $reviewPath "_claude-review.log"
        $cliArgs = @('-p', $flatPrompt, '--dangerously-skip-permissions', '--agent', 'ReviewPR',
            '--debug', 'all', '--debug-file', $debugFile)

        @{
            Label               = "claude-pr-$pr"
            ExecutionParameters = @{
                JobName    = "claude-pr-$pr"
                Command    = 'claude'
                Arguments  = $cliArgs
                WorkingDir = $repoRoot
                OutputDir  = $reviewPath
                LogPath    = $logFile
            }
            MonitorFiles = @($debugFile)
            CleanupTask  = {
                param($Tracker)
                $outDir = $Tracker.ExecutionParameters.OutputDir
                $dbg = Join-Path $outDir '_claude-debug.log'
                if (Test-Path $dbg) {
                    $fi = [System.IO.FileInfo]::new($dbg)
                    if ($fi.Length -gt 0) {
                        $sizeMB = [math]::Round($fi.Length / 1MB, 1)
                        Remove-Item $dbg -Force
                        Write-Host "[$($Tracker.Label)] Cleaned debug log (${sizeMB} MB)"
                    }
                }
                # Claude CLI auto-creates a 0-byte 'latest' marker file — remove it.
                $latest = Join-Path $outDir 'latest'
                if (Test-Path $latest) { Remove-Item $latest -Force }
            }
        }
    }
})

Info "`nBuilt $($jobDefs.Count) job definition(s):"
$jobDefs | ForEach-Object { Info "  $($_.Label)" }

# ── run orchestrator ─────────────────────────────────────────────────────

$orchestratorPath = Join-Path $scriptDir '..\..\parallel-job-orchestrator\scripts\Invoke-SimpleJobOrchestrator.ps1'

# CRITICAL: Lower ErrorActionPreference before calling the orchestrator.
# The orchestrator must run under 'Continue' so its monitoring loop survives
# transient errors.
$savedEAP = $ErrorActionPreference
$ErrorActionPreference = 'Continue'

$results = & $orchestratorPath `
    -JobDefinitions $jobDefs `
    -MaxConcurrent $MaxConcurrent `
    -InactivityTimeoutSeconds $InactivityTimeoutSeconds `
    -MaxRetryCount $MaxRetryCount `
    -PollIntervalSeconds $PollIntervalSeconds `
    -LogDir $reviewRoot

$ErrorActionPreference = $savedEAP

# ── process results ──────────────────────────────────────────────────────

# A job is only truly successful if it completed AND produced review output.
# The orchestrator marks jobs as 'Completed' when the process exits, but
# a fast crash (e.g. model unavailable) also exits cleanly with exit code 1.
$succeeded = @($results | Where-Object {
    if ($_.Status -ne 'Completed') { return $false }
    if ($_.Label -notmatch '(\d+)$') { return $false }
    $prDir = Join-Path $reviewRoot $Matches[1]
    $overview = Join-Path $prDir '00-OVERVIEW.md'
    return (Test-Path $overview)
})
$failed = @($results | Where-Object {
    $_ -notin $succeeded
})

# Write final signal files
foreach ($r in $results) {
    # Extract PR number from label (e.g. "copilot-pr-45601" → 45601)
    if ($r.Label -notmatch '(\d+)$') { continue }
    $prNum = [int]$Matches[1]
    $prDir = Join-Path $reviewRoot "$prNum"
    $signalPath = Join-Path $prDir '.signal'
    $isFailed = $r -in $failed

    # Discover completed step files
    $stepFiles = @()
    $skippedSteps = @()
    if (Test-Path $prDir) {
        $mdFiles = Get-ChildItem -Path $prDir -Filter '*.md' -ErrorAction SilentlyContinue
        $stepFiles = @($mdFiles | Where-Object { $_.Name -match '^\d{2}-' } |
            ForEach-Object { $_.BaseName })
        $overviewPath = Join-Path $prDir '00-OVERVIEW.md'
        if (Test-Path $overviewPath) {
            $overviewText = Get-Content $overviewPath -Raw -ErrorAction SilentlyContinue
            $skippedSteps = @([regex]::Matches($overviewText, '(\d{2}-[\w-]+)\s*.*Skipped') |
                ForEach-Object { $_.Groups[1].Value })
        }
    }

    $now = (Get-Date).ToString("o")
    $startedAt = $now
    if (Test-Path $signalPath) {
        try {
            $existing = Get-Content $signalPath -Raw | ConvertFrom-Json
            if ($existing.startedAt) { $startedAt = $existing.startedAt }
        } catch { }
    }

    [ordered]@{
        status         = if ($isFailed) { "failure" } else { "success" }
        prNumber       = $prNum
        totalSteps     = 13
        completedSteps = $stepFiles
        skippedSteps   = $skippedSteps
        lastStep       = if ($stepFiles.Count -gt 0) { $stepFiles[-1] } else { $null }
        lastUpdated    = $now
        startedAt      = $startedAt
        timestamp      = $now
        retryCount     = $r.RetryCount
    } | ConvertTo-Json -Depth 3 | Set-Content $signalPath -Force
}

# ── summary ──────────────────────────────────────────────────────────────

Info "`n$("=" * 80)"
Info "PR REVIEW COMPLETE"
Info ("=" * 80)
Info "Total jobs:      $($results.Count)"

if ($succeeded.Count -gt 0) {
    Success "Succeeded:       $($succeeded.Count)"
    foreach ($r in $succeeded) { Success "  $($r.Label) (retries: $($r.RetryCount))" }
}

if ($failed.Count -gt 0) {
    Err "Had issues:      $($failed.Count)"
    foreach ($r in $failed) { Err "  $($r.Label) — status: $($r.Status), retries: $($r.RetryCount)" }
}

Info "`nReview files location: $OutputRoot/<PR_NUMBER>/"
Info "Absolute output path: $reviewRoot"

# Display per-job results table
$results | Format-Table Label, Status, JobState, ExitCode, RetryCount, LogPath -AutoSize

Info ("=" * 80)

return $results
