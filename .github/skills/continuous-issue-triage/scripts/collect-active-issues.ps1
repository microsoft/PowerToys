<#
.SYNOPSIS
    Collects GitHub issues with activity since the last triage run.

.DESCRIPTION
    Fetches open issues updated since the last run, closed issues with new comments,
    and issues with pending follow-up actions.

.PARAMETER Since
    ISO 8601 datetime string. Collect issues updated after this time.
    If not specified, reads from triage-state.json.

.PARAMETER LookbackDays
    For first run (no state), how many days to look back. Default: 7.

.PARAMETER OutputPath
    Path to save collected issues JSON. Default: Generated Files/triage-issues/current-run/collected-issues.json

.PARAMETER Limit
    Maximum issues to collect per query. Default: 500.

.EXAMPLE
    .\collect-active-issues.ps1

.EXAMPLE
    .\collect-active-issues.ps1 -Since "2026-01-29T00:00:00Z" -Limit 100
#>

param(
    [Parameter()]
    [string]$Since,
    
    [Parameter()]
    [int]$LookbackDays = 7,
    
    [Parameter()]
    [string]$OutputPath = "Generated Files/triage-issues/current-run/collected-issues.json",
    
    [Parameter()]
    [int]$Limit = 500
)

$ErrorActionPreference = "Stop"

# Determine the "since" timestamp
if (-not $Since) {
    $statePath = "Generated Files/triage-issues/triage-state.json"
    if (Test-Path $statePath) {
        $state = Get-Content $statePath | ConvertFrom-Json
        if ($state.lastRun) {
            $Since = $state.lastRun
            Write-Host "Using last run timestamp: $Since"
        }
    }
    
    if (-not $Since) {
        $Since = (Get-Date).AddDays(-$LookbackDays).ToUniversalTime().ToString("o")
        Write-Host "First run - looking back $LookbackDays days to: $Since"
    }
}

$sinceDate = [datetime]$Since

# Ensure output directory exists
$outputDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
}

$collectedIssues = @()

# 1. Collect open issues updated since last run
Write-Host "Fetching open issues updated since $Since..."
$openIssues = gh issue list `
    --state open `
    --json number,title,updatedAt `
    --limit $Limit 2>$null | ConvertFrom-Json

$filteredOpen = $openIssues | Where-Object { 
    [datetime]$_.updatedAt -gt $sinceDate 
}
Write-Host "  Found $($filteredOpen.Count) open issues with recent activity"

foreach ($issue in $filteredOpen) {
    $collectedIssues += @{
        number = $issue.number
        title = $issue.title
        source = "open-updated"
        updatedAt = $issue.updatedAt
    }
}

# 2. Collect closed issues with recent activity (within tracking window)
Write-Host "Fetching closed issues with recent comments..."
$trackingDays = 30
$trackingCutoff = (Get-Date).AddDays(-$trackingDays)

$closedIssues = gh issue list `
    --state closed `
    --json number,title,updatedAt,closedAt `
    --limit 200 2>$null | ConvertFrom-Json

$activeClosedIssues = $closedIssues | Where-Object {
    $closedAt = [datetime]$_.closedAt
    $updatedAt = [datetime]$_.updatedAt
    # Closed within tracking window AND updated after being closed
    ($closedAt -gt $trackingCutoff) -and ($updatedAt -gt $closedAt)
}
Write-Host "  Found $($activeClosedIssues.Count) closed issues with post-close activity"

foreach ($issue in $activeClosedIssues) {
    $collectedIssues += @{
        number = $issue.number
        title = $issue.title
        source = "closed-with-activity"
        updatedAt = $issue.updatedAt
        closedAt = $issue.closedAt
    }
}

# 3. Check pending follow-ups from state
if (Test-Path $statePath) {
    $state = Get-Content $statePath | ConvertFrom-Json
    
    if ($state.pendingFollowUps) {
        Write-Host "Checking $($state.pendingFollowUps.Count) pending follow-ups..."
        foreach ($pending in $state.pendingFollowUps) {
            if ($pending.status -eq "pending") {
                if ($collectedIssues.number -notcontains $pending.issueNumber) {
                    $collectedIssues += @{
                        number = $pending.issueNumber
                        source = "pending-followup"
                        action = $pending.action
                    }
                }
            }
        }
    }
    
    # Check unhandled issues from previous run
    if ($state.issueSnapshots) {
        $unhandled = $state.issueSnapshots.PSObject.Properties | Where-Object {
            $snapshot = $_.Value
            $snapshot.pendingAction -and -not $snapshot.actionTaken
        }
        
        if ($unhandled) {
            Write-Host "Found $($unhandled.Count) unhandled issues from previous run"
            foreach ($prop in $unhandled) {
                $snapshot = $prop.Value
                if ($collectedIssues.number -notcontains $snapshot.number) {
                    $collectedIssues += @{
                        number = $snapshot.number
                        title = $snapshot.title
                        source = "unhandled-previous"
                        previousCategory = $snapshot.category
                    }
                }
            }
        }
    }
}

# Deduplicate by issue number
$uniqueIssues = $collectedIssues | Group-Object number | ForEach-Object {
    $_.Group | Select-Object -First 1
}

# Summary
Write-Host ""
Write-Host "=== Collection Summary ==="
Write-Host "Total unique issues: $($uniqueIssues.Count)"
Write-Host "  - Open with activity: $(($uniqueIssues | Where-Object { $_.source -eq 'open-updated' }).Count)"
Write-Host "  - Closed with activity: $(($uniqueIssues | Where-Object { $_.source -eq 'closed-with-activity' }).Count)"
Write-Host "  - Pending follow-ups: $(($uniqueIssues | Where-Object { $_.source -eq 'pending-followup' }).Count)"
Write-Host "  - Unhandled previous: $(($uniqueIssues | Where-Object { $_.source -eq 'unhandled-previous' }).Count)"

# Save results
$output = @{
    collectedAt = (Get-Date).ToUniversalTime().ToString("o")
    since = $Since
    totalCount = $uniqueIssues.Count
    issues = $uniqueIssues
}

$output | ConvertTo-Json -Depth 10 | Set-Content $OutputPath
Write-Host ""
Write-Host "Results saved to: $OutputPath"
