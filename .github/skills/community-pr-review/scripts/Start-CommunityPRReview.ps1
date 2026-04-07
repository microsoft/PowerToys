<#
.SYNOPSIS
    Orchestrate a full community PR review: code review + build verification + verification guide.

.DESCRIPTION
    Runs the ReviewCommunityPR agent workflow for a given PR number.
    Spawns Copilot CLI or Claude CLI with the review prompt and monitors completion.

.PARAMETER PRNumber
    The PR number to review (required).

.PARAMETER CLIType
    AI CLI to use: copilot or claude. Default: copilot.

.PARAMETER Model
    Model override for Copilot CLI.

.PARAMETER SkipBuild
    Skip the build verification phase.

.PARAMETER OutputRoot
    Root folder for review outputs. Default: Generated Files/communityPrReview

.PARAMETER Force
    Re-review PRs that already have completed reviews.

.PARAMETER DryRun
    Show what would be done without executing.

.EXAMPLE
    ./Start-CommunityPRReview.ps1 -PRNumber 45234

.EXAMPLE
    ./Start-CommunityPRReview.ps1 -PRNumber 45234 -CLIType claude -SkipBuild

.EXAMPLE
    ./Start-CommunityPRReview.ps1 -PRNumber 45234 -Force
#>
param(
    [int]$PRNumber,
    [string]$CLIType = 'copilot',
    [string]$Model,
    [int]$MaxIterations = 3,
    [switch]$SkipBuild,
    [string]$OutputRoot = 'Generated Files/communityPrReview',
    [string]$LogPath,
    [switch]$Force,
    [switch]$DryRun,
    [switch]$Help
)

$ErrorActionPreference = 'Stop'

if ($Help) {
    Get-Help $MyInvocation.MyCommand.Path -Full
    return
}

if (-not $PRNumber -or $PRNumber -eq 0) {
    Write-Error 'Start-CommunityPRReview: -PRNumber is required.'
    return
}

if ($CLIType -notin 'copilot', 'claude') {
    Write-Error "Start-CommunityPRReview: Invalid -CLIType '$CLIType'. Must be 'copilot' or 'claude'."
    return
}

# Load helpers
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. "$scriptDir/ReviewLib.ps1"

$repoRoot = Get-RepoRoot

# Resolve config directory name (.github or .claude) from script location
$_cfgDir = if ($PSScriptRoot -match '[\\/](\.github|\.claude)[\\/]') { $Matches[1] } else { '.github' }

# ── Setup logging ────────────────────────────────────────────────────────

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path (Get-Location) 'Start-CommunityPRReview.log'
}
$logDir = Split-Path -Parent $LogPath
if (-not [string]::IsNullOrWhiteSpace($logDir) -and -not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

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

# ── Resolve output directory ─────────────────────────────────────────────

$reviewRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot
} else {
    Join-Path $repoRoot $OutputRoot
}

$reviewDir = Join-Path $reviewRoot "$PRNumber"

# Check for existing review
if (-not $Force -and (Test-Path (Join-Path $reviewDir 'review-comments.md'))) {
    Info "PR #$PRNumber already has a completed review at $reviewDir"
    Info "Use -Force to re-review."
    return
}

if (-not (Test-Path $reviewDir)) {
    New-Item -ItemType Directory -Path $reviewDir -Force | Out-Null
}

# ── Prepare prompt ───────────────────────────────────────────────────────

Info "=" * 80
Info "Community PR Review — PR #$PRNumber"
Info "=" * 80
Info "Repository root: $repoRoot"
Info "Output directory: $reviewDir"
Info "CLI type: $CLIType"
Info "Max iterations: $MaxIterations"
Info "Skip build: $SkipBuild"

$reviewPromptPath = Join-Path $repoRoot "$_cfgDir/skills/community-pr-review/references/review-community-pr.prompt.md"
$reviewDirForPrompt = ($reviewDir -replace '\\', '/')

if (Test-Path $reviewPromptPath) {
    $rawPrompt = Get-Content $reviewPromptPath -Raw
    $rawPrompt = $rawPrompt -replace '\{\{pr_number\}\}', [string]$PRNumber
    $rawPrompt = $rawPrompt -replace '\{\{iteration\}\}', '1'
    $rawPrompt = $rawPrompt -replace 'Generated Files/communityPrReview/\{\{pr_number\}\}', $reviewDirForPrompt
}
else {
    Warn "Review prompt not found at $reviewPromptPath, using inline prompt."
    $rawPrompt = @"
Review community bug-fix PR #$PRNumber with a review-fix loop (max $MaxIterations iterations).
1. Fetch PR data and linked issue. Record original head SHA.
2. Checkout and build. If build fails, try merging main.
3. Review-fix loop: review 7 dimensions, fix high/medium issues, re-review until clean.
4. Generate suggested-changes.md with GitHub suggestion blocks from diff.
5. Write build-report.md and verification-guide.md.
6. Write .signal file.
Output to: $reviewDirForPrompt/
"@
}

