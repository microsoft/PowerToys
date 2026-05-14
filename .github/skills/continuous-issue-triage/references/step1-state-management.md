# Step 1: State Management

The triage skill maintains persistent state between runs to track issue activity and pending actions.

## State File Location

```
Generated Files/triage-issues/triage-state.json
```

## Initial State Creation

On first run (no existing state file), create initial state:

```powershell
# Check if state exists
$statePath = "Generated Files/triage-issues/triage-state.json"
if (-not (Test-Path $statePath)) {
    # First run - create initial state
    $initialState = @{
        version = "1.0"
        lastRun = $null
        lastRunType = $null
        issueSnapshots = @{}
        pendingFollowUps = @()
        closedWithActivity = @()
        configuration = @{
            trendingThreshold = 5
            staleWaitingDays = 14
            closedTrackingDays = 30
            labelConfidenceThreshold = 70
        }
    }
    New-Item -ItemType Directory -Force -Path (Split-Path $statePath)
    $initialState | ConvertTo-Json -Depth 10 | Set-Content $statePath
}
```

## Full State Schema

```json
{
  "version": "1.0",
  "lastRun": "2026-02-05T10:30:00Z",
  "lastRunType": "weekly",
  "issueSnapshots": {
    "12345": {
      "number": 12345,
      "title": "FancyZones: Window snapping not working",
      "state": "open",
      "labels": ["Product-FancyZones", "Issue-Bug"],
      "commentCount": 15,
      "lastCommentAt": "2026-02-04T15:30:00Z",
      "lastCommentAuthor": "user123",
      "reactions": {
        "thumbsUp": 10,
        "thumbsDown": 0,
        "heart": 2
      },
      "category": "trending",
      "categoryReason": "12 new comments since last run",
      "priorityScore": 75,
      "pendingAction": "review",
      "actionTaken": false,
      "actionTakenAt": null,
      "draftReplyPath": null,
      "linkedPRs": [],
      "firstSeenAt": "2026-01-15T...",
      "lastAnalyzedAt": "2026-02-01T..."
    }
  },
  "pendingFollowUps": [
    {
      "issueNumber": 12346,
      "action": "post-clarification",
      "scheduledFor": "2026-02-07T...",
      "draftPath": "draft-replies/issue-12346.md",
      "status": "pending"
    }
  ],
  "closedWithActivity": [
    {
      "issueNumber": 12350,
      "closedAt": "2026-01-20T...",
      "lastCheckedAt": "2026-02-05T...",
      "newCommentsSinceClosed": 2,
      "needsReview": true
    }
  ],
  "configuration": {
    "trendingThreshold": 5,
    "staleWaitingDays": 14,
    "closedTrackingDays": 30,
    "labelConfidenceThreshold": 70
  },
  "statistics": {
    "totalRunCount": 12,
    "issuesTriaged": 234,
    "repliesPosted": 45,
    "issuesClosed": 89
  }
}
```

## Loading State

```powershell
function Load-TriageState {
    param([string]$StatePath = "Generated Files/triage-issues/triage-state.json")
    
    if (Test-Path $StatePath) {
        $state = Get-Content $StatePath | ConvertFrom-Json -AsHashtable
        Write-Host "Loaded state from $($state.lastRun)"
        return $state
    }
    
    Write-Host "No previous state found - initializing fresh run"
    return $null
}
```

## Saving State

After each run, update and save the state:

```powershell
function Save-TriageState {
    param(
        [hashtable]$State,
        [string]$StatePath = "Generated Files/triage-issues/triage-state.json",
        [switch]$Archive
    )
    
    $State.lastRun = (Get-Date).ToUniversalTime().ToString("o")
    
    # Archive previous run if requested
    if ($Archive -and (Test-Path $StatePath)) {
        $archiveDate = (Get-Date).ToString("yyyy-MM-dd")
        $archivePath = "Generated Files/triage-issues/history/$archiveDate"
        New-Item -ItemType Directory -Force -Path $archivePath
        Copy-Item $StatePath "$archivePath/triage-state.json"
        
        # Also archive current-run folder
        if (Test-Path "Generated Files/triage-issues/current-run") {
            Copy-Item -Recurse "Generated Files/triage-issues/current-run" $archivePath
        }
    }
    
    $State | ConvertTo-Json -Depth 10 | Set-Content $StatePath
    Write-Host "State saved at $($State.lastRun)"
}
```

## State Transitions

### Issue Snapshot Lifecycle

```
NEW ISSUE DETECTED
       ↓
┌──────────────────┐
│ issueSnapshots   │ ← Add with initial data
│ category: null   │
└──────────────────┘
       ↓
CATEGORIZATION PASS
       ↓
┌──────────────────┐
│ category: set    │ ← trending/needs-label/etc.
│ priorityScore    │
│ pendingAction    │
└──────────────────┘
       ↓
HUMAN TAKES ACTION (external)
       ↓
┌──────────────────┐
│ actionTaken: true│ ← Mark as handled
│ actionTakenAt    │
└──────────────────┘
       ↓
NEXT RUN: RE-EVALUATE
       ↓
┌──────────────────┐
│ category: update │ ← May change category
│ reset action?    │   if new activity
└──────────────────┘
```

### Detecting Changes Between Runs

```powershell
function Get-IssueChanges {
    param(
        [hashtable]$PreviousSnapshot,
        [hashtable]$CurrentData
    )
    
    $changes = @{
        newComments = $CurrentData.commentCount - $PreviousSnapshot.commentCount
        stateChanged = $CurrentData.state -ne $PreviousSnapshot.state
        labelsChanged = (Compare-Object $PreviousSnapshot.labels $CurrentData.labels).Count -gt 0
        reactionsChanged = $CurrentData.reactions.thumbsUp -ne $PreviousSnapshot.reactions.thumbsUp
    }
    
    return $changes
}
```

## Configuration Options

| Setting | Default | Description |
|---------|---------|-------------|
| `trendingThreshold` | 5 | Minimum new comments to flag as trending |
| `staleWaitingDays` | 14 | Days waiting on author before stale |
| `closedTrackingDays` | 30 | Days to monitor closed issues for new comments |
| `labelConfidenceThreshold` | 70 | Minimum confidence % for label suggestions |

## Best Practices

1. **Always archive before overwriting**: Preserve history for audit trail
2. **Atomic updates**: Update state only after successful run completion
3. **Graceful degradation**: If state is corrupted, allow fresh start
4. **Version field**: Enables future schema migrations
