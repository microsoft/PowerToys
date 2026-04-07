<#
.SYNOPSIS
    Build a PR branch and attempt to fix build failures.

.DESCRIPTION
    Checks out a PR branch, initializes submodules, and builds the project.
    On failure, tries merging with latest main and rebuilding.
    Records all actions taken in a build report.

.PARAMETER PRNumber
    The PR number to build (required).

.PARAMETER OutputDir
    Directory to write the build report. Default: Generated Files/communityPrReview/<PR>

.PARAMETER SkipCheckout
    Skip PR checkout (assume already on the right branch).

.PARAMETER Help
    Show help.

.EXAMPLE
    ./Build-PRBranch.ps1 -PRNumber 45234

.EXAMPLE
    ./Build-PRBranch.ps1 -PRNumber 45234 -SkipCheckout
#>
param(
    [int]$PRNumber,
    [string]$OutputDir,
    [switch]$SkipCheckout,
    [switch]$Help
)

$ErrorActionPreference = 'Stop'

if ($Help) {
    Get-Help $MyInvocation.MyCommand.Path -Full
    return
}

if (-not $PRNumber -or $PRNumber -eq 0) {
    Write-Error 'Build-PRBranch: -PRNumber is required.'
    return
}

# Load helpers
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. "$scriptDir/ReviewLib.ps1"

$repoRoot = Get-RepoRoot

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "Generated Files/communityPrReview/$PRNumber"
}
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# Track all actions for the build report
$actions = [System.Collections.Generic.List[string]]::new()
$buildStatus = 'unknown'
$buildErrors = ''
$headSha = ''
$headBranch = ''
$baseBranch = ''

function Add-Action {
    param([string]$Action, [string]$Result)
    $entry = "$(Get-Date -Format 'HH:mm:ss') | $Action — $Result"
    $actions.Add($entry)
    Info $entry
}

# ── Phase 1: Checkout ────────────────────────────────────────────────────

if (-not $SkipCheckout) {
    Info "Checking out PR #$PRNumber..."
    try {
        $checkoutOutput = & gh pr checkout $PRNumber 2>&1
        Add-Action "gh pr checkout $PRNumber" "Success"
    }
    catch {
        Add-Action "gh pr checkout $PRNumber" "Failed: $_"
        Err "Failed to checkout PR #$PRNumber : $_"
        $buildStatus = 'checkout_failed'
    }
}
else {
    Add-Action "Checkout" "Skipped (--SkipCheckout)"
}

# Capture branch info
try {
    $headBranch = (git rev-parse --abbrev-ref HEAD 2>$null).Trim()
    $headSha = (git rev-parse HEAD 2>$null).Trim()
    $prJson = & gh pr view $PRNumber --json baseRefName 2>$null | ConvertFrom-Json
    $baseBranch = $prJson.baseRefName
}
catch {
    Warn "Could not fetch branch metadata: $_"
}

if ($buildStatus -eq 'checkout_failed') {
    # Write failure report and exit
    $report = @"
# Build Report — PR #$PRNumber

## Build Status: CHECKOUT_FAILED

## Environment
- PR: #$PRNumber
- Build date: $(Get-Date -Format 'o')

## Build Steps
$($actions | ForEach-Object { "1. $_" } | Out-String)

## Suggestions for Author
- Ensure the PR branch is up to date and force-pushable
- Check if the fork still exists
"@
    $report | Set-Content (Join-Path $OutputDir 'build-report.md') -Force
    return @{ Status = 'checkout_failed'; Actions = $actions }
}

# ── Phase 2: Submodules ─────────────────────────────────────────────────

Info "Initializing submodules..."
try {
    & git submodule update --init --recursive 2>&1 | Out-Null
    Add-Action "git submodule update --init --recursive" "Success"
}
catch {
    Add-Action "git submodule update --init --recursive" "Warning: $_"
    Warn "Submodule init warning: $_"
}

# ── Phase 3: Build ──────────────────────────────────────────────────────

