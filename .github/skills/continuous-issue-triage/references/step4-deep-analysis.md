# Step 4: Deep Analysis

For issues requiring detailed analysis, leverage the `review-issue` prompt to generate comprehensive reviews.

## When to Run Deep Analysis

| Category | Deep Analysis? | Reason |
|----------|---------------|--------|
| Trending | Optional | If conversation is contentious |
| Needs-Label | No | Label detection is keyword-based |
| Ready-for-Fix | Yes (cached) | Need scores for validation |
| Needs-Info | Optional | To identify specific gaps |
| Needs-Clarification | No | Simple question detection |
| Closeable | No | Mechanical check |
| Stale-Waiting | No | Time-based |
| Duplicate-Candidate | Optional | Similar issue search |

## Integration with review-issue Prompt

The `review-issue` prompt generates two artifacts:
- `overview.md` - Scoring, signals, suggested actions
- `implementation-plan.md` - Technical breakdown

### Invoking the Prompt

```markdown
# Within the agent's execution, reference the prompt:

For issue #{{issue_number}}, I need detailed analysis.

Use the review-issue prompt at `.github/prompts/review-issue.prompt.md` to generate:
1. `Generated Files/triage-issues/issue-cache/{{issue_number}}/overview.md`
2. `Generated Files/triage-issues/issue-cache/{{issue_number}}/implementation-plan.md`
```

### Caching Strategy

```
Generated Files/triage-issues/issue-cache/
├── 12345/
│   ├── overview.md
│   ├── implementation-plan.md
│   └── metadata.json
└── 12346/
    └── ...
```

**metadata.json**:
```json
{
  "issueNumber": 12345,
  "analyzedAt": "2026-02-05T10:30:00Z",
  "issueUpdatedAt": "2026-02-04T15:30:00Z",
  "commentCountAtAnalysis": 15,
  "isStale": false
}
```

### Cache Invalidation

Re-run analysis if:
1. Issue has new comments since last analysis
2. Issue state changed (open ↔ closed)
3. Labels changed significantly
4. More than 7 days since last analysis
5. User explicitly requests refresh

```powershell
function Test-CacheValid {
    param(
        [int]$IssueNumber,
        [hashtable]$CurrentIssueData
    )
    
    $cachePath = "Generated Files/triage-issues/issue-cache/$IssueNumber"
    $metadataPath = "$cachePath/metadata.json"
    
    if (-not (Test-Path $metadataPath)) {
        return @{ valid = $false; reason = "No cached analysis" }
    }
    
    $metadata = Get-Content $metadataPath | ConvertFrom-Json
    
    # Check freshness
    $daysSinceAnalysis = ((Get-Date) - [datetime]$metadata.analyzedAt).Days
    if ($daysSinceAnalysis -gt 7) {
        return @{ valid = $false; reason = "Cache older than 7 days" }
    }
    
    # Check for new comments
    if ($CurrentIssueData.commentCount -gt $metadata.commentCountAtAnalysis) {
        return @{ valid = $false; reason = "New comments added" }
    }
    
    # Check for state change
    if ($CurrentIssueData.updatedAt -gt $metadata.issueUpdatedAt) {
        return @{ valid = $false; reason = "Issue updated since analysis" }
    }
    
    return @{ valid = $true }
}
```

## Selective Analysis

Don't analyze every issue - be selective:

### Batch 1: High-Priority Analysis

Analyze first:
- Trending issues with negative sentiment
- Potential ready-for-fix candidates (unclear if ready)
- Issues with high reaction counts (>10 👍)

### Batch 2: Moderate Priority

Analyze if time permits:
- Needs-Info issues (to draft better questions)
- Complex duplicate candidates

### Batch 3: Skip Analysis

Don't analyze:
- Clear closeable issues
- Stale-waiting issues
- Already-analyzed recent issues

## Extracting Scores from Analysis

After running `review-issue`, parse the `overview.md`:

