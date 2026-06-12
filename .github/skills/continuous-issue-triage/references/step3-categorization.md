# Step 3: Categorization Rules

Apply categorization rules to assign each issue to an actionable bucket.

## Category Definitions

| Category | ID | Priority | Criteria |
|----------|-----|----------|----------|
| 🔥 **Trending** | `trending` | 1 | 5+ new comments since last run |
| 🏷️ **Needs-Label** | `needs-label` | 2 | Missing `Product-*` or `Area-*` label |
| ✅ **Ready-for-Fix** | `ready-for-fix` | 3 | High clarity (≥70), feasible (≥60), validated |
| ❓ **Needs-Info** | `needs-info` | 4 | Missing repro, impact, or expected result |
| 💬 **Needs-Clarification** | `needs-clarification` | 5 | Question/discussion, not actionable bug |
| ✔️ **Closeable** | `closeable` | 6 | Fixed by merged PR, or released, or resolved |
| ⏳ **Stale-Waiting** | `stale-waiting` | 7 | Waiting on author >14 days after ask |
| 🔁 **Duplicate-Candidate** | `duplicate-candidate` | 8 | Likely duplicate of existing issue |

## Categorization Algorithm

```
FOR EACH issue in collected_issues:
    
    # Priority order - first match wins
    
    1. CHECK TRENDING
       IF new_comments >= 5:
           category = "trending"
           CONTINUE
    
    2. CHECK CLOSEABLE
       IF has_merged_PR AND PR_in_released_version:
           category = "closeable"
           reason = "Fixed in PR #X, released in vY.Z"
           CONTINUE
       IF state == "open" AND all_linked_PRs_merged:
           category = "closeable"
           reason = "All linked PRs merged"
           CONTINUE
    
    3. CHECK NEEDS-LABEL
       IF missing_product_or_area_label:
           category = "needs-label"
           suggested_label = analyze_content()
           CONTINUE
    
    4. CHECK STALE-WAITING
       IF has_label("Needs-Author-Feedback"):
           IF days_since_last_author_response > 14:
               category = "stale-waiting"
               CONTINUE
    
    5. CHECK NEEDS-CLARIFICATION (question, not bug)
       IF is_question_not_bug():
           category = "needs-clarification"
           draft_reply = generate_explanation()
           CONTINUE
    
    6. CHECK NEEDS-INFO
       IF missing_repro_steps OR missing_expected_result OR missing_version:
           category = "needs-info"
           missing_items = identify_gaps()
           draft_questions = generate_questions()
           CONTINUE
    
    7. CHECK READY-FOR-FIX
       IF clarity_score >= 70 AND feasibility_score >= 60:
           category = "ready-for-fix"
           CONTINUE
    
    8. CHECK DUPLICATE
       IF similar_issues_found AND confidence > 80:
           category = "duplicate-candidate"
           duplicate_of = [similar_issue_numbers]
           CONTINUE
    
    9. DEFAULT
       category = "review-needed"
       # Needs human judgment
```

## Category Rule Details

### 🔥 Trending Detection

```powershell
function Test-Trending {
    param(
        [hashtable]$Issue,
        [hashtable]$PreviousSnapshot,
        [int]$Threshold = 5
    )
    
    $previousCount = if ($PreviousSnapshot) { $PreviousSnapshot.commentCount } else { 0 }
    $newComments = $Issue.commentCount - $previousCount
    
    if ($newComments -ge $Threshold) {
        return @{
            isTrending = $true
            newCommentCount = $newComments
            reason = "$newComments new comments since last triage"
            sentiment = Get-CommentSentiment $Issue.comments  # Optional
        }
    }
    
    return @{ isTrending = $false }
}
```

### 🏷️ Label Analysis

