<#
.SYNOPSIS
    Get the current status of issues/PRs in the issue-to-PR cycle.

.DESCRIPTION
    Checks the status of:
    - Issue review completion (has overview.md + implementation-plan.md)
    - Issue fix completion (has worktree + commits)
    - PR creation status (has open PR)
    - PR review status (has review files)
    - PR active comments count

.PARAMETER IssueNumbers
    Array of issue numbers to check status for.

.PARAMETER PRNumbers
    Array of PR numbers to check status for.

.PARAMETER CheckAll
    Check all issues with review data and all open PRs with issue/* branches.

.EXAMPLE
    ./Get-CycleStatus.ps1 -IssueNumbers 44044, 32950

.EXAMPLE
    ./Get-CycleStatus.ps1 -PRNumbers 45234, 45235

.EXAMPLE
    ./Get-CycleStatus.ps1 -CheckAll
#>

[CmdletBinding()]
param(
    [int[]]$IssueNumbers = @(),
    [int[]]$PRNumbers = @(),
    [switch]$CheckAll,
    [switch]$JsonOutput
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptDir 'IssueReviewLib.ps1')

$repoRoot = Get-RepoRoot
$genFiles = Get-GeneratedFilesPath -RepoRoot $repoRoot
$worktreeLib = Join-Path $repoRoot 'tools/build/WorktreeLib.ps1'
if (Test-Path $worktreeLib) {
    . $worktreeLib
}

function Get-IssueStatus {
    param([int]$IssueNumber)
    
    $status = @{
        IssueNumber = $IssueNumber
        HasReview = $false
        HasImplementationPlan = $false
        FeasibilityScore = 0
        ClarityScore = 0
        EffortDays = 0
        HasWorktree = $false
        WorktreePath = $null
        HasCommits = $false
        CommitCount = 0
        HasPR = $false
        PRNumber = 0
        PRState = $null
        PRUrl = $null
        ReviewSignalStatus = $null
        ReviewSignalTimestamp = $null
        FixSignalStatus = $null
        FixSignalTimestamp = $null
    }
    
    # Check review status
    $reviewDir = Join-Path $genFiles "issueReview/$IssueNumber"
    $overviewPath = Join-Path $reviewDir 'overview.md'
    $implPlanPath = Join-Path $reviewDir 'implementation-plan.md'
    
    if (Test-Path $overviewPath) {
        $status.HasReview = $true
        $overview = Get-Content $overviewPath -Raw
        
        if ($overview -match 'Technical Feasibility[^\d]*(\d+)/100') {
            $status.FeasibilityScore = [int]$Matches[1]
        }
        if ($overview -match 'Requirement Clarity[^\d]*(\d+)/100') {
            $status.ClarityScore = [int]$Matches[1]
        }
        if ($overview -match 'Effort Estimate[^|]*\|\s*[\d.]+(?:-(\d+))?\s*days?') {
            $status.EffortDays = if ($Matches[1]) { [int]$Matches[1] } else { 1 }
        }
    }
    
    if (Test-Path $implPlanPath) {
        $status.HasImplementationPlan = $true
    }

    # Check review signal
    $reviewSignalPath = Join-Path $reviewDir '.signal'
    if (Test-Path $reviewSignalPath) {
        try {
            $reviewSignal = Get-Content $reviewSignalPath -Raw | ConvertFrom-Json
            $status.ReviewSignalStatus = $reviewSignal.status
            $status.ReviewSignalTimestamp = $reviewSignal.timestamp
        }
        catch {}
    }
    
    # Check worktree status
    $worktrees = Get-WorktreeEntries | Where-Object { $_.Branch -like "issue/$IssueNumber*" }
    if ($worktrees) {
        $status.HasWorktree = $true
        $status.WorktreePath = $worktrees[0].Path
        
        # Check for commits
        Push-Location $status.WorktreePath
        try {
            $commits = git log --oneline "main..HEAD" 2>$null
            if ($commits) {
                $status.HasCommits = $true
                $status.CommitCount = @($commits).Count
            }
        }
        finally {
            Pop-Location
        }
    }

    # Check fix signal
    $fixSignalPath = Join-Path $genFiles "issueFix/$IssueNumber/.signal"
    if (Test-Path $fixSignalPath) {
        try {
            $fixSignal = Get-Content $fixSignalPath -Raw | ConvertFrom-Json
            $status.FixSignalStatus = $fixSignal.status
            $status.FixSignalTimestamp = $fixSignal.timestamp
        }
        catch {}
    }
    
    # Check PR status
    $prs = gh pr list --head "issue/$IssueNumber" --state all --json number,url,state 2>$null | ConvertFrom-Json
    if (-not $prs -or $prs.Count -eq 0) {
        # Try searching by issue reference
        $prs = gh pr list --search "fixes #$IssueNumber OR closes #$IssueNumber" --state all --json number,url,state --limit 1 2>$null | ConvertFrom-Json
    }
    if ($prs -and $prs.Count -gt 0) {
        $status.HasPR = $true
        $status.PRNumber = $prs[0].number
        $status.PRState = $prs[0].state
        $status.PRUrl = $prs[0].url
    }
    
    return $status
}

function Get-PRStatus {
    param([int]$PRNumber)
    
    $status = @{
        PRNumber = $PRNumber
        State = $null
        IssueNumber = 0
        Branch = $null
        HasReviewFiles = $false
        ReviewStepCount = 0
        HighSeverityCount = 0
        MediumSeverityCount = 0
        ActiveCommentCount = 0
        UnresolvedThreadCount = 0
        CopilotReviewRequested = $false
        ReviewSignalStatus = $null
        ReviewSignalTimestamp = $null
        FixSignalStatus = $null
        FixSignalTimestamp = $null
    }
    
    # Get PR info
    $prInfo = gh pr view $PRNumber --json state,headRefName,number 2>$null | ConvertFrom-Json
    if (-not $prInfo) {
        return $status
    }
    
    $status.State = $prInfo.state
    $status.Branch = $prInfo.headRefName
    
    # Extract issue number from branch
    if ($status.Branch -match 'issue/(\d+)') {
        $status.IssueNumber = [int]$Matches[1]
    }
    
    # Check review files
    $reviewDir = Join-Path $genFiles "prReview/$PRNumber"
    if (Test-Path $reviewDir) {
        $status.HasReviewFiles = $true
        $stepFiles = Get-ChildItem -Path $reviewDir -Filter "*.md" -ErrorAction SilentlyContinue | 
                     Where-Object { $_.Name -match '^\d{2}-' }
        $status.ReviewStepCount = $stepFiles.Count
        
        # Count severity issues
        foreach ($stepFile in $stepFiles) {
            $content = Get-Content $stepFile.FullName -Raw -ErrorAction SilentlyContinue
            if ($content) {
                $status.HighSeverityCount += ([regex]::Matches($content, '\*\*Severity:\s*high\*\*', 'IgnoreCase')).Count
                $status.HighSeverityCount += ([regex]::Matches($content, '🔴\s*High', 'IgnoreCase')).Count
                $status.MediumSeverityCount += ([regex]::Matches($content, '\*\*Severity:\s*medium\*\*', 'IgnoreCase')).Count
                $status.MediumSeverityCount += ([regex]::Matches($content, '🟡\s*Medium', 'IgnoreCase')).Count
            }
        }
    }

    # Check review signal
    $reviewSignalPath = Join-Path $reviewDir '.signal'
    if (Test-Path $reviewSignalPath) {
        try {
            $reviewSignal = Get-Content $reviewSignalPath -Raw | ConvertFrom-Json
            $status.ReviewSignalStatus = $reviewSignal.status
            $status.ReviewSignalTimestamp = $reviewSignal.timestamp
        }
        catch {}
    }

    # Check fix signal
    $fixSignalPath = Join-Path $genFiles "prFix/$PRNumber/.signal"
    if (Test-Path $fixSignalPath) {
        try {
            $fixSignal = Get-Content $fixSignalPath -Raw | ConvertFrom-Json
            $status.FixSignalStatus = $fixSignal.status
            $status.FixSignalTimestamp = $fixSignal.timestamp
        }
        catch {}
    }
    
    # Get active comments (not in reply to another comment)
    try {
        $commentCount = gh api "repos/microsoft/PowerToys/pulls/$PRNumber/comments" --jq '[.[] | select(.in_reply_to_id == null)] | length' 2>$null
        $status.ActiveCommentCount = [int]$commentCount
    }
    catch {
        $status.ActiveCommentCount = 0
    }
    
    # Get unresolved thread count
    try {
        $threads = gh api graphql -f query="query { repository(owner: `"microsoft`", name: `"PowerToys`") { pullRequest(number: $PRNumber) { reviewThreads(first: 100) { nodes { isResolved } } } } }" --jq '.data.repository.pullRequest.reviewThreads.nodes | map(select(.isResolved == false)) | length' 2>$null
        $status.UnresolvedThreadCount = [int]$threads
    }
    catch {
        $status.UnresolvedThreadCount = 0
    }
    
    # Check if Copilot review was requested
    try {
        $reviewers = gh pr view $PRNumber --json reviewRequests --jq '.reviewRequests[].login' 2>$null
        if ($reviewers -contains 'copilot' -or $reviewers -contains 'github-copilot') {
            $status.CopilotReviewRequested = $true
        }
    }
    catch {}
    
    return $status
}

# Main execution
$results = @{
    Issues = @()
    PRs = @()
    Timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
}

# Gather issue numbers to check
$issuesToCheck = @()
$prsToCheck = @()

if ($CheckAll) {
    # Get all reviewed issues
    $reviewDir = Join-Path $genFiles 'issueReview'
    if (Test-Path $reviewDir) {
        $issuesToCheck = Get-ChildItem -Path $reviewDir -Directory |
            Where-Object { $_.Name -match '^\d+$' } |
            ForEach-Object { [int]$_.Name }
    }
    
    # Get all open PRs with issue/* branches
    $openPRs = gh pr list --state open --json number,headRefName 2>$null | ConvertFrom-Json |
               Where-Object { $_.headRefName -like 'issue/*' }
    $prsToCheck = @($openPRs | ForEach-Object { $_.number })
}
else {
    $issuesToCheck = $IssueNumbers
    $prsToCheck = $PRNumbers
}

# Get issue statuses
foreach ($issueNum in $issuesToCheck) {
    $status = Get-IssueStatus -IssueNumber $issueNum
    $results.Issues += $status
}

# Get PR statuses
foreach ($prNum in $prsToCheck) {
    $status = Get-PRStatus -PRNumber $prNum
    $results.PRs += $status
}

# Output
if ($JsonOutput) {
    $results | ConvertTo-Json -Depth 5
    return $results
}
else {
    if ($results.Issues.Count -gt 0) {
        Write-Host "`n=== ISSUE STATUS ===" -ForegroundColor Cyan
        Write-Host ("-" * 100)
        Write-Host ("{0,-8} {1,-8} {2,-8} {3,-5} {4,-5} {5,-10} {6,-8} {7,-8} {8,-8} {9,-8}" -f "Issue", "Review", "Plan", "Feas", "Clar", "Worktree", "Commits", "PR", "RevSig", "FixSig")
        Write-Host ("-" * 100)
        foreach ($issue in $results.Issues | Sort-Object IssueNumber) {
            $reviewMark = if ($issue.HasReview) { "✓" } else { "-" }
            $planMark = if ($issue.HasImplementationPlan) { "✓" } else { "-" }
            $wtMark = if ($issue.HasWorktree) { "✓" } else { "-" }
            $commitMark = if ($issue.HasCommits) { $issue.CommitCount } else { "-" }
            $prMark = if ($issue.HasPR) { "#$($issue.PRNumber) ($($issue.PRState))" } else { "-" }
            $reviewSignalMark = if ($issue.ReviewSignalStatus) { $issue.ReviewSignalStatus } else { "-" }
            $fixSignalMark = if ($issue.FixSignalStatus) { $issue.FixSignalStatus } else { "-" }
            
            Write-Host ("{0,-8} {1,-8} {2,-8} {3,-5} {4,-5} {5,-10} {6,-8} {7,-8} {8,-8} {9,-8}" -f 
                "#$($issue.IssueNumber)", $reviewMark, $planMark, $issue.FeasibilityScore, $issue.ClarityScore, $wtMark, $commitMark, $prMark, $reviewSignalMark, $fixSignalMark)
        }
    }
    
    if ($results.PRs.Count -gt 0) {
        Write-Host "`n=== PR STATUS ===" -ForegroundColor Cyan
        Write-Host ("-" * 120)
        Write-Host ("{0,-8} {1,-10} {2,-10} {3,-8} {4,-8} {5,-10} {6,-12} {7,-10} {8,-8} {9,-8}" -f "PR", "State", "Issue", "Reviews", "High", "Medium", "Comments", "Unresolved", "RevSig", "FixSig")
        Write-Host ("-" * 120)
        foreach ($pr in $results.PRs | Sort-Object PRNumber) {
            $reviewMark = if ($pr.HasReviewFiles) { "$($pr.ReviewStepCount) steps" } else { "-" }
            $issueMark = if ($pr.IssueNumber -gt 0) { "#$($pr.IssueNumber)" } else { "-" }
            $reviewSignalMark = if ($pr.ReviewSignalStatus) { $pr.ReviewSignalStatus } else { "-" }
            $fixSignalMark = if ($pr.FixSignalStatus) { $pr.FixSignalStatus } else { "-" }
            
            Write-Host ("{0,-8} {1,-10} {2,-10} {3,-8} {4,-8} {5,-10} {6,-12} {7,-10} {8,-8} {9,-8}" -f 
                "#$($pr.PRNumber)", $pr.State, $issueMark, $reviewMark, $pr.HighSeverityCount, $pr.MediumSeverityCount, $pr.ActiveCommentCount, $pr.UnresolvedThreadCount, $reviewSignalMark, $fixSignalMark)
        }
    }
    
    Write-Host "`nTimestamp: $($results.Timestamp)" -ForegroundColor Gray
}

return $results
