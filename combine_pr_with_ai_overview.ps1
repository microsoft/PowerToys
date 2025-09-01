param(
    [Parameter(Mandatory=$true)]
    [string]$StartCommit
)

Write-Host "Combining PR data with AI Pull Request Overview..." -ForegroundColor Green
Write-Host "Start commit: $StartCommit" -ForegroundColor Yellow

# Check if required files exist
$prFile = "stable_branch_prs_filtered.txt"
$copilotFile = "stable_branch_copilot_reviews.json"

if (-not (Test-Path $prFile)) {
    Write-Host "Error: $prFile not found. Please run get_stable_branch_prs.ps1 first." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $copilotFile)) {
    Write-Host "Error: $copilotFile not found. Please run get_copilot_reviews.ps1 first." -ForegroundColor Red
    exit 1
}

# Read the Copilot reviews data
$copilotData = Get-Content $copilotFile -Raw | ConvertFrom-Json
Write-Host "Loaded $($copilotData.reviews.Count) Copilot reviews" -ForegroundColor Cyan

# Create a hashtable to store PR Overview by PR number
$prOverviews = @{}

# Extract Pull Request Overview from Copilot reviews
Write-Host "Processing $($copilotData.reviews.Count) reviews..." -ForegroundColor Yellow
$reviewCount = 0
foreach ($review in $copilotData.reviews) {
    $reviewCount++
    if ($reviewCount % 20 -eq 0) {
        Write-Host "  Processed $reviewCount reviews..." -ForegroundColor Gray
    }
    
    $prNumber = $review.pr_number
    $reviewBody = $review.review_body
    $commentBody = $review.comment_body
    
    # Look for "## Pull Request Overview" in review body
    if ($reviewBody -and $reviewBody.Contains("## Pull Request Overview")) {
        # Extract content after "## Pull Request Overview"
        $startIndex = $reviewBody.IndexOf("## Pull Request Overview") + "## Pull Request Overview".Length
        $remaining = $reviewBody.Substring($startIndex).Trim()
        
        # Find the end of the overview section
        $endPatterns = @("### ", "## ", "---", "<details>")
        $endIndex = $remaining.Length
        foreach ($pattern in $endPatterns) {
            $index = $remaining.IndexOf($pattern)
            if ($index -gt 0 -and $index -lt $endIndex) {
                $endIndex = $index
            }
        }
        
        $overview = $remaining.Substring(0, $endIndex).Trim()
        if ($overview -and -not $prOverviews.ContainsKey($prNumber)) {
            Write-Host "Found overview for PR $prNumber" -ForegroundColor Green
            $prOverviews[$prNumber] = @{
                Content = $overview
                Source = "Review"
                Reviewer = $review.reviewer
                SubmittedAt = $review.submitted_at
            }
        }
    }
    
    # Also check comment body if no overview found yet
    if (-not $prOverviews.ContainsKey($prNumber) -and $commentBody -and $commentBody -match "## Pull Request Overview\\n\\n(.*?)(?=\\n###|\\n##|\\n---|\z)") {
        $overview = $matches[1].Trim() -replace '\\n', "`n"
        if ($overview) {
            $prOverviews[$prNumber] = @{
                Content = $overview
                Source = "Comment"
                Reviewer = $review.reviewer
                CreatedAt = $review.created_at
            }
        }
    }
}

Write-Host "Found Pull Request Overviews for $($prOverviews.Count) PRs" -ForegroundColor Cyan

# Read PR data from filtered file
$prContent = Get-Content $prFile -Raw

# Extract PR information using simpler approach
$prBlocks = $prContent -split "={80}" | Where-Object { $_.Trim() -and $_.Contains("PR #") }

Write-Host "Processing $($prBlocks.Count) PR blocks..." -ForegroundColor Yellow

