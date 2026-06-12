<#
.SYNOPSIS
    Orchestrate a full PR triage run: collect → review → categorize → report.
    Fully resumable — skips any step whose output file already exists.

.DESCRIPTION
    This is the main entry point for the pr-triage skill.  It:

    1. Collects all open PRs via Get-OpenPrs.ps1 → all-prs.json
    2. Runs detailed AI reviews via the pr-review skill → prReview/<N>/
    3. AI enrichment via Invoke-AiEnrichment.ps1 → ai-enrichment.json
    4. Categorizes via Invoke-PrCategorization.ps1 → categorized-prs.json
    5. Generates summary.md and per-category reports via Export-TriageReport.ps1

    Resume:  Just re-run the same command.  The orchestrator checks which output
    files exist on disk and skips completed work.  No external state JSON needed.

    Progress:  While this is running (or after a crash), call:
        .\Get-TriageProgress.ps1 [-Detailed] [-AsJson]
    to see exactly where things stand.

.PARAMETER Repository
    GitHub repository in owner/repo format.  Default: microsoft/PowerToys

.PARAMETER PRNumbers
    PR numbers to triage. Required.

.PARAMETER MaxConcurrent
    Max parallel review / enrichment jobs.  Default: 20.

.PARAMETER TimeoutMin
    Per-job timeout in minutes (used by review step).  Default: 5.

.PARAMETER RunDate
    Date folder name (YYYY-MM-DD).  Default: today.

.PARAMETER Force
    Re-run all steps even if output files exist.

.EXAMPLE
    .\Start-PrTriage.ps1 -PRNumbers 45234,45235
    Run full triage for specific PRs.

.EXAMPLE
    # Resume after crash
    .\Start-PrTriage.ps1
    # It auto-detects completed steps and picks up where it left off.
#>
# NOTE: Do NOT use [CmdletBinding()], [Parameter(Mandatory)], or [ValidateSet()]
# here.  These make the script "advanced" which propagates ErrorActionPreference
# through PS7's plumbing and can silently crash child scope monitoring loops.
param(
    [string]$Repository = 'microsoft/PowerToys',
    [int[]]$PRNumbers,
    [int]$MaxConcurrent = 20,
    [int]$TimeoutMin = 5,
    [string]$RunDate,
    [string]$CLIType = 'copilot',
    [string]$RunLabel,
    [string]$OutputRoot = 'Generated Files/pr-triage',
    [string]$ReviewOutputRoot = 'Generated Files/prReview',
    [string]$LogPath,
    [switch]$Force,
    [switch]$SkipAiEnrichment,
    [switch]$SkipReview
)

# Use 'Stop' so any gh-cli or JSON error is immediately caught.
$ErrorActionPreference = 'Stop'

# Manual validation (replacing [Parameter(Mandatory)] and [ValidateSet()])
if (-not $PRNumbers -or $PRNumbers.Count -eq 0) {
    Write-Error 'Start-PrTriage: -PRNumbers is required.'
    return
}
if ($CLIType -notin 'copilot', 'claude') {
    Write-Error "Start-PrTriage: Invalid -CLIType '$CLIType'. Must be 'copilot' or 'claude'."
    return
}

# ── Load libraries ──────────────────────────────────────────────────────────

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ── Resolve paths ───────────────────────────────────────────────────────────

$repoRoot = git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) { $repoRoot = (Get-Location).Path }

$resolvedReviewOutputRoot = if ([System.IO.Path]::IsPathRooted($ReviewOutputRoot)) {
    $ReviewOutputRoot
} else {
    Join-Path $repoRoot $ReviewOutputRoot
}

$resolvedOutputRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot
} else {
    Join-Path $repoRoot $OutputRoot
}

if (-not $RunDate) { $RunDate = (Get-Date).ToString('yyyy-MM-dd') }
if (-not $RunLabel) { $RunLabel = $CLIType }

$triageRoot = $resolvedOutputRoot
$runFolder  = Join-Path $RunDate $RunLabel
$runRoot    = Join-Path $triageRoot $runFolder
$cacheDir   = Join-Path $triageRoot '__cache'

foreach ($d in @($runRoot, $cacheDir)) {
    if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
}

if (-not $LogPath) { $LogPath = Join-Path $runRoot 'triage.log' }
if (-not [System.IO.Path]::IsPathRooted($LogPath)) { $LogPath = Join-Path $runRoot $LogPath }
$logDir = Split-Path -Parent $LogPath
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }

function Write-LogHost {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [object[]]$Object,
        [object]$ForegroundColor,
        [object]$BackgroundColor,
        [switch]$NoNewline,
        [object]$Separator = ' '
    )

    $message = if ($null -eq $Object) { '' } else { ($Object -join [string]$Separator) }
    Add-Content -Path $LogPath -Value ("[{0}] {1}" -f (Get-Date).ToString('o'), $message)

    $writeParams = @{}
    if ($PSBoundParameters.ContainsKey('Object')) { $writeParams.Object = $Object }
    if ($PSBoundParameters.ContainsKey('ForegroundColor') -and -not [string]::IsNullOrWhiteSpace([string]$ForegroundColor)) { $writeParams.ForegroundColor = $ForegroundColor }
    if ($PSBoundParameters.ContainsKey('BackgroundColor') -and -not [string]::IsNullOrWhiteSpace([string]$BackgroundColor)) { $writeParams.BackgroundColor = $BackgroundColor }
    if ($PSBoundParameters.ContainsKey('NoNewline')) { $writeParams.NoNewline = $NoNewline }
    if ($PSBoundParameters.ContainsKey('Separator')) { $writeParams.Separator = $Separator }
    Microsoft.PowerShell.Utility\Write-Host @writeParams
}

$allPrsFile       = Join-Path $runRoot 'all-prs.json'
$aiCatFile        = Join-Path $runRoot 'ai-enrichment.json'
$categorizedFile  = Join-Path $runRoot 'categorized-prs.json'
$summaryFile      = Join-Path $runRoot 'summary.md'

$totalSteps = 5

Write-LogHost ''
Write-LogHost '═══════════════════════════════════════════════════════════════' -ForegroundColor Cyan
Write-LogHost "  PR Triage — $RunDate" -ForegroundColor Cyan
Write-LogHost "  Repository: $Repository" -ForegroundColor Cyan
Write-LogHost "  PRs: $($PRNumbers -join ', ')" -ForegroundColor Cyan
Write-LogHost "  AI Engine: $CLIType" -ForegroundColor Cyan
Write-LogHost "  Run Label: $RunLabel" -ForegroundColor Cyan
Write-LogHost "  Triage Root: $triageRoot" -ForegroundColor Cyan
Write-LogHost "  Review Root: $resolvedReviewOutputRoot" -ForegroundColor Cyan
Write-LogHost "  Log File: $LogPath" -ForegroundColor Cyan
Write-LogHost '  Pipeline: Collect → Review → AI Enrich → Categorize → Report' -ForegroundColor Cyan
Write-LogHost '═══════════════════════════════════════════════════════════════' -ForegroundColor Cyan
Write-LogHost ''

$batchStart = Get-Date

# ═══════════════════════════════════════════════════════════════════════════
# STEP 1 — Collect open PRs
# ═══════════════════════════════════════════════════════════════════════════

$step1Done = (Test-Path $allPrsFile) -and ((Get-Item $allPrsFile).Length -gt 0) -and -not $Force

if ($step1Done) {
    Write-LogHost "[1/$totalSteps] Collection — already done (all-prs.json exists)" -ForegroundColor Green
    $allPrsData = Get-Content $allPrsFile -Raw | ConvertFrom-Json
} else {
    Write-LogHost "[1/$totalSteps] Collecting selected PRs..." -ForegroundColor Yellow

    $collectParams = @{
        Repository = $Repository
        PRNumbers  = $PRNumbers
        OutputPath = $allPrsFile
        LogPath    = (Join-Path $runRoot 'step1-collection.log')
    }

    & (Join-Path $scriptDir 'Get-OpenPrs.ps1') @collectParams

    if (-not (Test-Path $allPrsFile)) {
        Write-LogHost 'Collection failed — all-prs.json not created.' -ForegroundColor Red
        return
    }
    $allPrsData = Get-Content $allPrsFile -Raw | ConvertFrom-Json
    Write-LogHost "  Collected $($allPrsData.TotalCount) PRs" -ForegroundColor Green
}

$prNumbers = $allPrsData.Prs | ForEach-Object { $_.Number }

# ═══════════════════════════════════════════════════════════════════════════
# STEP 2 — Detailed PR reviews via pr-review skill
# ═══════════════════════════════════════════════════════════════════════════
#
# Run reviews immediately after collection so their output is available
# for categorization and reporting. Delegates to the pr-review skill's
# Start-PRReviewWorkflow.ps1 in the current PowerShell context.
# Output goes to  Generated Files/prReview/<PR>/  as usual.
# ═══════════════════════════════════════════════════════════════════════════

Write-LogHost ''
Write-LogHost "[2/$totalSteps] Running detailed PR reviews..." -ForegroundColor Yellow

$reviewWorkflow = Join-Path $repoRoot '.github' 'skills' 'pr-review' 'scripts' 'Start-PRReviewWorkflow.ps1'

if (-not (Test-Path $reviewWorkflow)) {
    Write-LogHost "pr-review skill not found — expected: $reviewWorkflow" -ForegroundColor Red
    return
}

$reviewPrNumbers = @($allPrsData.Prs | ForEach-Object { [int]$_.Number })