```powershell
function Test-NeedsLabel {
    param([hashtable]$Issue)
    
    $productLabels = $Issue.labels | Where-Object { $_ -like "Product-*" }
    $areaLabels = $Issue.labels | Where-Object { $_ -like "Area-*" }
    
    if ($productLabels.Count -eq 0 -and $areaLabels.Count -eq 0) {
        # Analyze content to suggest label
        $suggestion = Get-LabelSuggestion $Issue
        
        return @{
            needsLabel = $true
            missingType = "product-or-area"
            suggestedLabels = $suggestion.labels
            confidence = $suggestion.confidence
            reason = $suggestion.reason
        }
    }
    
    return @{ needsLabel = $false }
}

function Get-LabelSuggestion {
    param([hashtable]$Issue)
    
    # Keyword mapping to products
    $productKeywords = @{
        "Product-FancyZones" = @("fancy zones", "fancyzones", "zone", "snap", "layout", "window arrangement")
        "Product-PowerToys Run" = @("run", "launcher", "alt+space", "search", "plugin")
        "Product-Color Picker" = @("color picker", "colorpicker", "eyedropper", "hex", "rgb")
        "Product-Keyboard Manager" = @("keyboard", "remap", "shortcut", "key")
        "Product-Mouse Utils" = @("mouse", "crosshairs", "find my mouse", "highlighter", "pointer")
        "Product-File Explorer" = @("file explorer", "preview", "thumbnail", "markdown preview", "svg")
        "Product-Image Resizer" = @("image resizer", "resize", "bulk resize")
        "Product-PowerRename" = @("rename", "power rename", "bulk rename", "regex rename")
        "Product-Awake" = @("awake", "keep awake", "prevent sleep", "caffeinate")
        "Product-Shortcut Guide" = @("shortcut guide", "win key", "keyboard shortcuts")
        "Product-Text Extractor" = @("text extractor", "ocr", "screen text", "copy text from screen")
        "Product-Hosts File Editor" = @("hosts", "hosts file", "dns")
        "Product-Peek" = @("peek", "quick preview", "spacebar preview")
        "Product-Crop And Lock" = @("crop", "crop and lock", "window crop")
        "Product-Paste As Plain Text" = @("paste", "plain text", "paste as")
        "Product-Registry Preview" = @("registry", "reg file", "registry preview")
        "Product-Environment Variables" = @("environment", "env", "variables", "path")
        "Product-Command Not Found" = @("command not found", "winget suggest")
        "Product-New+" = @("new+", "new plus", "file template")
        "Product-Advanced Paste" = @("advanced paste", "ai paste", "clipboard")
        "Product-Workspaces" = @("workspaces", "workspace", "project launcher")
        "Product-Cmd Palette" = @("command palette", "cmd palette", "palette")
        "Product-ZoomIt" = @("zoomit", "zoom it", "screen zoom", "magnifier")
    }
    
    $titleLower = $Issue.title.ToLower()
    $bodyLower = if ($Issue.body) { $Issue.body.ToLower() } else { "" }
    $combined = "$titleLower $bodyLower"
    
    $matches = @()
    foreach ($product in $productKeywords.Keys) {
        $keywords = $productKeywords[$product]
        $matchCount = ($keywords | Where-Object { $combined -match $_ }).Count
        if ($matchCount -gt 0) {
            $matches += @{
                label = $product
                matchCount = $matchCount
                confidence = [Math]::Min(100, $matchCount * 25 + 25)
            }
        }
    }
    
    $best = $matches | Sort-Object confidence -Descending | Select-Object -First 1
    
    if ($best -and $best.confidence -ge 50) {
        return @{
            labels = @($best.label)
            confidence = $best.confidence
            reason = "Matched $($best.matchCount) keywords for $($best.label)"
        }
    }
    
    return @{
        labels = @()
        confidence = 0
        reason = "No confident label match - needs human review"
    }
}
```

### ✅ Ready-for-Fix Detection

Leverage the `review-issue` prompt scores:

```powershell
function Test-ReadyForFix {
    param(
        [hashtable]$Issue,
        [string]$CachePath = "Generated Files/triage-issues/issue-cache"
    )
    
    $overviewPath = "$CachePath/$($Issue.number)/overview.md"
    
    if (-not (Test-Path $overviewPath)) {
        # Need to run deep analysis first
        return @{ needsAnalysis = $true }
    }
    
    # Parse scores from cached overview
    $overview = Get-Content $overviewPath -Raw
    $clarityScore = [regex]::Match($overview, 'Requirement Clarity.*?(\d+)/100').Groups[1].Value
    $feasibilityScore = [regex]::Match($overview, 'Technical Feasibility.*?(\d+)/100').Groups[1].Value
    
    if ([int]$clarityScore -ge 70 -and [int]$feasibilityScore -ge 60) {
        return @{
            readyForFix = $true
            clarityScore = [int]$clarityScore
            feasibilityScore = [int]$feasibilityScore
            reason = "High clarity ($clarityScore) and feasible ($feasibilityScore)"
        }
    }
    
    return @{ readyForFix = $false }
}
```

### ❓ Needs-Info Detection

```powershell
function Test-NeedsInfo {
    param([hashtable]$Issue)
    
    $missingItems = @()
    $body = $Issue.body
    
    # Check for repro steps
    if ($body -notmatch '(?i)(steps to reproduce|repro|how to reproduce|reproduction)') {
        $missingItems += "reproduction steps"
    }
    
    # Check for expected result
    if ($body -notmatch '(?i)(expected|should|supposed to)') {
        $missingItems += "expected behavior"
    }
    
    # Check for version
    if ($body -notmatch '(?i)(version|v\d+\.\d+|\d+\.\d+\.\d+)') {
        $missingItems += "PowerToys version"
    }
    
    # Check for OS version
    if ($body -notmatch '(?i)(windows 1[01]|win1[01]|22h2|23h2|24h2|build \d+)') {
        $missingItems += "Windows version"
    }
    
    # Check for actual result (for bugs)
    if ($Issue.labels -contains "Issue-Bug") {
        if ($body -notmatch '(?i)(actual|instead|but|however|currently)') {
            $missingItems += "actual behavior/result"
        }
    }
    
    if ($missingItems.Count -gt 0) {
        return @{
            needsInfo = $true
            missingItems = $missingItems
            reason = "Missing: " + ($missingItems -join ", ")
        }
    }
    
    return @{ needsInfo = $false }
}
```