foreach ($block in $prBlocks) {
    if ($block -match "PR #(\d+) \[MERGED\]") {
        $prNumber = [int]$matches[1]
        
        # Extract other fields
        if ($block -match "Title: (.*?)(?=\r?\n|\r)") { $title = $matches[1].Trim() }
        if ($block -match "Author: (.*?)(?=\r?\n|\r)") { $author = $matches[1].Trim() }
        if ($block -match "Created: (.*?)(?=\r?\n|\r)") { $created = $matches[1].Trim() }
        if ($block -match "Base: (.*?) \| Head: (.*?)(?=\r?\n|\r)") { 
            $base = $matches[1].Trim()
            $head = $matches[2].Trim()
        }
        if ($block -match "URL: (.*?)(?=\r?\n|\r)") { $url = $matches[1].Trim() }
        
        # Extract description
        if ($block -match "Description:\r?\n(.*?)$") {
            $description = $matches[1].Trim()
        } else {
            $description = ""
        }
        
        $prInfo = @{
            PRNumber = $prNumber
            Title = $title
            Author = $author
            Created = $created
            Base = $base
            Head = $head
            URL = $url
            Description = $description
            AIOverview = $null
            HasAIOverview = $false
        }
        
        # Check if we have AI overview for this PR
        if ($prOverviews.ContainsKey($prNumber)) {
            $overview = $prOverviews[$prNumber]
            $prInfo.AIOverview = $overview.Content
            $prInfo.HasAIOverview = $true
        }
        
        $combinedData += $prInfo
    }
}

$combinedData = @()
Write-Host "Processed $($combinedData.Count) PRs total" -ForegroundColor Cyan
$prsWithOverview = ($combinedData | Where-Object { $_.HasAIOverview }).Count
Write-Host "PRs with AI Overview: $prsWithOverview" -ForegroundColor Green

# Generate combined output
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$outputContent = @"
PowerToys PRs with AI Pull Request Overview from stable branch after $StartCommit
Generated: $timestamp
Total PRs: $($combinedData.Count)
PRs with AI Overview: $prsWithOverview
================================================================================

"@

foreach ($pr in $combinedData) {
    $outputContent += @"
PR #$($pr.PRNumber): $($pr.Title)
URL: $($pr.URL)
Author: $($pr.Author)
Created: $($pr.Created)

## Original PR Description
$($pr.Description)

"@

    if ($pr.HasAIOverview) {
        $outputContent += @"
## AI Pull Request Overview
Reviewer: $($pr.AIReviewer)
Review Date: $($pr.AIReviewDate)

$($pr.AIOverview)

"@
    } else {
        $outputContent += @"
## AI Pull Request Overview
(No AI overview available for this PR)

"@
    }
    
    $outputContent += "`n" + "="*80 + "`n`n"
}

# Save outputs
$jsonFile = "combined_prs_with_ai_overview.json"
$txtFile = "combined_prs_with_ai_overview.txt"

$combinedData | ConvertTo-Json -Depth 10 | Out-File -FilePath $jsonFile -Encoding UTF8
$outputContent | Out-File -FilePath $txtFile -Encoding UTF8

Write-Host "`nFiles generated:" -ForegroundColor Green
Write-Host "JSON: $(Resolve-Path $jsonFile)" -ForegroundColor Yellow
Write-Host "TXT:  $(Resolve-Path $txtFile)" -ForegroundColor Yellow

Write-Host "`nSummary:" -ForegroundColor Green
Write-Host "--------"
Write-Host "Start commit: $StartCommit"
Write-Host "Total PRs processed: $($combinedData.Count)"
Write-Host "PRs with AI Overview: $prsWithOverview"

if ($prsWithOverview -gt 0) {
    Write-Host "`nSample PRs with AI Overview:" -ForegroundColor Cyan
    $samplesWithOverview = $combinedData | Where-Object { $_.HasAIOverview } | Select-Object -First 3
    foreach ($sample in $samplesWithOverview) {
        Write-Host "  PR #$($sample.PRNumber): $($sample.Title)"
    }
}