```powershell
function Get-AnalysisScores {
    param([string]$OverviewPath)
    
    $content = Get-Content $OverviewPath -Raw
    
    # Extract from the At-a-Glance Score Table
    $scores = @{}
    
    # Business Importance
    if ($content -match '\*\*A\) Business Importance\*\*.*?(\d+)/100') {
        $scores.businessImportance = [int]$Matches[1]
    }
    
    # Community Excitement
    if ($content -match '\*\*B\) Community Excitement\*\*.*?(\d+)/100') {
        $scores.communityExcitement = [int]$Matches[1]
    }
    
    # Technical Feasibility
    if ($content -match '\*\*C\) Technical Feasibility\*\*.*?(\d+)/100') {
        $scores.technicalFeasibility = [int]$Matches[1]
    }
    
    # Requirement Clarity
    if ($content -match '\*\*D\) Requirement Clarity\*\*.*?(\d+)/100') {
        $scores.requirementClarity = [int]$Matches[1]
    }
    
    # Overall Priority
    if ($content -match '\*\*Overall Priority\*\*.*?(\d+)/100') {
        $scores.overallPriority = [int]$Matches[1]
    }
    
    # Effort Estimate
    if ($content -match '\*\*Effort Estimate\*\*.*?(\d+) days.*?(XS|S|M|L|XL|XXL|Epic)') {
        $scores.effortDays = [int]$Matches[1]
        $scores.effortTShirt = $Matches[2]
    }
    
    return $scores
}
```

## Similar Issue Search

For duplicate detection, search existing issues:

```powershell
function Find-SimilarIssues {
    param([hashtable]$Issue)
    
    # Extract key terms from title
    $searchTerms = $Issue.title -split '\s+' | Where-Object { $_.Length -gt 3 }
    $searchQuery = ($searchTerms | Select-Object -First 5) -join ' '
    
    # Search both open and closed
    $similar = gh issue list `
        --search "$searchQuery" `
        --state all `
        --json number,title,state,closedAt,labels `
        --limit 10 `
        | ConvertFrom-Json `
        | Where-Object { $_.number -ne $Issue.number }
    
    # Score similarity
    $results = $similar | ForEach-Object {
        $similarity = Get-TitleSimilarity $Issue.title $_.title
        @{
            number = $_.number
            title = $_.title
            state = $_.state
            closedAt = $_.closedAt
            similarityScore = $similarity
        }
    } | Where-Object { $_.similarityScore -gt 50 } | Sort-Object similarityScore -Descending
    
    return $results
}

function Get-TitleSimilarity {
    param(
        [string]$Title1,
        [string]$Title2
    )
    
    $words1 = $Title1.ToLower() -split '\W+' | Where-Object { $_.Length -gt 2 }
    $words2 = $Title2.ToLower() -split '\W+' | Where-Object { $_.Length -gt 2 }
    
    $common = ($words1 | Where-Object { $words2 -contains $_ }).Count
    $total = [Math]::Max($words1.Count, $words2.Count)
    
    if ($total -eq 0) { return 0 }
    
    return [int](($common / $total) * 100)
}
```

## MCP Tools for Rich Context

When available, use MCP tools for additional context:

### Images (UI issues)

```markdown
If the issue mentions screenshots or UI problems, use MCP:

github_issue_images(owner: "microsoft", repo: "PowerToys", issueNumber: 12345)
```

### Attachments (Logs)

```markdown
If the issue mentions logs or diagnostic reports:

github_issue_attachments(
    owner: "microsoft", 
    repo: "PowerToys", 
    issueNumber: 12345,
    extractFolder: "Generated Files/triage-issues/issue-cache/12345/logs"
)
```

## Analysis Output

Save analysis metadata for state tracking:

```powershell
$metadata = @{
    issueNumber = $Issue.number
    analyzedAt = (Get-Date).ToUniversalTime().ToString("o")
    issueUpdatedAt = $Issue.updatedAt
    commentCountAtAnalysis = $Issue.commentCount
    scores = $extractedScores
    suggestedCategory = $determinedCategory
}

$metadata | ConvertTo-Json | Set-Content "$cachePath/metadata.json"
```

## Next Step

Proceed to [Step 5: Report Generation](./step5-reports.md).
