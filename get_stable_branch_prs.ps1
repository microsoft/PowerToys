param(
    [Parameter(Mandatory=$true)]
    [string]$StartCommit
)

Write-Host "Getting PR data from stable branch after commit: $StartCommit" -ForegroundColor Green

# Check if we're in a git repository
if (-not (Test-Path ".git")) {
    Write-Host "Error: This script must be run from a git repository root" -ForegroundColor Red
    exit 1
}

# Check if stable branch exists
$stableBranchExists = git branch -r | Where-Object { $_ -match "origin/stable" }
if (-not $stableBranchExists) {
    Write-Host "Error: stable branch not found in remote" -ForegroundColor Red
    exit 1
}

# Fetch latest changes
Write-Host "Fetching latest changes..." -ForegroundColor Yellow
git fetch origin

# Get commits from stable branch after the start commit
Write-Host "Getting commits from stable branch after $StartCommit..." -ForegroundColor Yellow
$commits = git log origin/stable --oneline --format="%H %s" "$StartCommit..origin/stable"

if (-not $commits) {
    Write-Host "No commits found after $StartCommit on stable branch" -ForegroundColor Yellow
    # Create empty file
    @() | Out-File -FilePath "stable_branch_prs_filtered.txt" -Encoding UTF8
    exit 0
}

$commitArray = $commits | ForEach-Object { 
    if ($_ -match "^([a-f0-9]+)\s+(.+)$") {
        @{
            Hash = $matches[1]
            Message = $matches[2]
        }
    }
}

Write-Host "Found $($commitArray.Count) commits to process" -ForegroundColor Cyan

# Extract PR numbers from commit messages
$prNumbers = @()
foreach ($commit in $commitArray) {
    # Look for PR numbers in commit messages (common patterns)
    if ($commit.Message -match "#(\d+)" -or $commit.Message -match "PR\s*#?(\d+)" -or $commit.Message -match "\(#(\d+)\)") {
        $prNumber = [int]$matches[1]
        if ($prNumbers -notcontains $prNumber) {
            $prNumbers += $prNumber
            Write-Host "Found PR #$prNumber from commit: $($commit.Message.Substring(0, [Math]::Min(60, $commit.Message.Length)))" -ForegroundColor Gray
        }
    }
}

if ($prNumbers.Count -eq 0) {
    Write-Host "No PR numbers found in commit messages" -ForegroundColor Yellow
    # Create empty file
    @() | Out-File -FilePath "stable_branch_prs_filtered.txt" -Encoding UTF8
    exit 0
}

Write-Host "Found $($prNumbers.Count) unique PR numbers" -ForegroundColor Cyan

# Function to clean PR description
function Remove-PRChecklistSection {
    param([string]$description)
    
    if (-not $description) { return "(No description provided)" }
    
    # Remove the entire "## PR Checklist" section but keep other sections
    $result = $description
    
    # Remove HTML comments
    $result = $result -replace '<!--.*?-->', ''
    
    # Split into sections and filter out PR Checklist
    if ($result -match '##\s+PR\s+Checklist') {
        # Find the start and end of the PR Checklist section
        $sections = $result -split '(?=##\s+)'
        $filteredSections = @()
        
        foreach ($section in $sections) {
            # Skip PR Checklist section and Validation Steps section if empty
            if ($section -match '^##\s+PR\s+Checklist' -or 
                $section -match '^##\s+Validation\s+Steps\s+Performed\s*$') {
                continue
            }
            $section = $section.Trim()
            if ($section) {
                $filteredSections += $section
            }
        }
        
        $result = $filteredSections -join "`r`n`r`n"
    }
    
    # Clean up extra whitespace
    $result = $result -replace '\r\n\s*\r\n+', "`r`n`r`n"
    $result = $result.Trim()
    
    return $result
}

