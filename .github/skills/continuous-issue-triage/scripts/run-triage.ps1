<#
.SYNOPSIS
    Runs continuous issue triage using GitHub Copilot CLI with parallel processing.

.DESCRIPTION
    Orchestrates the full triage workflow:
    1. Collects active issues
    2. Analyzes issues in parallel using Copilot CLI
    3. Categorizes results
    4. Generates reports
    5. Updates state for delta tracking

.PARAMETER RunType
    Type of triage run: daily, twice-weekly, weekly. Default: weekly

.PARAMETER MaxParallel
    Maximum parallel Copilot CLI invocations. Default: 5

.PARAMETER TimeoutMinutes
    Timeout for each Copilot analysis. Default: 5

.PARAMETER MaxRetries
    Maximum retries on timeout. Default: 3

.PARAMETER Model
    Copilot model to use (optional).

.PARAMETER McpConfig
    Path to MCP config file (optional).

.PARAMETER LookbackDays
    For first run, days to look back. Default: 7

.PARAMETER Force
    Force re-analysis of all issues, ignoring cache.

.EXAMPLE
    .\run-triage.ps1

.EXAMPLE
    .\run-triage.ps1 -RunType daily -MaxParallel 10 -Model "claude-sonnet-4"
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet("daily", "twice-weekly", "weekly")]
    [string]$RunType = "weekly",
    
    [Parameter()]
    [int]$MaxParallel = 5,
    
    [Parameter()]
    [int]$TimeoutMinutes = 5,
    
    [Parameter()]
    [int]$MaxRetries = 3,
    
    [Parameter()]
    [string]$Model,
    
    [Parameter()]
    [string]$McpConfig,
    
    [Parameter()]
    [int]$LookbackDays = 7,
    
    [Parameter()]
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = git rev-parse --show-toplevel 2>$null

# Resolve config directory name (.github or .claude) from script location
$_cfgDir = if ($PSScriptRoot -match '[\\/](\.github|\.claude)[\\/]') { $Matches[1] } else { '.github' }
if (-not $repoRoot) {
    $repoRoot = (Get-Location).Path
}

# Paths
$triageRoot = Join-Path $repoRoot "Generated Files/triage-issues"
$currentRunPath = Join-Path $triageRoot "current-run"
$statePath = Join-Path $triageRoot "triage-state.json"
$issueCachePath = Join-Path $triageRoot "issue-cache"
$historyPath = Join-Path $triageRoot "history"

# Ensure directories exist
@($triageRoot, $currentRunPath, $issueCachePath, $historyPath) | ForEach-Object {
    if (-not (Test-Path $_)) {
        New-Item -ItemType Directory -Path $_ -Force | Out-Null
    }
}

Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  PowerToys Issue Triage - $RunType run" -ForegroundColor Cyan
Write-Host "  Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

#region State Management
Write-Host "[1/6] Loading previous state..." -ForegroundColor Yellow

$state = $null
if (Test-Path $statePath) {
    $state = Get-Content $statePath -Raw | ConvertFrom-Json -AsHashtable
    Write-Host "  ✓ Loaded state from: $($state.lastRun)" -ForegroundColor Green
    Write-Host "  Previous run type: $($state.lastRunType)" -ForegroundColor Gray
    Write-Host "  Known issues: $($state.issueSnapshots.Count)" -ForegroundColor Gray
} else {
    Write-Host "  First run - initializing fresh state" -ForegroundColor Yellow
    $state = @{
        version = "1.0"
        lastRun = $null
        lastRunType = $null
        issueSnapshots = @{}
        pendingFollowUps = @()
        closedWithActivity = @()
        analysisResults = @{}
        statistics = @{
            totalRunCount = 0
            issuesAnalyzed = 0
            repliesPosted = 0
            issuesClosed = 0
        }
    }
}
#endregion

#region Issue Collection
Write-Host ""
Write-Host "[2/6] Collecting active issues..." -ForegroundColor Yellow

$since = if ($state.lastRun) { $state.lastRun } else { (Get-Date).AddDays(-$LookbackDays).ToUniversalTime().ToString("o") }
Write-Host "  Looking for issues updated since: $since" -ForegroundColor Gray

# Collect open issues with recent activity
$openIssuesJson = gh issue list --state open --json number,title,updatedAt,labels --limit 500 2>$null
$openIssues = $openIssuesJson | ConvertFrom-Json | Where-Object { 
    [datetime]$_.updatedAt -gt [datetime]$since 
}

