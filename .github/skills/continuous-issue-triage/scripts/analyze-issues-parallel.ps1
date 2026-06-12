<#
.SYNOPSIS
    Runs Copilot CLI analysis on issues using review-issue prompt.

.DESCRIPTION
    Kicks off GitHub Copilot CLI to analyze each issue using
    the review-issue.prompt.md file. Processes sequentially with timeout handling.

.PARAMETER IssueNumbers
    Array of issue numbers to analyze. If not provided, collects from recent activity.

.PARAMETER TimeoutMinutes
    Timeout for each Copilot analysis. Default: 8

.PARAMETER MaxRetryCount
    Maximum retries on timeout/failure. Default: 3

.PARAMETER Model
    Copilot model to use (optional).

.EXAMPLE
    .\analyze-issues-parallel.ps1 -IssueNumbers @(45201, 45107, 45321)

.EXAMPLE
    .\analyze-issues-parallel.ps1 -TimeoutMinutes 10 -MaxRetries 2
#>

[CmdletBinding()]
param(
    [Parameter()]
    [int[]]$IssueNumbers,
    
    [Parameter()]
    [int]$TimeoutMinutes = 8,
    
    [Parameter()]
    [int]$MaxRetryCount = 3,
    
    [Parameter()]
    [string]$Model,
    
    [Parameter()]
    [int]$LookbackDays = 14,
    
    [Parameter()]
    [int]$MaxIssues = 15
)

$ErrorActionPreference = "Stop"
$repoRoot = (git rev-parse --show-toplevel 2>$null); if (-not $repoRoot) { $repoRoot = (Get-Location).Path }; $repoRoot = (Resolve-Path $repoRoot).Path

# Resolve config directory name (.github or .claude) from script location
$_cfgDir = if ($PSScriptRoot -match '[\\/](\.github|\.claude)[\\/]') { $Matches[1] } else { '.github' }
$triageRoot = Join-Path $repoRoot "Generated Files\triage-issues"
$issueCachePath = Join-Path $triageRoot "issue-cache"
$promptPath = Join-Path $repoRoot "$_cfgDir\prompts\review-issue.prompt.md"

# Ensure directories exist
if (-not (Test-Path $issueCachePath)) {
    New-Item -ItemType Directory -Path $issueCachePath -Force | Out-Null
}

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Issue Analysis with Copilot CLI" -ForegroundColor Cyan
Write-Host "  Using: review-issue.prompt.md" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# If no issues provided, collect from recent activity
if (-not $IssueNumbers -or $IssueNumbers.Count -eq 0) {
    Write-Host "Collecting issues from last $LookbackDays days..." -ForegroundColor Yellow
    
    $issues = gh issue list --state open --json number,title,comments,updatedAt --limit 200 | ConvertFrom-Json
    $recent = $issues | Where-Object { [datetime]$_.updatedAt -gt (Get-Date).AddDays(-$LookbackDays) }
    
    # Prioritize: trending first, then by recency
    $prioritized = $recent | Sort-Object { -$_.comments.Count }, { [datetime]$_.updatedAt } -Descending
    $IssueNumbers = ($prioritized | Select-Object -First $MaxIssues).number
    
    Write-Host "  Found $($recent.Count) recent issues, selected top $($IssueNumbers.Count) for analysis" -ForegroundColor Green
}

Write-Host ""
Write-Host "Issues to analyze: $($IssueNumbers -join ', ')" -ForegroundColor Cyan
Write-Host "Timeout: ${TimeoutMinutes}m | Retries: $MaxRetryCount" -ForegroundColor Gray
Write-Host ""

# Results tracking
$results = @{}
$startTime = Get-Date
$totalIssues = $IssueNumbers.Count
$current = 0