### 💬 Needs-Clarification (Not a Bug)

```powershell
function Test-NeedsClarification {
    param([hashtable]$Issue)
    
    $questionPatterns = @(
        '(?i)^(how (do|can|to)|why (does|is|doesn''t)|is (it|there|this) (possible|a way))',
        '(?i)\?$',  # Ends with question mark
        '(?i)(wondering|curious|question|asking)',
        '(?i)(is this (intended|by design|expected))',
        '(?i)(can (someone|you) (explain|help))'
    )
    
    $titleAndBody = $Issue.title + " " + $Issue.body
    
    $isQuestion = $false
    foreach ($pattern in $questionPatterns) {
        if ($titleAndBody -match $pattern) {
            $isQuestion = $true
            break
        }
    }
    
    # Also check if explicitly marked as question
    if ($Issue.labels -contains "Issue-Question" -or $Issue.labels -contains "Type-Question") {
        $isQuestion = $true
    }
    
    if ($isQuestion -and ($Issue.labels -notcontains "Issue-Bug")) {
        return @{
            needsClarification = $true
            type = "question"
            reason = "Appears to be a question/inquiry rather than bug report"
        }
    }
    
    return @{ needsClarification = $false }
}
```

### ✔️ Closeable Detection

```powershell
function Test-Closeable {
    param([hashtable]$Issue)
    
    $closeReasons = @()
    
    # Check for merged linked PRs
    $mergedPRs = $Issue.linkedPRs | Where-Object { $_.state -eq "MERGED" }
    if ($mergedPRs.Count -gt 0) {
        $closeReasons += @{
            type = "fixed-by-pr"
            prNumbers = $mergedPRs.number
            reason = "Fixed by PR(s): #" + ($mergedPRs.number -join ", #")
        }
    }
    
    # Check comments for "fixed in" or "released in"
    $recentComments = $Issue.comments | Sort-Object createdAt -Descending | Select-Object -First 5
    foreach ($comment in $recentComments) {
        if ($comment.body -match '(?i)(fixed in|released in|available in|shipped in) v?(\d+\.\d+)') {
            $version = $Matches[2]
            $closeReasons += @{
                type = "released"
                version = $version
                reason = "Released in v$version"
            }
            break
        }
    }
    
    # Check if marked as duplicate
    if ($Issue.labels -contains "Resolution-Duplicate") {
        $closeReasons += @{
            type = "duplicate"
            reason = "Marked as duplicate"
        }
    }
    
    # Check if marked as won't fix
    if ($Issue.labels -contains "Resolution-Won't Fix" -or $Issue.labels -contains "Resolution-By-Design") {
        $closeReasons += @{
            type = "wont-fix"
            reason = "Marked as won't fix / by design"
        }
    }
    
    if ($closeReasons.Count -gt 0) {
        return @{
            closeable = $true
            reasons = $closeReasons
        }
    }
    
    return @{ closeable = $false }
}
```

## Priority Scoring

Combine signals for overall priority within category:

```powershell
function Get-PriorityScore {
    param([hashtable]$Issue)
    
    $score = 50  # Base score
    
    # Reaction boost
    $thumbsUp = $Issue.reactions.thumbsUp
    $score += [Math]::Min(20, $thumbsUp * 2)
    
    # Comment engagement
    $score += [Math]::Min(15, $Issue.commentCount)
    
    # Recency boost (updated recently)
    $daysSinceUpdate = ((Get-Date) - [datetime]$Issue.updatedAt).Days
    if ($daysSinceUpdate -le 7) { $score += 10 }
    elseif ($daysSinceUpdate -le 30) { $score += 5 }
    
    # Label boosts
    if ($Issue.labels -contains "Priority-High") { $score += 15 }
    if ($Issue.labels -match "Regression") { $score += 20 }
    if ($Issue.labels -match "Security") { $score += 25 }
    
    return [Math]::Min(100, $score)
}
```

## Output

Save categorization results:

```json
{
  "12345": {
    "category": "trending",
    "categoryReason": "8 new comments since last run",
    "priorityScore": 82,
    "additionalFlags": ["negative-sentiment"],
    "suggestedAction": "Review urgent - heated discussion"
  }
}
```

## Next Step

Proceed to [Step 4: Deep Analysis](./step4-deep-analysis.md) for complex issues.