if ($reviewPrNumbers.Count -eq 0) {
    Write-LogHost '[2] No PRs to review' -ForegroundColor Green
} elseif ($SkipReview) {
    Write-LogHost '  Review step skipped (-SkipReview)' -ForegroundColor Yellow
} else {
    Write-LogHost "  PRs to review: $($reviewPrNumbers.Count)" -ForegroundColor Gray
    Write-LogHost "  PR numbers: $($reviewPrNumbers -join ', ')" -ForegroundColor Gray

    $reviewLogFile = Join-Path $runRoot 'step2-review.log'

    # Check if all PRs already have completed reviews (skip condition)
    $reviewOutDir = $resolvedReviewOutputRoot
    $alreadyReviewed = 0
    foreach ($n in $reviewPrNumbers) {
        $overview = Join-Path $reviewOutDir $n.ToString() '00-OVERVIEW.md'
        if (Test-Path $overview) { $alreadyReviewed++ }
    }

    if ($alreadyReviewed -eq $reviewPrNumbers.Count -and -not $Force) {
        Write-LogHost "  All $alreadyReviewed/$($reviewPrNumbers.Count) PRs already reviewed — skipping" -ForegroundColor Green
    } else {
        $reviewParams = @{
            PRNumbers  = $reviewPrNumbers
            CLIType    = $CLIType
            OutputRoot = $resolvedReviewOutputRoot
            MaxConcurrent = $MaxConcurrent
            InactivityTimeoutSeconds = [Math]::Max(($TimeoutMin * 60), 60)
            LogPath    = $reviewLogFile
        }
        if ($Force) { $reviewParams.Force = $true }

        try {
            & $reviewWorkflow @reviewParams
        } catch {
            Write-LogHost "  Review step failed (non-fatal — continuing): $($_.Exception.Message)" -ForegroundColor Yellow
        }

        $completedReviews = 0
        foreach ($n in $reviewPrNumbers) {
            $overview = Join-Path $reviewOutDir $n.ToString() '00-OVERVIEW.md'
            if (Test-Path $overview) { $completedReviews++ }
        }
        Write-LogHost "  Reviews: $completedReviews/$($reviewPrNumbers.Count) completed" -ForegroundColor $(if ($completedReviews -eq $reviewPrNumbers.Count) { 'Green' } else { 'Yellow' })
    }
}

# ═══════════════════════════════════════════════════════════════════════════
# STEP 3 — AI Enrichment (sequential AI CLI per PR)
# ═══════════════════════════════════════════════════════════════════════════
#
# Each PR gets its own AI CLI invocation that reads:
#   - PR metadata and AI code review findings (from Step 2)
#   - Full discussion comments via gh CLI
#   - Images and attachments via GitHub MCP tools
# This enriches each PR with 7 dimension scores and context-aware signals.
# Actual category assignment happens in Step 4.
# ═══════════════════════════════════════════════════════════════════════════

$step3Done = (Test-Path $aiCatFile) -and ((Get-Item $aiCatFile).Length -gt 0) -and -not $Force

if ($SkipAiEnrichment) {
    Write-LogHost ''
    Write-LogHost "[3/$totalSteps] AI Enrichment — skipped (-SkipAiEnrichment)" -ForegroundColor Yellow
} elseif ($step3Done) {
    Write-LogHost ''
    Write-LogHost "[3/$totalSteps] AI Enrichment — already done (ai-enrichment.json exists)" -ForegroundColor Green
} else {
    Write-LogHost ''
    Write-LogHost "[3/$totalSteps] AI Enrichment ($CLIType) — reading discussions + images per PR..." -ForegroundColor Yellow

    $aiCatScript = Join-Path $scriptDir 'Invoke-AiEnrichment.ps1'
    if (-not (Test-Path $aiCatScript)) {
        Write-LogHost "  AI enrichment script not found: $aiCatScript" -ForegroundColor Red
        Write-LogHost "  Falling back to rule-based categorization only" -ForegroundColor Yellow
    } else {
        $aiCatParams = @{
            InputPath  = $allPrsFile
            OutputPath = $aiCatFile
            Repository = $Repository
            TimeoutMin = $TimeoutMin
            CLIType    = $CLIType
            OutputRoot = $runRoot
            ReviewOutputRoot = $resolvedReviewOutputRoot
            LogPath = (Join-Path $runRoot 'step3-ai-enrichment.log')
            MaxConcurrent = $MaxConcurrent
        }
        if ($Force) { $aiCatParams.Force = $true }

        & $aiCatScript @aiCatParams

        if (Test-Path $aiCatFile) {
            $aiCatData = Get-Content $aiCatFile -Raw | ConvertFrom-Json
    Write-LogHost "  AI enriched: $($aiCatData.AiSuccessCount) PRs" -ForegroundColor Green
            if ($aiCatData.AiFailedCount -gt 0) {
                Write-LogHost "  AI failed (will use rule fallback): $($aiCatData.AiFailedCount) PRs" -ForegroundColor Yellow
            }
        } else {
    Write-LogHost "  AI enrichment did not produce output (non-fatal — Step 4 uses rules only)" -ForegroundColor Yellow
        }
    }
}