$skipBuildNote = if ($SkipBuild) { "`n`nIMPORTANT: Skip the build verification phase. Set buildStatus to 'skipped' in the signal file." } else { '' }
$loopNote = "`n`nReview-fix loop: max $MaxIterations iterations. Exit when no high/medium findings remain or max iterations reached."

$prompt = @"
You are running a community PR review workflow for PR #$PRNumber.
Execute the workflow below exactly and write all outputs to $reviewDirForPrompt/.
$skipBuildNote$loopNote

$rawPrompt
"@

$flatPrompt = ($prompt -replace "[\r\n]+", ' ').Trim()

if ($DryRun) {
    Info "`nDry run — would execute:"
    Info "  CLI: $CLIType"
    Info "  Agent: ReviewCommunityPR"
    Info "  Output: $reviewDir"
    Info "`nPrompt (first 500 chars):"
    Info $flatPrompt.Substring(0, [Math]::Min($flatPrompt.Length, 500))
    return
}

# Write in-progress signal
Write-Signal -OutputDir $reviewDir -Data @{
    status   = 'in-progress'
    prNumber = $PRNumber
}

# ── Execute CLI ──────────────────────────────────────────────────────────

$logFile = Join-Path $reviewDir "_review.log"

if ($CLIType -eq 'copilot') {
    $copilotExe = (Get-Command copilot -ErrorAction SilentlyContinue).Source
    if (-not $copilotExe) { $copilotExe = 'copilot' }

    $cliArgs = @('-p', $flatPrompt, '--yolo', '--no-custom-instructions', '--agent', 'ReviewCommunityPR')
    if ($Model) { $cliArgs += @('--model', $Model) }

    Info "`nLaunching Copilot CLI..."
    Info "Log file: $logFile"

    & $copilotExe @cliArgs 2>&1 | Tee-Object -FilePath $logFile
    $exitCode = $LASTEXITCODE
}
else {
    $cliArgs = @('-p', $flatPrompt, '--dangerously-skip-permissions', '--agent', 'ReviewCommunityPR')

    Info "`nLaunching Claude CLI..."
    Info "Log file: $logFile"

    & claude @cliArgs 2>&1 | Tee-Object -FilePath $logFile
    $exitCode = $LASTEXITCODE
}

# ── Finalize ─────────────────────────────────────────────────────────────

$hasReview = Test-Path (Join-Path $reviewDir 'review-comments.md')
$hasBuild = Test-Path (Join-Path $reviewDir 'build-report.md')
$hasVerification = Test-Path (Join-Path $reviewDir 'verification-guide.md')
$hasSuggestions = Test-Path (Join-Path $reviewDir 'suggested-changes.md')
$hasFixSummary = Test-Path (Join-Path $reviewDir 'fix-summary.md')

$status = if ($hasReview -and ($hasBuild -or $SkipBuild) -and $hasVerification) {
    'success'
} elseif ($hasReview) {
    'partial'
} else {
    'failure'
}

Write-Signal -OutputDir $reviewDir -Data @{
    status          = $status
    prNumber        = $PRNumber
    exitCode        = $exitCode
    hasReview       = $hasReview
    hasBuild        = $hasBuild
    hasVerification = $hasVerification
    hasSuggestions  = $hasSuggestions
    hasFixSummary   = $hasFixSummary
}

Info "`n$("=" * 80)"
Info "COMMUNITY PR REVIEW COMPLETE"
Info "=" * 80
Info "PR:              #$PRNumber"
Info "Status:          $status"
Info "Review comments: $(if ($hasReview) { 'YES' } else { 'NO' })"
Info "Suggested changes: $(if ($hasSuggestions) { 'YES' } else { 'NO' })"
Info "Fix summary:     $(if ($hasFixSummary) { 'YES' } else { 'NO' })"
Info "Build report:    $(if ($hasBuild) { 'YES' } else { 'NO' })"
Info "Verification:    $(if ($hasVerification) { 'YES' } else { 'NO' })"
Info "Output:          $reviewDir"
Info "=" * 80
