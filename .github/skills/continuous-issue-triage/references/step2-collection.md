# Step 2: Issue Collection

Collect issues that need triage attention based on activity since last run.

## Collection Strategy

### Issue Sources

1. **Recently Updated Open Issues**: Any open issue with activity since last run
2. **Closed Issues with New Comments**: People may ask questions on closed issues
3. **Previously Flagged Issues**: Issues with pending actions from last run
4. **New Issues**: Issues created since last run

## GitHub CLI Commands

### Collect Recently Updated Open Issues

```powershell
# Get open issues updated since last run
$since = "2026-01-29T00:00:00Z"  # From triage-state.json.lastRun

gh issue list `
    --state open `
    --json number,title,body,author,createdAt,updatedAt,state,labels,milestone,reactions,comments `
    --limit 500 `
    | ConvertFrom-Json `
    | Where-Object { [datetime]$_.updatedAt -gt [datetime]$since }
```

### Collect Closed Issues with Recent Activity

```powershell
# Closed issues that might have new comments
$trackingDays = 30

gh issue list `
    --state closed `
    --json number,title,updatedAt,closedAt,comments `
    --limit 200 `
    | ConvertFrom-Json `
    | Where-Object { 
        $closedDate = [datetime]$_.closedAt
        $updatedDate = [datetime]$_.updatedAt
        $cutoff = (Get-Date).AddDays(-$trackingDays)
        
        # Closed within tracking window AND updated after closed
        ($closedDate -gt $cutoff) -and ($updatedDate -gt $closedDate)
    }
```

### Full Issue Details

For each issue needing analysis, fetch complete data:

```powershell
function Get-IssueDetails {
    param([int]$IssueNumber)
    
    $issue = gh issue view $IssueNumber `
        --json number,title,body,author,createdAt,updatedAt,state,labels,milestone,reactions,comments,linkedPullRequests `
        | ConvertFrom-Json
    
    return @{
        number = $issue.number
        title = $issue.title
        body = $issue.body
        author = $issue.author.login
        state = $issue.state
        createdAt = $issue.createdAt
        updatedAt = $issue.updatedAt
        labels = $issue.labels | ForEach-Object { $_.name }
        milestone = $issue.milestone.title
        reactions = @{
            thumbsUp = ($issue.reactions | Where-Object { $_.content -eq "THUMBS_UP" }).Count
            thumbsDown = ($issue.reactions | Where-Object { $_.content -eq "THUMBS_DOWN" }).Count
            heart = ($issue.reactions | Where-Object { $_.content -eq "HEART" }).Count
        }
        commentCount = $issue.comments.Count
        comments = $issue.comments | ForEach-Object {
            @{
                author = $_.author.login
                createdAt = $_.createdAt
                body = $_.body
            }
        }
        linkedPRs = $issue.linkedPullRequests | ForEach-Object {
            @{
                number = $_.number
                title = $_.title
                state = $_.state
                mergedAt = $_.mergedAt
            }
        }
    }
}
```

## Filtering Logic

### First Run (No Previous State)

```powershell
# Collect issues from last 7 days
$lookbackDays = 7
$since = (Get-Date).AddDays(-$lookbackDays).ToUniversalTime().ToString("o")

$openIssues = gh issue list --state open --json number,updatedAt --limit 500 `
    | ConvertFrom-Json `
    | Where-Object { [datetime]$_.updatedAt -gt [datetime]$since }

Write-Host "First run: Found $($openIssues.Count) issues from last $lookbackDays days"
```

### Subsequent Runs

```powershell
function Get-IssuesToTriage {
    param(
        [hashtable]$State,
        [string]$RunType = "weekly"  # daily, twice-weekly, weekly
    )
    
    $since = [datetime]$State.lastRun
    $issues = @()
    
    # 1. Open issues updated since last run
    $openUpdated = gh issue list --state open --json number,updatedAt --limit 500 `
        | ConvertFrom-Json `
        | Where-Object { [datetime]$_.updatedAt -gt $since }
    $issues += $openUpdated
    
    # 2. Closed issues we're tracking
    foreach ($tracked in $State.closedWithActivity) {
        $issueData = gh issue view $tracked.issueNumber --json updatedAt,comments | ConvertFrom-Json
        if ([datetime]$issueData.updatedAt -gt [datetime]$tracked.lastCheckedAt) {
            $issues += @{ number = $tracked.issueNumber; source = "closed-tracking" }
        }
    }
    
    # 3. Issues with pending actions (re-check status)
    foreach ($pending in $State.pendingFollowUps) {
        if ($pending.status -eq "pending") {
            $issues += @{ number = $pending.issueNumber; source = "pending-action" }
        }
    }
    
    # 4. Issues previously categorized but action not taken
    foreach ($snapshot in $State.issueSnapshots.Values) {
        if ($snapshot.pendingAction -and -not $snapshot.actionTaken) {
            if ($issues.number -notcontains $snapshot.number) {
                $issues += @{ number = $snapshot.number; source = "unhandled" }
            }
        }
    }
    
    return $issues | Sort-Object -Property number -Unique
}
```

## Comment Analysis

For trending detection, analyze comment activity:

```powershell
function Get-CommentDelta {
    param(
        [int]$IssueNumber,
        [hashtable]$PreviousSnapshot
    )
    
    $current = gh issue view $IssueNumber --json comments | ConvertFrom-Json
    
    $previousCount = if ($PreviousSnapshot) { $PreviousSnapshot.commentCount } else { 0 }
    $previousLastComment = if ($PreviousSnapshot) { $PreviousSnapshot.lastCommentAt } else { $null }
    
    $newComments = $current.comments | Where-Object {
        -not $previousLastComment -or [datetime]$_.createdAt -gt [datetime]$previousLastComment
    }
    
    return @{
        totalComments = $current.comments.Count
        newCommentCount = $newComments.Count
        newComments = $newComments | ForEach-Object {
            @{
                author = $_.author.login
                createdAt = $_.createdAt
                bodyPreview = $_.body.Substring(0, [Math]::Min(200, $_.body.Length))
            }
        }
        lastCommentAt = ($current.comments | Sort-Object createdAt -Descending | Select-Object -First 1).createdAt
        lastCommentAuthor = ($current.comments | Sort-Object createdAt -Descending | Select-Object -First 1).author.login
    }
}
```

## Output Format

Save collected issues to working file:

```powershell
$collectedIssues | ConvertTo-Json -Depth 10 | Set-Content "Generated Files/triage-issues/current-run/collected-issues.json"
```

## Rate Limiting

GitHub API has rate limits. For large backlogs:

```powershell
# Check rate limit
gh api rate_limit --jq '.resources.core'

# Batch requests with delay if needed
$batchSize = 50
$delaySeconds = 2

for ($i = 0; $i -lt $issues.Count; $i += $batchSize) {
    $batch = $issues[$i..([Math]::Min($i + $batchSize - 1, $issues.Count - 1))]
    # Process batch...
    Start-Sleep -Seconds $delaySeconds
}
```

## Next Step

After collection, proceed to [Step 3: Categorization](./step3-categorization.md).