# Collect closed issues with post-close activity (within 30 days)
$closedIssuesJson = gh issue list --state closed --json number,title,updatedAt,closedAt --limit 200 2>$null
$closedIssues = $closedIssuesJson | ConvertFrom-Json | Where-Object {
    $closedAt = [datetime]$_.closedAt
    $updatedAt = [datetime]$_.updatedAt
    $cutoff = (Get-Date).AddDays(-30)
    ($closedAt -gt $cutoff) -and ($updatedAt -gt $closedAt)
}

# Combine and dedupe
$allIssues = @()
$allIssues += $openIssues | ForEach-Object { @{ number = $_.number; title = $_.title; state = "open"; updatedAt = $_.updatedAt } }
$allIssues += $closedIssues | ForEach-Object { @{ number = $_.number; title = $_.title; state = "closed"; updatedAt = $_.updatedAt } }

# Add pending follow-ups from previous run
if ($state.pendingFollowUps) {
    foreach ($pending in $state.pendingFollowUps) {
        if ($pending.status -eq "pending" -and ($allIssues.number -notcontains $pending.issueNumber)) {
            $allIssues += @{ number = $pending.issueNumber; title = "pending-followup"; state = "unknown" }
        }
    }
}

$uniqueIssues = $allIssues | Group-Object number | ForEach-Object { $_.Group | Select-Object -First 1 }

Write-Host "  ✓ Found $($uniqueIssues.Count) issues to analyze" -ForegroundColor Green
Write-Host "    - Open with activity: $(($uniqueIssues | Where-Object { $_.state -eq 'open' }).Count)" -ForegroundColor Gray
Write-Host "    - Closed with activity: $(($uniqueIssues | Where-Object { $_.state -eq 'closed' }).Count)" -ForegroundColor Gray
#endregion

#region Filter for Analysis
Write-Host ""
Write-Host "[3/6] Filtering issues for analysis..." -ForegroundColor Yellow

$issuesToAnalyze = @()
foreach ($issue in $uniqueIssues) {
    $issueNum = $issue.number
    $cached = $state.analysisResults[$issueNum.ToString()]
    
    $needsAnalysis = $false
    $reason = ""
    
    if ($Force) {
        $needsAnalysis = $true
        $reason = "forced"
    }
    elseif (-not $cached) {
        $needsAnalysis = $true
        $reason = "new"
    }
    elseif ($cached.analyzedAt) {
        $daysSinceAnalysis = ((Get-Date) - [datetime]$cached.analyzedAt).Days
        if ($daysSinceAnalysis -gt 7) {
            $needsAnalysis = $true
            $reason = "stale-cache"
        }
        elseif ($cached.commentCountAtAnalysis -and $state.issueSnapshots[$issueNum.ToString()]) {
            $previousCount = $state.issueSnapshots[$issueNum.ToString()].commentCount
            if ($cached.commentCountAtAnalysis -lt $previousCount) {
                $needsAnalysis = $true
                $reason = "new-comments"
            }
        }
    }
    
    if ($needsAnalysis) {
        $issuesToAnalyze += @{
            number = $issueNum
            title = $issue.title
            state = $issue.state
            reason = $reason
        }
    }
}

Write-Host "  ✓ $($issuesToAnalyze.Count) issues need analysis" -ForegroundColor Green
Write-Host "  ✓ $($uniqueIssues.Count - $issuesToAnalyze.Count) issues using cached results" -ForegroundColor Gray
#endregion

#region Parallel Copilot Analysis
Write-Host ""
Write-Host "[4/6] Running parallel Copilot analysis..." -ForegroundColor Yellow
Write-Host "  Max parallel: $MaxParallel | Timeout: ${TimeoutMinutes}m | Max retries: $MaxRetries" -ForegroundColor Gray
Write-Host ""

# Prepare the prompt template
$promptTemplate = @"
Analyze GitHub issue #ISSUE_NUMBER for PowerToys triage.

Use the review-issue prompt methodology from $_cfgDir/prompts/review-issue.prompt.md.

