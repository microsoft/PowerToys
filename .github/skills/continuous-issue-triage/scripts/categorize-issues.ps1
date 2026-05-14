<#
.SYNOPSIS
    Categorizes collected issues into actionable buckets.

.DESCRIPTION
    Applies categorization rules to issues collected by collect-active-issues.ps1.
    Outputs categorized results with priority scores and suggested actions.

.PARAMETER InputPath
    Path to collected issues JSON. Default: Generated Files/triage-issues/current-run/collected-issues.json

.PARAMETER StatePath
    Path to triage state JSON. Default: Generated Files/triage-issues/triage-state.json

.PARAMETER OutputPath
    Path to save categorized results. Default: Generated Files/triage-issues/current-run/categorized-issues.json

.PARAMETER TrendingThreshold
    Minimum new comments to flag as trending. Default: 5

.EXAMPLE
    .\categorize-issues.ps1

.EXAMPLE
    .\categorize-issues.ps1 -TrendingThreshold 10
#>

param(
    [Parameter()]
    [string]$InputPath = "Generated Files/triage-issues/current-run/collected-issues.json",
    
    [Parameter()]
    [string]$StatePath = "Generated Files/triage-issues/triage-state.json",
    
    [Parameter()]
    [string]$OutputPath = "Generated Files/triage-issues/current-run/categorized-issues.json",
    
    [Parameter()]
    [int]$TrendingThreshold = 5
)

$ErrorActionPreference = "Stop"

# Product keyword mapping
$ProductKeywords = @{
    "Product-FancyZones" = @("fancy zones", "fancyzones", "zone", "snap", "layout", "window arrangement", "virtual desktop")
    "Product-PowerToys Run" = @("run", "launcher", "alt+space", "alt space", "search", "plugin", "powertoys run")
    "Product-Color Picker" = @("color picker", "colorpicker", "eyedropper", "hex", "rgb", "color code")
    "Product-Keyboard Manager" = @("keyboard", "remap", "shortcut", "key mapping", "keyboard manager")
    "Product-Mouse Utils" = @("mouse", "crosshairs", "find my mouse", "highlighter", "pointer", "mouse without borders")
    "Product-File Explorer" = @("file explorer", "preview", "thumbnail", "markdown preview", "svg preview", "preview pane")
    "Product-Image Resizer" = @("image resizer", "resize image", "bulk resize", "resize pictures")
    "Product-PowerRename" = @("rename", "power rename", "powerrename", "bulk rename", "regex rename")
    "Product-Awake" = @("awake", "keep awake", "prevent sleep", "caffeinate", "stay awake")
    "Product-Shortcut Guide" = @("shortcut guide", "win key", "windows key guide")
    "Product-Text Extractor" = @("text extractor", "ocr", "screen text", "copy text from screen")
    "Product-Hosts File Editor" = @("hosts", "hosts file", "dns mapping")
    "Product-Peek" = @("peek", "quick preview", "spacebar preview", "file peek")
    "Product-Crop And Lock" = @("crop", "crop and lock", "window crop", "cropped window")
    "Product-Paste As Plain Text" = @("paste", "plain text", "paste as plain")
    "Product-Registry Preview" = @("registry", "reg file", "registry preview")
    "Product-Environment Variables" = @("environment", "env variable", "path variable", "system variable")
    "Product-Command Not Found" = @("command not found", "winget suggest", "command suggestion")
    "Product-New+" = @("new\+", "newplus", "file template", "new file")
    "Product-Advanced Paste" = @("advanced paste", "ai paste", "clipboard ai", "smart paste")
    "Product-Workspaces" = @("workspaces", "workspace launcher", "project layout")
    "Product-Cmd Palette" = @("command palette", "cmd palette", "quick command")
    "Product-ZoomIt" = @("zoomit", "zoom it", "screen zoom", "presentation zoom")
}

# Load collected issues
if (-not (Test-Path $InputPath)) {
    Write-Error "Input file not found: $InputPath. Run collect-active-issues.ps1 first."
    exit 1
}

$collected = Get-Content $InputPath | ConvertFrom-Json

# Load previous state
$previousState = $null
if (Test-Path $StatePath) {
    $previousState = Get-Content $StatePath | ConvertFrom-Json
}