# ═══════════════════════════════════════════════════════════════════════════
# STEP 4 — Categorization
# ═══════════════════════════════════════════════════════════════════════════
#
# Enriches PRs via GitHub API (reviews, CI, activity timestamps), merges
# with AI enrichment from Step 3, and assigns final triage categories.
# PRs with AI dimensions use dimension rules; others get rule-based fallback.
# ═══════════════════════════════════════════════════════════════════════════

$step4Done = (Test-Path $categorizedFile) -and ((Get-Item $categorizedFile).Length -gt 0) -and -not $Force

if ($step4Done) {
    Write-LogHost ''
    Write-LogHost "[4/$totalSteps] Categorization — already done (categorized-prs.json exists)" -ForegroundColor Green
} else {
    Write-LogHost ''
    Write-LogHost "[4/$totalSteps] Categorizing PRs (parallel, max-concurrent: $MaxConcurrent)..." -ForegroundColor Yellow

    $catParams = @{
        InputPath     = $allPrsFile
        OutputPath    = $categorizedFile
        Repository    = $Repository
        ThrottleLimit = $MaxConcurrent
        ReviewOutputRoot = $resolvedReviewOutputRoot
        LogPath       = (Join-Path $runRoot 'step4-categorization.log')
    }

    # Pass AI enrichment results if available
    if ((Test-Path $aiCatFile) -and -not $SkipAiEnrichment) {
        $catParams.AiEnrichmentPath = $aiCatFile
    }

    & (Join-Path $scriptDir 'Invoke-PrCategorization.ps1') @catParams

    if (-not (Test-Path $categorizedFile)) {
        Write-LogHost '  [ERROR] Categorization failed — categorized-prs.json not created.' -ForegroundColor Red
        return
    }

    Write-LogHost '  Categorization complete' -ForegroundColor Green
}

# ═══════════════════════════════════════════════════════════════════════════
# STEP 5 — Generate reports (summary.md + per-category .md)
# ═══════════════════════════════════════════════════════════════════════════

$step5Done = (Test-Path $summaryFile) -and ((Get-Item $summaryFile).Length -gt 0) -and -not $Force

if ($step5Done) {
    Write-LogHost ''
    Write-LogHost "[5/$totalSteps] Reporting — already done (summary.md exists)" -ForegroundColor Green
} else {
    Write-LogHost ''
    Write-LogHost "[5/$totalSteps] Generating reports..." -ForegroundColor Yellow

    $reportParams = @{
        InputPath             = $categorizedFile
        OutputDir             = $runRoot
        Repository            = $Repository
        IncludeDetailedReview = $true
        LogPath               = (Join-Path $runRoot 'step5-reporting.log')
    }

    # Find previous run for delta comparison
    $prevRun = Get-ChildItem $triageRoot -Directory |
        Where-Object { $_.Name -match '^\d{4}-\d{2}-\d{2}$' -and $_.Name -lt $RunDate } |
        Sort-Object Name -Descending | Select-Object -First 1
    if ($prevRun) {
        $prevCatFile = Join-Path $prevRun.FullName 'categorized-prs.json'
        if (Test-Path $prevCatFile) {
            $reportParams.PreviousInputPath = $prevCatFile
            Write-LogHost "  Comparing against previous run: $($prevRun.Name)" -ForegroundColor Gray
        }
    }

    & (Join-Path $scriptDir 'Export-TriageReport.ps1') @reportParams

    Write-LogHost '  Reports generated' -ForegroundColor Green
}

# ═══════════════════════════════════════════════════════════════════════════
# Done
# ═══════════════════════════════════════════════════════════════════════════

$totalElapsed = (Get-Date) - $batchStart

Write-LogHost ''
Write-LogHost '═══════════════════════════════════════════════════════════════' -ForegroundColor Cyan
Write-LogHost '  Triage complete!' -ForegroundColor Green
Write-LogHost "  Duration: $([math]::Round($totalElapsed.TotalMinutes, 1)) minutes" -ForegroundColor Cyan
Write-LogHost "  Reports:  $runRoot" -ForegroundColor Cyan
Write-LogHost '  Start with: summary.md' -ForegroundColor Cyan
Write-LogHost ''
Write-LogHost '  Check progress any time:' -ForegroundColor Gray
Write-LogHost "    .\Get-TriageProgress.ps1 -RunDate $RunDate -Detailed" -ForegroundColor Gray
Write-LogHost '═══════════════════════════════════════════════════════════════' -ForegroundColor Cyan
Write-LogHost ''