Output a JSON summary to stdout with this structure:
{
  "issueNumber": ISSUE_NUMBER,
  "category": "trending|needs-label|ready-for-fix|needs-info|needs-clarification|closeable|stale-waiting|duplicate-candidate|review-needed",
  "categoryReason": "brief explanation",
  "priorityScore": 0-100,
  "suggestedAction": "what human should do",
  "suggestedLabels": ["label1", "label2"],
  "labelConfidence": 0-100,
  "missingInfo": ["item1", "item2"],
  "similarIssues": [12345, 12346],
  "potentialAssignees": ["@user1", "@user2"],
  "draftReply": "if needs-info or needs-clarification, draft the reply message here",
  "clarityScore": 0-100,
  "feasibilityScore": 0-100,
  "newCommentsSummary": "brief summary of recent discussion if trending"
}

Focus on actionable triage. Be concise.
"@

# Thread-safe collections for results
$analysisResults = [System.Collections.Concurrent.ConcurrentDictionary[string, object]]::new()
$analysisErrors = [System.Collections.Concurrent.ConcurrentBag[object]]::new()

# Progress tracking
$totalIssues = $issuesToAnalyze.Count
$completedCount = [ref]0
$startTime = Get-Date

if ($totalIssues -gt 0) {
    $issuesToAnalyze | ForEach-Object -ThrottleLimit $MaxParallel -Parallel {
        $issue = $_
        $issueNum = $issue.number
        $results = $using:analysisResults
        $errors = $using:analysisErrors
        $completed = $using:completedCount
        $total = $using:totalIssues
        $timeoutMin = $using:TimeoutMinutes
        $maxRetry = $using:MaxRetries
        $model = $using:Model
        $mcpCfg = $using:McpConfig
        $template = $using:promptTemplate
        $root = $using:repoRoot
        $cachePath = $using:issueCachePath
        
        $prompt = $template -replace 'ISSUE_NUMBER', $issueNum
        $logDir = Join-Path $cachePath $issueNum
        if (-not (Test-Path $logDir)) {
            New-Item -ItemType Directory -Path $logDir -Force | Out-Null
        }
        
        $success = $false
        $lastError = $null
        $output = $null
        
        for ($retry = 0; $retry -lt $maxRetry -and -not $success; $retry++) {
            if ($retry -gt 0) {
                Write-Host "    ⟳ Retry $retry/$maxRetry for #$issueNum" -ForegroundColor Yellow
                Start-Sleep -Seconds 10
            }
            
            try {
                # Build Copilot CLI arguments
                $copilotArgs = @()
                if ($mcpCfg) {
                    $copilotArgs += @('--additional-mcp-config', $mcpCfg)
                }
                $copilotArgs += @('-p', $prompt, '--yolo', '--agent', 'ReviewIssue')
                if ($model) {
                    $copilotArgs += @('--model', $model)
                }
                
                # Run with timeout
                $job = Start-Job -ScriptBlock {
                    param($args)
                    & copilot @args 2>&1
                } -ArgumentList (,$copilotArgs)
                
                $timeoutSec = $timeoutMin * 60
                $jobResult = $job | Wait-Job -Timeout $timeoutSec
                
                if ($job.State -eq 'Running') {
                    # Timeout - kill the job
                    $job | Stop-Job -PassThru | Remove-Job -Force
                    $lastError = "Timeout after ${timeoutMin} minutes"
                } else {
                    $output = $job | Receive-Job
                    $job | Remove-Job -Force
                    
                    # Check for valid output
                    if ($output) {
                        $outputStr = $output -join "`n"
                        # Try to extract JSON from output
                        if ($outputStr -match '\{[\s\S]*"issueNumber"[\s\S]*\}') {
                            $success = $true
                        } else {
                            $lastError = "No valid JSON in output"
                        }
                    } else {
                        $lastError = "Empty output from Copilot"
                    }
                }
            }
            catch {
                $lastError = $_.Exception.Message
            }
        }
        
        # Update progress
        [System.Threading.Interlocked]::Increment($completed) | Out-Null
        $pct = [math]::Round(($completed.Value / $total) * 100)
        
        if ($success) {
            # Save output and parse result
            $outputStr = $output -join "`n"
            $outputStr | Out-File -FilePath (Join-Path $logDir "analysis.log") -Force
            
            # Try to extract JSON
            try {
                if ($outputStr -match '(\{[\s\S]*"issueNumber"[\s\S]*\})') {
                    $jsonStr = $Matches[1]
                    $parsed = $jsonStr | ConvertFrom-Json -AsHashtable
                    $results[$issueNum.ToString()] = @{
                        success = $true
                        data = $parsed
                        analyzedAt = (Get-Date).ToUniversalTime().ToString("o")
                    }
                    Write-Host "  [$pct%] ✓ #$issueNum - $($parsed.category)" -ForegroundColor Green
                }
            }
            catch {
                $errors.Add(@{ issueNumber = $issueNum; error = "JSON parse error: $_" })
                Write-Host "  [$pct%] ⚠ #$issueNum - JSON parse failed" -ForegroundColor Yellow
            }
        } else {
            # Log error
            $lastError | Out-File -FilePath (Join-Path $logDir "error.log") -Force
            $errors.Add(@{ issueNumber = $issueNum; error = $lastError; retries = $maxRetry })
            Write-Host "  [$pct%] ✗ #$issueNum - $lastError" -ForegroundColor Red
        }
    }
}