# Function to extract AI Overview from PR comments and reviews
function Get-AIOverviewFromComments {
    param([int]$prNumber)
    
    try {
        # Get PR reviews first (Copilot reviews are usually here)
        $reviewsJson = gh pr view $prNumber --json reviews
        $reviewsData = $reviewsJson | ConvertFrom-Json
        
        foreach ($review in $reviewsData.reviews) {
            # Check if this is a GitHub Copilot review
            $isGitHubCopilot = $review.author.login -match "copilot|github-actions|app/github-copilot" -or
                              $review.body -match "github copilot|copilot|AI.*overview|AI.*summary" -or
                              $review.authorAssociation -eq "APP"
            
            if ($isGitHubCopilot -and $review.body) {
                # Look for various AI Overview patterns
                $overviewPatterns = @(
                    '##\s*Pull\s*Request\s*Overview\s*([\s\S]*?)(?=##|$)',
                    '##\s*AI\s*Overview\s*([\s\S]*?)(?=##|$)',
                    '##\s*Summary\s*([\s\S]*?)(?=##|$)',
                    '##\s*Overview\s*([\s\S]*?)(?=##|$)',
                    '##\s*Pull\s*Request\s*Summary\s*([\s\S]*?)(?=##|$)'
                )
                
                foreach ($pattern in $overviewPatterns) {
                    if ($review.body -match $pattern) {
                        $aiOverview = $matches[1].Trim()
                        if ($aiOverview -and $aiOverview.Length -gt 20) {
                            # Additional validation - make sure it's not just a header
                            if ($aiOverview -match '\w+.*\w+') {
                                return $aiOverview
                            }
                        }
                    }
                }
            }
        }
        
        # Get PR comments
        $commentsJson = gh pr view $prNumber --json comments
        $commentsData = $commentsJson | ConvertFrom-Json
        
        foreach ($comment in $commentsData.comments) {
            # Check if this is a GitHub Copilot comment
            $isGitHubCopilot = $comment.author.login -match "github-actions|copilot|app/github-copilot" -or
                              $comment.body -match "github copilot|copilot|AI.*overview|AI.*summary" -or
                              $comment.authorAssociation -eq "APP"
            
            if ($isGitHubCopilot) {
                # Look for various AI Overview patterns
                $overviewPatterns = @(
                    '##\s*Pull\s*Request\s*Overview\s*([\s\S]*?)(?=##|$)',
                    '##\s*AI\s*Overview\s*([\s\S]*?)(?=##|$)',
                    '##\s*Summary\s*([\s\S]*?)(?=##|$)',
                    '##\s*Overview\s*([\s\S]*?)(?=##|$)',
                    '##\s*Pull\s*Request\s*Summary\s*([\s\S]*?)(?=##|$)'
                )
                
                foreach ($pattern in $overviewPatterns) {
                    if ($comment.body -match $pattern) {
                        $aiOverview = $matches[1].Trim()
                        if ($aiOverview -and $aiOverview.Length -gt 20) {
                            # Additional validation - make sure it's not just a header
                            if ($aiOverview -match '\w+.*\w+') {
                                return $aiOverview
                            }
                        }
                    }
                }
            }
        }
        
        # If not found in comments/reviews, also check the main PR body for AI-generated sections
        $prJson = gh pr view $prNumber --json body
        $prData = $prJson | ConvertFrom-Json
        
        if ($prData.body) {
            $overviewPatterns = @(
                '##\s*Pull\s*Request\s*Overview\s*([\s\S]*?)(?=##|$)',
                '##\s*AI\s*Overview\s*([\s\S]*?)(?=##|$)',
                '##\s*Overview\s*([\s\S]*?)(?=##|$)',
                '##\s*Pull\s*Request\s*Summary\s*([\s\S]*?)(?=##|$)'
            )
            
            foreach ($pattern in $overviewPatterns) {
                if ($prData.body -match $pattern) {
                    $aiOverview = $matches[1].Trim()
                    if ($aiOverview -and $aiOverview.Length -gt 20 -and $aiOverview -match '\w+.*\w+') {
                        return $aiOverview
                    }
                }
            }
        }
        
        return $null
    } catch {
        Write-Host "    Warning: Could not fetch comments/reviews for PR #$prNumber : $($_.Exception.Message)" -ForegroundColor Yellow
        return $null
    }
}

# Check if GitHub CLI is available
$ghAvailable = $false
try {
    $null = gh --version
    $ghAvailable = $true
    Write-Host "GitHub CLI (gh) is available" -ForegroundColor Green
} catch {
    Write-Host "GitHub CLI (gh) is not available. Will use simplified format." -ForegroundColor Yellow
}

$combinedData = @()
$processedPRs = 0

