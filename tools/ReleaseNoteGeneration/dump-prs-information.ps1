# === CONFIGURATION ===
$repo = "microsoft/Powertoys"           # e.g., "microsoft/winget-cli"
$milestone = "PowerToys 0.94"           # Milestone title
$outputJson = "milestone_prs.json"
$outputCsv = "sorted_prs.csv"

# === STEP 1: Query PRs from GitHub ===
Write-Host "Fetching PRs for milestone '$milestone'..."
$searchQuery = "milestone:`"$($milestone)`""
$ghCommand = "gh pr list --repo $repo --state merged --search '$searchQuery' --json number,title,labels,author,url,body --limit 200"
Invoke-Expression "$ghCommand" | Out-File -Encoding UTF8 -FilePath $outputJson

# === STEP 2: Parse and Sort ===
$prs = Get-Content $outputJson | ConvertFrom-Json
$sorted = $prs | Sort-Object { $_.labels[0]?.name }

Write-Host "Fetching Copilot reviews for each PR..."
$csvData = $sorted | ForEach-Object {
    $prNumber = $_.number
    Write-Host "Processing PR #$prNumber..."
    
    # Get Copilot review for this PR
    $copilotOverview = ""
    try {
        $reviewsCommand = "gh pr view $prNumber --repo $repo --json reviews"
        $reviewsJson = Invoke-Expression $reviewsCommand | ConvertFrom-Json
        
        # Find the latest Copilot review - try different possible author names
        $copilotReviews = $reviewsJson.reviews | Where-Object { 
            ($_.author.login -eq "github-copilot[bot]" -or 
             $_.author.login -eq "copilot" -or 
             $_.author.login -eq "github-copilot" -or
             $_.author.login -like "*copilot*") -and 
            $_.body -and 
            $_.body.Trim() -ne ""
        } | Sort-Object submittedAt -Descending
        
        if ($copilotReviews -and $copilotReviews.Count -gt 0) {
            $copilotOverview = $copilotReviews[0].body.Replace("`r", "").Replace("`n", " ") -replace '\s+', ' '
            Write-Host "  Found Copilot review from: $($copilotReviews[0].author.login)"
        } else {
            Write-Host "  No Copilot reviews found for PR #$prNumber"
        }
    }
    catch {
        Write-Host "  Warning: Could not fetch reviews for PR #$prNumber"
    }
    
    # Filter labels to only include specific patterns
    $filteredLabels = $_.labels | Where-Object { 
        ($_.name -like "Product-*") -or 
        ($_.name -like "Area-*") -or 
        ($_.name -like "Github*") -or 
        ($_.name -like "*Plugin") -or 
        ($_.name -like "Issue-*") 
    }
    
    $labelNames = ($filteredLabels | ForEach-Object { $_.name }) -join ", "
    [PSCustomObject]@{
        Id = $_.number
        Title  = $_.title
        Labels = $labelNames
        Author = $_.author.login
        Url    = $_.url
        Body   = $_.body.Replace("`r", "").Replace("`n", " ") -replace '\s+', ' '  # Make body single-line
        CopilotSummary = $copilotOverview
    }
}

# === STEP 3: Output CSV ===
Write-Host "Saving to $outputCsv..."
$csvData | Export-Csv $outputCsv -NoTypeInformation
Write-Host "✅ Done. Open '$outputCsv' to group PRs and send them back."