$elapsed = (Get-Date) - $startTime
Write-Host ""
Write-Host "  Analysis complete in $([math]::Round($elapsed.TotalMinutes, 1)) minutes" -ForegroundColor Cyan
Write-Host "  ✓ Successful: $($analysisResults.Count)" -ForegroundColor Green
Write-Host "  ✗ Failed: $($analysisErrors.Count)" -ForegroundColor $(if ($analysisErrors.Count -gt 0) { 'Red' } else { 'Gray' })
#endregion

#region Merge Results & Categorize
Write-Host ""
Write-Host "[5/6] Merging results and updating state..." -ForegroundColor Yellow

# Merge new analysis with cached results
$allResults = @{}

# Add cached results
foreach ($key in $state.analysisResults.Keys) {
    if (-not $analysisResults.ContainsKey($key)) {
        $allResults[$key] = $state.analysisResults[$key]
    }
}

# Add new results
foreach ($key in $analysisResults.Keys) {
    $allResults[$key] = $analysisResults[$key]
}

# Categorize for reporting
$categorized = @{
    trending = @()
    "needs-label" = @()
    "ready-for-fix" = @()
    "needs-info" = @()
    "needs-clarification" = @()
    closeable = @()
    "stale-waiting" = @()
    "duplicate-candidate" = @()
    "review-needed" = @()
}

foreach ($key in $allResults.Keys) {
    $result = $allResults[$key]
    if ($result.success -and $result.data) {
        $data = $result.data
        $category = $data.category
        if ($categorized.ContainsKey($category)) {
            $categorized[$category] += $data
        } else {
            $categorized["review-needed"] += $data
        }
    }
}

# Sort each category by priority
foreach ($cat in $categorized.Keys) {
    $categorized[$cat] = $categorized[$cat] | Sort-Object { -[int]$_.priorityScore }
}

Write-Host "  Categorization complete:" -ForegroundColor Green
foreach ($cat in $categorized.Keys | Sort-Object { $categorized[$_].Count } -Descending) {
    if ($categorized[$cat].Count -gt 0) {
        Write-Host "    - $cat`: $($categorized[$cat].Count)" -ForegroundColor Gray
    }
}
#endregion

#region Generate Reports
Write-Host ""
Write-Host "[6/6] Generating reports..." -ForegroundColor Yellow

# Archive previous run
$archiveDate = Get-Date -Format "yyyy-MM-dd_HHmm"
$archivePath = Join-Path $historyPath $archiveDate
if (Test-Path "$currentRunPath/summary.md") {
    New-Item -ItemType Directory -Path $archivePath -Force | Out-Null
    Copy-Item -Path "$currentRunPath/*" -Destination $archivePath -Recurse -Force
    Write-Host "  ✓ Archived previous run to: $archiveDate" -ForegroundColor Gray
}