foreach ($prNumber in $prNumbers | Sort-Object) {
    $processedPRs++
    Write-Host "Processing PR #$prNumber ($processedPRs/$($prNumbers.Count))..." -ForegroundColor Yellow
    
    if ($ghAvailable) {
        try {
            # Get PR information using GitHub CLI
            $prJson = gh pr view $prNumber --json number,title,author,createdAt,baseRefName,headRefName,url,body,state,labels
            $prData = $prJson | ConvertFrom-Json
            
            # Only process merged PRs
            if ($prData.state -eq "MERGED") {
                $author = $prData.author.login
                $created = $prData.createdAt
                $base = $prData.baseRefName
                $head = $prData.headRefName
                $description = Remove-PRChecklistSection -description $prData.body
                
                # Get AI Overview from comments
                Write-Host "    Fetching AI Overview..." -ForegroundColor Gray
                $aiOverview = Get-AIOverviewFromComments -prNumber $prNumber
                $hasAiOverview = $null -ne $aiOverview
                
                if ($hasAiOverview) {
                    Write-Host "    ✓ Found AI Overview" -ForegroundColor Green
                } else {
                    Write-Host "    - No AI Overview found" -ForegroundColor Gray
                }
                
                # Process labels
                $labelNames = @()
                if ($prData.labels -and $prData.labels.Count -gt 0) {
                    $labelNames = $prData.labels | ForEach-Object { $_.name }
                }
                
                $prInfo = @{
                    PRNumber = $prData.number
                    Title = $prData.title
                    Author = $author
                    Created = $created
                    Base = $base
                    Head = $head
                    URL = $prData.url
                    Description = $description
                    AIOverview = $aiOverview
                    HasAIOverview = $hasAiOverview
                    Labels = $labelNames
                }
                
                $combinedData += $prInfo
                Write-Host "  ✓ PR #${prNumber}: $($prData.title)" -ForegroundColor Green
            } else {
                Write-Host "  - PR #${prNumber}: Skipped (not merged, state: $($prData.state))" -ForegroundColor Gray
            }
        } catch {
            Write-Host "  × Error getting PR #$prNumber : $($_.Exception.Message)" -ForegroundColor Red
            
            # Fallback: create basic entry from commit information
            $relatedCommits = $commitArray | Where-Object { $_.Message -match "#$prNumber|PR\s*#?$prNumber|\(#$prNumber\)" }
            if ($relatedCommits) {
                $firstCommit = $relatedCommits[0]
                $title = $firstCommit.Message -replace "#$prNumber|\(#$prNumber\)|PR\s*#?$prNumber", "" -replace "^\s+|\s+$", ""
                
                $prInfo = @{
                    PRNumber = $prNumber
                    Title = $title
                    Author = "(Unknown)"
                    Created = "(Unknown)"
                    Base = "stable"
                    Head = "(Unknown)"
                    URL = "https://github.com/microsoft/PowerToys/pull/$prNumber"
                    Description = "(Unable to fetch PR details - inferred from commit message)"
                    AIOverview = $null
                    HasAIOverview = $false
                    Labels = @()
                }
                
                $combinedData += $prInfo
            }
        }
    } else {
        # Fallback when GitHub CLI is not available
        $relatedCommits = $commitArray | Where-Object { $_.Message -match "#$prNumber|PR\s*#?$prNumber|\(#$prNumber\)" }
        if ($relatedCommits) {
            $firstCommit = $relatedCommits[0]
            $title = $firstCommit.Message -replace "#$prNumber|\(#$prNumber\)|PR\s*#?$prNumber", "" -replace "^\s+|\s+$", ""
            
            # Get commit date
            $commitDate = git show -s --format="%ci" $firstCommit.Hash
            
            $prInfo = @{
                PRNumber = $prNumber
                Title = $title
                Author = "(Unknown - GitHub CLI required)"
                Created = $commitDate
                Base = "stable"
                Head = "(Unknown)"
                URL = "https://github.com/microsoft/PowerToys/pull/$prNumber"
                Description = "(GitHub CLI not available - inferred from commit: $($firstCommit.Message))"
                AIOverview = $null
                HasAIOverview = $false
                Labels = @()
            }
            
            $combinedData += $prInfo
            
            Write-Host "  ✓ PR #${prNumber}: $title (basic info)" -ForegroundColor Yellow
        }
    }
}

# Generate JSON output
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$outputData = @{
    generated_at = $timestamp
    start_commit = $StartCommit
    total_prs = $combinedData.Count
    commits_analyzed = $commitArray.Count
    unique_prs_found = $prNumbers.Count
    prs = $combinedData
}

# Save the JSON file
$jsonFile = "stable_branch_prs.json"
$outputData | ConvertTo-Json -Depth 10 | Out-File -FilePath $jsonFile -Encoding UTF8

Write-Host "`nGenerated file: $(Resolve-Path $jsonFile)" -ForegroundColor Green
Write-Host "Total PRs processed: $($combinedData.Count)" -ForegroundColor Cyan
Write-Host "Start commit: $StartCommit" -ForegroundColor Yellow

# Display summary
Write-Host "`nSummary:" -ForegroundColor Green
Write-Host "--------"
Write-Host "Commits analyzed: $($commitArray.Count)"
Write-Host "Unique PRs found: $($prNumbers.Count)"
Write-Host "Merged PRs included: $($combinedData.Count)"

if ($combinedData.Count -gt 0) {
    Write-Host "`nFirst few PRs:" -ForegroundColor Cyan
    $samplePRs = $combinedData | Select-Object -First 3
    foreach ($pr in $samplePRs) {
        Write-Host "  - PR #$($pr.PRNumber): $($pr.Title)"
    }
}
