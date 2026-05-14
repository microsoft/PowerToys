<#
.SYNOPSIS
    Run issue-review-review in parallel from a single terminal.

.PARAMETER IssueNumbers
    Issue numbers to review-review.

.PARAMETER ThrottleLimit
    Maximum parallel tasks.

.PARAMETER CLIType
    AI CLI type (copilot/claude).

.PARAMETER Model
    Copilot CLI model to use (e.g., gpt-5.2-codex).

.PARAMETER Force
    Skip confirmation prompts.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [int[]]$IssueNumbers,

    [int]$ThrottleLimit = 5,

    [ValidateSet('copilot', 'claude')]
    [string]$CLIType = 'copilot',

    [string]$Model,

    [switch]$Force
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')

# Resolve config directory name (.github or .claude) from script location
$_cfgDir = if ($PSScriptRoot -match '[\\/](\.github|\.claude)[\\/]') { $Matches[1] } else { '.github' }
$scriptPath = Join-Path $repoRoot "$_cfgDir\skills\issue-review-review\scripts\Start-IssueReviewReview.ps1"

$results = $IssueNumbers | ForEach-Object -Parallel {
    $issue = $PSItem
    $repoRoot = $using:repoRoot
    $scriptPath = $using:scriptPath
    $cliType = $using:CLIType
    $model = $using:Model
    $force = $using:Force

    Set-Location $repoRoot

    if (-not $issue) {
        return [pscustomobject]@{
            IssueNumber  = $issue
            ExitCode     = 1
            QualityScore = 0
            Error        = 'Issue number is empty.'
        }
    }

    $params = @{
        IssueNumber = [int]$issue
        CLIType     = $cliType
    }
    if ($model) {
        $params.Model = $model
    }
    if ($force) {
        $params.Force = $true
    }

    try {
        $result = & $scriptPath @params
        [pscustomobject]@{
            IssueNumber   = $issue
            ExitCode      = $LASTEXITCODE
            QualityScore  = $result.QualityScore
            NeedsReReview = $result.NeedsReReview
            Iteration     = $result.Iteration
            Verdict       = $result.Verdict
        }
    }
    catch {
        [pscustomobject]@{
            IssueNumber   = $issue
            ExitCode      = 1
            QualityScore  = 0
            NeedsReReview = $true
            Error         = $_.Exception.Message
        }
    }
} -ThrottleLimit $ThrottleLimit

# Summary
$passed = @($results | Where-Object { $_.QualityScore -ge 90 })
$needsWork = @($results | Where-Object { $_.QualityScore -gt 0 -and $_.QualityScore -lt 90 })
$failed = @($results | Where-Object { $_.QualityScore -eq 0 -or $_.Error })

Write-Host "`n=== REVIEW-REVIEW SUMMARY ===" -ForegroundColor Cyan
Write-Host "Total:          $($results.Count)"
Write-Host "Passed (>=90):  $($passed.Count)" -ForegroundColor Green
Write-Host "Needs work:     $($needsWork.Count)" -ForegroundColor Yellow
Write-Host "Failed:         $($failed.Count)" -ForegroundColor Red

if ($needsWork.Count -gt 0) {
    Write-Host "`nIssues needing re-review:" -ForegroundColor Yellow
    foreach ($r in $needsWork) {
        Write-Host "  #$($r.IssueNumber) — score: $($r.QualityScore)/100 (iteration $($r.Iteration))"
    }
}

$results