# Clean current run
if (Test-Path $currentRunPath) {
    Remove-Item -Path "$currentRunPath/*" -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path "$currentRunPath/draft-replies" -Force | Out-Null

# Category info for display
$categoryInfo = @{
    "trending" = @{ emoji = "🔥"; name = "Trending" }
    "needs-label" = @{ emoji = "🏷️"; name = "Needs-Label" }
    "ready-for-fix" = @{ emoji = "✅"; name = "Ready-for-Fix" }
    "needs-info" = @{ emoji = "❓"; name = "Needs-Info" }
    "needs-clarification" = @{ emoji = "💬"; name = "Needs-Clarification" }
    "closeable" = @{ emoji = "✔️"; name = "Closeable" }
    "stale-waiting" = @{ emoji = "⏳"; name = "Stale-Waiting" }
    "duplicate-candidate" = @{ emoji = "🔁"; name = "Duplicate-Candidate" }
    "review-needed" = @{ emoji = "👀"; name = "Review-Needed" }
}

$repoUrl = "https://github.com/microsoft/PowerToys/issues"

# Generate summary.md
$summary = @"
# Issue Triage Summary - $(Get-Date -Format 'yyyy-MM-dd')

**Run Type**: $RunType | **Time**: $(Get-Date -Format 'HH:mm UTC') | **Duration**: $([math]::Round($elapsed.TotalMinutes, 1)) min

## 📊 Delta Since Last Run

| Metric | Value |
|--------|-------|
| Issues with new activity | $($uniqueIssues.Count) |
| Newly analyzed | $($analysisResults.Count) |
| Using cached analysis | $($allResults.Count - $analysisResults.Count) |
| Analysis failures | $($analysisErrors.Count) |

## ⚡ Action Required by Category

| Category | Count | Top Priority | Score |
|----------|-------|--------------|-------|

"@

foreach ($cat in @("trending", "needs-label", "ready-for-fix", "needs-info", "needs-clarification", "closeable", "stale-waiting", "duplicate-candidate", "review-needed")) {
    $info = $categoryInfo[$cat]
    $issues = $categorized[$cat]
    if ($issues.Count -gt 0) {
        $top = $issues[0]
        $summary += "| $($info.emoji) $($info.name) | $($issues.Count) | [#$($top.issueNumber)]($repoUrl/$($top.issueNumber)) | $($top.priorityScore)/100 |`n"
    }
}

$summary += @"

## 🎯 Top 10 Priority Actions

"@

# Get top 10 across all categories
$allIssueData = @()
foreach ($cat in $categorized.Keys) {
    $allIssueData += $categorized[$cat]
}
$topIssues = $allIssueData | Sort-Object { -[int]$_.priorityScore } | Select-Object -First 10

$priority = 1
foreach ($issue in $topIssues) {
    $info = $categoryInfo[$issue.category]
    $urgency = if ([int]$issue.priorityScore -ge 80) { "**[Urgent]**" } 
               elseif ([int]$issue.priorityScore -ge 60) { "**[High]**" }
               elseif ([int]$issue.priorityScore -ge 40) { "[Medium]" }
               else { "[Low]" }
    
    $summary += "$priority. $urgency $($info.emoji) [#$($issue.issueNumber)]($repoUrl/$($issue.issueNumber)) - $($issue.categoryReason)`n"
    $priority++
}

$summary += @"

## 📁 Detailed Reports

"@

foreach ($cat in @("trending", "needs-label", "ready-for-fix", "needs-info", "needs-clarification", "closeable", "stale-waiting", "duplicate-candidate")) {
    $info = $categoryInfo[$cat]
    if ($categorized[$cat].Count -gt 0) {
        $summary += "- [$($info.emoji) $($info.name)](./$cat.md) ($($categorized[$cat].Count) issues)`n"
    }
}

$summary += @"

## 📝 Draft Replies Ready

"@

$draftsWritten = 0
foreach ($cat in @("needs-info", "needs-clarification", "closeable", "stale-waiting")) {
    foreach ($issue in $categorized[$cat]) {
        if ($issue.draftReply) {
            $draftPath = Join-Path "$currentRunPath/draft-replies" "issue-$($issue.issueNumber).md"
            $draftContent = @"
---
issue: $($issue.issueNumber)
category: $($issue.category)
generated: $(Get-Date -Format "o")
---

$($issue.draftReply)
"@
            $draftContent | Out-File -FilePath $draftPath -Force
            $draftsWritten++
        }
    }
}

$summary += "**$draftsWritten** draft replies ready in ``draft-replies/```n`n"

if ($analysisErrors.Count -gt 0) {
    $summary += @"

## ⚠️ Analysis Failures

| Issue | Error |
|-------|-------|

"@
    foreach ($err in $analysisErrors) {
        $summary += "| #$($err.issueNumber) | $($err.error) |`n"
    }
}

$summary += @"

---
*Generated by continuous-issue-triage skill*
*Next suggested run: $(Get-Date (Get-Date).AddDays($(if ($RunType -eq 'daily') { 1 } elseif ($RunType -eq 'twice-weekly') { 3 } else { 7 })) -Format 'yyyy-MM-dd')*
"@

$summary | Out-File -FilePath "$currentRunPath/summary.md" -Force
Write-Host "  ✓ Generated: summary.md" -ForegroundColor Green

# Generate category reports
foreach ($cat in $categorized.Keys) {
    $issues = $categorized[$cat]
    if ($issues.Count -eq 0) { continue }
    
    $info = $categoryInfo[$cat]
    $report = @"
# $($info.emoji) $($info.name) Issues

**Total**: $($issues.Count) issues

## Overview

| # | Issue | Priority | Reason | Suggested Action |
|---|-------|----------|--------|------------------|

"@
    
    foreach ($issue in $issues) {
        $reason = if ($issue.categoryReason.Length -gt 40) { $issue.categoryReason.Substring(0, 37) + "..." } else { $issue.categoryReason }
        $action = if ($issue.suggestedAction.Length -gt 40) { $issue.suggestedAction.Substring(0, 37) + "..." } else { $issue.suggestedAction }
        $report += "| [#$($issue.issueNumber)]($repoUrl/$($issue.issueNumber)) | $($issue.priorityScore)/100 | $reason | $action |`n"
    }
    
    $report += "`n## Detailed Breakdown`n`n"
    
    foreach ($issue in $issues) {
        $report += @"
### [#$($issue.issueNumber)]($repoUrl/$($issue.issueNumber))

- **Priority Score**: $($issue.priorityScore)/100
- **Category Reason**: $($issue.categoryReason)
- **Suggested Action**: $($issue.suggestedAction)
- **Clarity Score**: $($issue.clarityScore)/100
- **Feasibility Score**: $($issue.feasibilityScore)/100

"@
        if ($issue.suggestedLabels -and $issue.suggestedLabels.Count -gt 0) {
            $report += "- **Suggested Labels**: $($issue.suggestedLabels -join ', ') (confidence: $($issue.labelConfidence)%)`n"
        }
        if ($issue.missingInfo -and $issue.missingInfo.Count -gt 0) {
            $report += "- **Missing Info**: $($issue.missingInfo -join ', ')`n"
        }
        if ($issue.potentialAssignees -and $issue.potentialAssignees.Count -gt 0) {
            $report += "- **Potential Assignees**: $($issue.potentialAssignees -join ', ')`n"
        }
        if ($issue.similarIssues -and $issue.similarIssues.Count -gt 0) {
            $report += "- **Similar Issues**: #$($issue.similarIssues -join ', #')`n"
        }
        if ($issue.draftReply) {
            $report += "- **Draft Reply**: [View](./draft-replies/issue-$($issue.issueNumber).md)`n"
        }
        $report += "`n---`n`n"
    }
    
    $report | Out-File -FilePath "$currentRunPath/$cat.md" -Force
    Write-Host "  ✓ Generated: $cat.md ($($issues.Count) issues)" -ForegroundColor Green
}
#endregion

#region Save State
Write-Host ""
Write-Host "Saving state for next run..." -ForegroundColor Yellow

# Update issue snapshots
foreach ($issue in $uniqueIssues) {
    $issueNum = $issue.number.ToString()
    $result = $allResults[$issueNum]
    
    $state.issueSnapshots[$issueNum] = @{
        number = $issue.number
        title = $issue.title
        state = $issue.state
        lastSeenAt = (Get-Date).ToUniversalTime().ToString("o")
        category = if ($result.data) { $result.data.category } else { "unknown" }
        priorityScore = if ($result.data) { $result.data.priorityScore } else { 0 }
    }
}

$state.lastRun = (Get-Date).ToUniversalTime().ToString("o")
$state.lastRunType = $RunType
$state.analysisResults = $allResults
$state.statistics.totalRunCount++
$state.statistics.issuesAnalyzed += $analysisResults.Count

$state | ConvertTo-Json -Depth 10 | Out-File -FilePath $statePath -Force
Write-Host "  ✓ State saved" -ForegroundColor Green
#endregion

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Triage complete!" -ForegroundColor Cyan
Write-Host "  Reports: $currentRunPath" -ForegroundColor Cyan
Write-Host "  Start with: summary.md" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