foreach ($issueNum in $IssueNumbers) {
    $current++
    $issueDir = Join-Path $issueCachePath $issueNum
    if (-not (Test-Path $issueDir)) {
        New-Item -ItemType Directory -Path $issueDir -Force | Out-Null
    }
    
    $logFile = Join-Path $issueDir "analysis.log"
    $errorFile = Join-Path $issueDir "error.log"
    $statusFile = Join-Path $issueDir "status.json"
    
    Write-Host ""
    Write-Host "[$current/$totalIssues] #$issueNum - Beginning analysis..." -ForegroundColor Yellow
    
    $success = $false
    $lastError = $null
    $retryCount = 0
    
    for ($retry = 0; $retry -lt $MaxRetryCount -and -not $success; $retry++) {
        $retryCount = $retry + 1
        
        if ($retry -gt 0) {
            Write-Host "  [RETRY] Attempt $retryCount/$MaxRetryCount (waiting 10s)..." -ForegroundColor Yellow
            Start-Sleep -Seconds 10
        }
        
        try {
            # Build the prompt - use the review-issue prompt directly
            $prompt = @"
Analyze GitHub issue #$issueNum using the methodology from $_cfgDir/prompts/review-issue.prompt.md

First, fetch the issue data:
gh issue view $issueNum --json number,title,body,author,createdAt,updatedAt,state,labels,milestone,reactions,comments,linkedPullRequests

Then produce a concise JSON summary with this structure (output ONLY the JSON):
{
  "issueNumber": $issueNum,
  "title": "issue title",
  "category": "trending|needs-label|ready-for-fix|needs-info|needs-clarification|closeable|stale-waiting|duplicate-candidate|review-needed",
  "categoryReason": "brief explanation",
  "priorityScore": 0-100,
  "clarityScore": 0-100,
  "feasibilityScore": 0-100,
  "suggestedAction": "what human should do",
  "suggestedLabels": ["label1", "label2"],
  "missingInfo": ["item1", "item2"],
  "draftReply": "if needs-info or needs-clarification, draft the reply"
}
"@
            
            # Build Copilot CLI arguments
            $copilotArgs = @('-p', $prompt, '--yolo', '--agent', 'ReviewIssue')
            if ($Model) {
                $copilotArgs += @('--model', $Model)
            }
            
            Write-Host "  Running copilot CLI..." -ForegroundColor Gray
            
            # Run copilot directly (not in job)
            $output = & copilot @copilotArgs 2>&1
            $outputStr = $output | Out-String
            
            # Save the output
            $outputStr | Out-File -FilePath $logFile -Force
            
            # Check for valid output
            if ($outputStr.Length -gt 200) {
                $success = $true
                Write-Host "  [SUCCESS] Analysis complete ($($outputStr.Length) chars)" -ForegroundColor Green
            }
            else {
                $lastError = "Output too short ($($outputStr.Length) chars)"
                Write-Host "  [WARN] $lastError" -ForegroundColor Yellow
            }
        }
        catch {
            $lastError = $_.Exception.Message
            Write-Host "  [ERROR] $lastError" -ForegroundColor Red
        }
    }
    
    # Save status
    $status = @{
        issueNumber = $issueNum
        success = $success
        attempts = $retryCount
        lastError = $lastError
        analyzedAt = (Get-Date).ToUniversalTime().ToString("o")
    }
    $status | ConvertTo-Json | Out-File -FilePath $statusFile -Force
    $results[$issueNum] = $status
    
    if (-not $success) {
        $lastError | Out-File -FilePath $errorFile -Force
        Write-Host "  [FAILED] All $MaxRetryCount attempts failed: $lastError" -ForegroundColor Red
    }
}

$elapsed = (Get-Date) - $startTime

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Analysis Complete" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "Duration: $([math]::Round($elapsed.TotalMinutes, 1)) minutes" -ForegroundColor Gray
Write-Host "Total issues: $($IssueNumbers.Count)" -ForegroundColor Gray

$successCount = ($results.Values | Where-Object { $_.success }).Count
$failCount = ($results.Values | Where-Object { -not $_.success }).Count

Write-Host "Successful: $successCount" -ForegroundColor Green
Write-Host "Failed: $failCount" -ForegroundColor $(if ($failCount -gt 0) { 'Red' } else { 'Gray' })

if ($failCount -gt 0) {
    Write-Host ""
    Write-Host "Failed issues:" -ForegroundColor Red
    $results.Values | Where-Object { -not $_.success } | ForEach-Object {
        Write-Host "  #$($_.issueNumber): $($_.lastError)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Results saved to: $issueCachePath" -ForegroundColor Cyan