function Invoke-Build {
    param([string]$Label)

    Info "Running build ($Label)..."

    # First run build-essentials for NuGet restore
    $essentialsCmd = Join-Path $repoRoot 'tools/build/build-essentials.cmd'
    if (Test-Path $essentialsCmd) {
        Info "Running build-essentials.cmd..."
        $essResult = & cmd /c "`"$essentialsCmd`"" 2>&1
        $essExit = $LASTEXITCODE
        Add-Action "build-essentials.cmd ($Label)" "Exit code: $essExit"
    }

    # Then run the main build
    $buildCmd = Join-Path $repoRoot 'tools/build/build.cmd'
    if (-not (Test-Path $buildCmd)) {
        Add-Action "build.cmd ($Label)" "NOT FOUND at $buildCmd"
        return $false
    }

    $buildResult = & cmd /c "`"$buildCmd`"" 2>&1
    $buildExit = $LASTEXITCODE
    Add-Action "build.cmd ($Label)" "Exit code: $buildExit"

    if ($buildExit -eq 0) {
        return $true
    }

    # Read error log
    $errorLog = Get-BuildErrorLog -BuildDir $repoRoot
    if ($errorLog -and (Test-Path $errorLog)) {
        $script:buildErrors = Get-Content $errorLog -Raw -ErrorAction SilentlyContinue
        Add-Action "Read error log" "$errorLog"
    }
    else {
        $script:buildErrors = ($buildResult | Out-String)
        Add-Action "Read error log" "No .errors.log found, captured stdout/stderr"
    }

    return $false
}

# First build attempt
$buildOk = Invoke-Build -Label 'initial'

if ($buildOk) {
    $buildStatus = 'success'
    Success "Build succeeded on first attempt."
}
else {
    Warn "Initial build failed. Trying merge with main..."

    # ── Phase 4: Merge main and retry ────────────────────────────────────

    try {
        & git fetch origin main 2>&1 | Out-Null
        Add-Action "git fetch origin main" "Success"

        $mergeOutput = & git merge origin/main --no-edit 2>&1
        $mergeExit = $LASTEXITCODE
        Add-Action "git merge origin/main" "Exit code: $mergeExit"

        if ($mergeExit -ne 0) {
            # Merge conflicts
            & git merge --abort 2>$null
            Add-Action "git merge --abort" "Merge had conflicts, aborted"
            $buildStatus = 'merge_conflict'
        }
        else {
            # Retry build after merge
            $buildOk = Invoke-Build -Label 'after-merge'
            if ($buildOk) {
                $buildStatus = 'success_after_merge'
                Success "Build succeeded after merging main."
            }
            else {
                $buildStatus = 'failure'
                Err "Build still fails after merging main."
            }
        }
    }
    catch {
        Add-Action "Merge main attempt" "Exception: $_"
        $buildStatus = 'failure'
    }
}

# ── Phase 5: Write Build Report ─────────────────────────────────────────

$statusDisplay = switch ($buildStatus) {
    'success'             { 'SUCCESS' }
    'success_after_merge' { 'SUCCESS_AFTER_MERGE' }
    'failure'             { 'FAILURE' }
    'merge_conflict'      { 'FAILURE (merge conflicts with main)' }
    default               { 'UNKNOWN' }
}

$stepsText = ($actions | ForEach-Object { "- $_" }) -join "`n"

$fixActions = @($actions | Where-Object { $_ -match 'merge|essentials|after-merge' })
$fixActionsText = if ($fixActions.Count -gt 0) {
    ($fixActions | ForEach-Object { "- $_" }) -join "`n"
} else {
    "None — build succeeded on first attempt."
}

$errorsSection = if ($buildStatus -in 'failure', 'merge_conflict') {
    @"

## Remaining Build Errors
``````
$($buildErrors.Substring(0, [Math]::Min($buildErrors.Length, 5000)))
``````
"@
} else { '' }

$suggestionsSection = switch ($buildStatus) {
    'success_after_merge' {
        @"

## Suggestions for Author
- The PR builds successfully after merging with latest main
- Please merge the latest main branch into your PR branch: ``git merge origin/main``
- This will resolve build issues caused by the PR being behind main
"@
    }
    'failure' {
        @"

## Suggestions for Author
- The build fails even after merging with latest main
- Please review the build errors above and fix them
- Run ``tools\build\build-essentials.cmd`` followed by ``tools\build\build.cmd`` locally to verify
"@
    }
    'merge_conflict' {
        @"

## Suggestions for Author
- The PR has merge conflicts with the latest main branch
- Please resolve merge conflicts by running: ``git fetch origin main && git merge origin/main``
- After resolving conflicts, verify the build passes locally
"@
    }
    default { '' }
}

$report = @"
# Build Report — PR #$PRNumber

## Build Status: $statusDisplay

## Environment
- PR: #$PRNumber
- Branch: $headBranch
- Base: $baseBranch
- Head SHA: $headSha
- Build date: $(Get-Date -Format 'o')

## Build Steps
$stepsText

## Actions Taken to Fix Build
$fixActionsText
$errorsSection
$suggestionsSection
"@

$reportPath = Join-Path $OutputDir 'build-report.md'
$report | Set-Content $reportPath -Force
Info "Build report written: $reportPath"

# Return result object for the orchestrator
return @{
    Status      = $buildStatus
    Actions     = $actions
    Errors      = $buildErrors
    ReportPath  = $reportPath
}