function Get-IssueDetails {
    param([int]$IssueNumber)
    
    $json = gh issue view $IssueNumber `
        --json number,title,body,author,createdAt,updatedAt,state,labels,milestone,reactions,comments,linkedPullRequests 2>$null
    
    if (-not $json) { return $null }
    
    $issue = $json | ConvertFrom-Json
    
    return @{
        number = $issue.number
        title = $issue.title
        body = $issue.body
        author = $issue.author.login
        state = $issue.state
        createdAt = $issue.createdAt
        updatedAt = $issue.updatedAt
        labels = @($issue.labels | ForEach-Object { $_.name })
        milestone = $issue.milestone.title
        reactions = @{
            thumbsUp = ($issue.reactions | Where-Object { $_.content -eq "THUMBS_UP" }).Count
            thumbsDown = ($issue.reactions | Where-Object { $_.content -eq "THUMBS_DOWN" }).Count
            heart = ($issue.reactions | Where-Object { $_.content -eq "HEART" }).Count
        }
        commentCount = $issue.comments.Count
        comments = @($issue.comments | ForEach-Object {
            @{
                author = $_.author.login
                createdAt = $_.createdAt
                body = $_.body
            }
        })
        linkedPRs = @($issue.linkedPullRequests | ForEach-Object {
            @{
                number = $_.number
                state = $_.state
                mergedAt = $_.mergedAt
            }
        })
    }
}

function Get-LabelSuggestion {
    param([hashtable]$Issue)
    
    $titleLower = $Issue.title.ToLower()
    $bodyLower = if ($Issue.body) { $Issue.body.ToLower() } else { "" }
    $combined = "$titleLower $bodyLower"
    
    $matches = @()
    foreach ($product in $ProductKeywords.Keys) {
        $keywords = $ProductKeywords[$product]
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
            reason = "Matched $($best.matchCount) keywords"
        }
    }
    
    return @{ labels = @(); confidence = 0; reason = "No confident match" }
}

function Get-PriorityScore {
    param([hashtable]$Issue)
    
    $score = 50
    
    # Reactions
    $score += [Math]::Min(20, $Issue.reactions.thumbsUp * 2)
    
    # Comments
    $score += [Math]::Min(15, $Issue.commentCount)
    
    # Recency
    $daysSinceUpdate = ((Get-Date) - [datetime]$Issue.updatedAt).Days
    if ($daysSinceUpdate -le 7) { $score += 10 }
    elseif ($daysSinceUpdate -le 30) { $score += 5 }
    
    # Labels
    if ($Issue.labels -contains "Priority-High") { $score += 15 }
    if ($Issue.labels -match "Regression") { $score += 20 }
    if ($Issue.labels -match "Security") { $score += 25 }
    
    return [Math]::Min(100, $score)
}

# Process each issue
$categorized = @{}
$issueCount = $collected.issues.Count
$current = 0

Write-Host "Categorizing $issueCount issues..."
Write-Host ""

foreach ($collectedIssue in $collected.issues) {
    $current++
    $issueNum = $collectedIssue.number
    
    Write-Host "[$current/$issueCount] Processing #$issueNum..."
    
    # Get full issue details
    $issue = Get-IssueDetails -IssueNumber $issueNum
    if (-not $issue) {
        Write-Host "  Warning: Could not fetch issue #$issueNum"
        continue
    }
    
    # Get previous snapshot
    $previousSnapshot = $null
    if ($previousState -and $previousState.issueSnapshots.$issueNum) {
        $previousSnapshot = $previousState.issueSnapshots.$issueNum
    }
    
    # Calculate new comments
    $previousCommentCount = if ($previousSnapshot) { $previousSnapshot.commentCount } else { 0 }
    $newComments = $issue.commentCount - $previousCommentCount
    
    # Categorize (priority order - first match wins)
    $category = $null
    $categoryReason = $null
    $suggestedAction = $null
    $additionalData = @{}
    
    # 1. Trending
    if ($newComments -ge $TrendingThreshold) {
        $category = "trending"
        $categoryReason = "$newComments new comments since last run"
        $suggestedAction = "Review conversation urgently"
    }
    
    # 2. Closeable (check for merged PRs)
    if (-not $category) {
        $mergedPRs = $issue.linkedPRs | Where-Object { $_.state -eq "MERGED" }
        if ($mergedPRs.Count -gt 0 -and $issue.state -eq "OPEN") {
            $category = "closeable"
            $categoryReason = "Has merged PR(s): #" + ($mergedPRs.number -join ", #")
            $suggestedAction = "Close with thank you message"
            $additionalData.mergedPRs = $mergedPRs.number
        }
    }
    
    # 3. Needs-Label
    if (-not $category) {
        $productLabels = $issue.labels | Where-Object { $_ -like "Product-*" }
        $areaLabels = $issue.labels | Where-Object { $_ -like "Area-*" }
        
        if ($productLabels.Count -eq 0 -and $areaLabels.Count -eq 0) {
            $suggestion = Get-LabelSuggestion -Issue $issue
            $category = "needs-label"
            $categoryReason = "Missing Product/Area label"
            $suggestedAction = "Apply label: $($suggestion.labels -join ', ')"
            $additionalData.suggestedLabels = $suggestion.labels
            $additionalData.labelConfidence = $suggestion.confidence
        }
    }
    
    # 4. Stale-Waiting
    if (-not $category) {
        if ($issue.labels -contains "Needs-Author-Feedback") {
            $lastAuthorComment = $issue.comments | 
                Where-Object { $_.author -eq $issue.author } | 
                Sort-Object createdAt -Descending | 
                Select-Object -First 1
            
            if ($lastAuthorComment) {
                $daysSince = ((Get-Date) - [datetime]$lastAuthorComment.createdAt).Days
                if ($daysSince -gt 14) {
                    $category = "stale-waiting"
                    $categoryReason = "Waiting on author for $daysSince days"
                    $suggestedAction = "Ping or close"
                    $additionalData.daysWaiting = $daysSince
                }
            }
        }
    }
    
    # 5. Needs-Clarification (question, not bug)
    if (-not $category) {
        $isQuestion = $false
        $titleAndBody = "$($issue.title) $($issue.body)"
        
        if ($titleAndBody -match '\?$' -or 
            $titleAndBody -match '(?i)(how (do|can|to)|why (does|is)|is (it|there) possible)' -or
            $issue.labels -contains "Issue-Question") {
            $isQuestion = $true
        }
        
        if ($isQuestion -and ($issue.labels -notcontains "Issue-Bug")) {
            $category = "needs-clarification"
            $categoryReason = "Appears to be a question/inquiry"
            $suggestedAction = "Draft explanation reply"
        }
    }
    
    # 6. Needs-Info
    if (-not $category) {
        $missingItems = @()
        $body = $issue.body
        
        if ($body -and $body.Length -gt 0) {
            if ($body -notmatch '(?i)(steps to reproduce|repro|how to reproduce)') {
                $missingItems += "repro steps"
            }
            if ($body -notmatch '(?i)(expected|should|supposed to)') {
                $missingItems += "expected behavior"
            }
            if ($body -notmatch '(?i)(version|v\d+\.\d+)') {
                $missingItems += "PowerToys version"
            }
        } else {
            $missingItems += "description"
        }
        
        if ($missingItems.Count -gt 0) {
            $category = "needs-info"
            $categoryReason = "Missing: " + ($missingItems -join ", ")
            $suggestedAction = "Post clarifying questions"
            $additionalData.missingItems = $missingItems
        }
    }
    
    # 7. Default: review-needed
    if (-not $category) {
        $category = "review-needed"
        $categoryReason = "Needs human review for categorization"
        $suggestedAction = "Manual triage"
    }
    
    # Calculate priority score
    $priorityScore = Get-PriorityScore -Issue $issue
    
    # Store result
    $categorized[$issueNum] = @{
        number = $issue.number
        title = $issue.title
        state = $issue.state
        labels = $issue.labels
        category = $category
        categoryReason = $categoryReason
        priorityScore = $priorityScore
        suggestedAction = $suggestedAction
        newComments = $newComments
        totalComments = $issue.commentCount
        reactions = $issue.reactions
        updatedAt = $issue.updatedAt
        additionalData = $additionalData
    }
    
    Write-Host "  -> $category (priority: $priorityScore)"
}

# Group by category for summary
$byCategory = $categorized.Values | Group-Object category

Write-Host ""
Write-Host "=== Categorization Summary ==="
foreach ($group in $byCategory | Sort-Object Count -Descending) {
    Write-Host "  $($group.Name): $($group.Count) issues"
}

# Save results
$output = @{
    categorizedAt = (Get-Date).ToUniversalTime().ToString("o")
    totalCategorized = $categorized.Count
    byCategory = @{}
    issues = $categorized
}

foreach ($group in $byCategory) {
    $output.byCategory[$group.Name] = @{
        count = $group.Count
        topIssues = @($group.Group | Sort-Object priorityScore -Descending | Select-Object -First 3 | ForEach-Object { $_.number })
    }
}

$output | ConvertTo-Json -Depth 10 | Set-Content $OutputPath
Write-Host ""
Write-Host "Results saved to: $OutputPath"
