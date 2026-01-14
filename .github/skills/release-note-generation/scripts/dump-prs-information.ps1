<#
.SYNOPSIS
    Export merged pull requests for a milestone into JSON and CSV (sorted) with optional Copilot review summarization.

.DESCRIPTION
    Uses the GitHub CLI (gh) to list merged PRs for the specified milestone, captures basic metadata,
    attempts to obtain a Copilot review summary (choosing the longest Copilot-authored review body),
    filters labels to a predefined allow-list, and outputs:
      * Raw JSON list (for traceability)
      * Sorted CSV (first label alphabetical) used by downstream grouping scripts.

.PARAMETER Repo
    GitHub repository in the form 'owner/name'. Default: 'microsoft/PowerToys'.

.PARAMETER Milestone
    Exact milestone title (as it appears on GitHub), e.g. 'PowerToys 0.95'.

.PARAMETER OutputJson
    Path for raw JSON output. Default: 'milestone_prs.json'.

.PARAMETER OutputCsv
    Path for sorted CSV output. Default: 'sorted_prs.csv'.

.EXAMPLE
    pwsh ./dump-prs-information.ps1 -Milestone 'PowerToys 0.95'

.EXAMPLE
    pwsh ./dump-prs-information.ps1 -Repo microsoft/PowerToys -Milestone 'PowerToys 0.95' -OutputCsv m1.csv

.NOTES
    Requires: gh CLI authenticated with repo read access.
    This script intentionally does NOT use Set-StrictMode (per current repository guidance for release tooling).
#>
[CmdletBinding()] param(
    [Parameter(Mandatory=$false)][string]$Repo = 'microsoft/PowerToys',
    [Parameter(Mandatory=$true)][string]$Milestone,
    [Parameter(Mandatory=$false)][string]$OutputJson = 'milestone_prs.json',
    [Parameter(Mandatory=$false)][string]$OutputCsv  = 'sorted_prs.csv'
)

$ErrorActionPreference = 'Stop'

function Write-Info($m){ Write-Host "[info] $m" -ForegroundColor Cyan }
function Write-Warn($m){ Write-Host "[warn] $m" -ForegroundColor Yellow }
function Write-Err($m){ Write-Host "[error] $m" -ForegroundColor Red }

# Load member list from MemberList.md (internal team - no thanks needed)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$memberListPath = Join-Path $scriptDir "..\references\MemberList.md"
$memberList = @()
if (Test-Path $memberListPath) {
    $memberListContent = Get-Content $memberListPath -Raw
    # Extract usernames - skip markdown code fence lines, get all non-empty lines
    $memberList = ($memberListContent -split "`n") | Where-Object { $_ -notmatch '^\s*```' -and $_.Trim() -ne '' } | ForEach-Object { $_.Trim() }
    Write-Info "Loaded $($memberList.Count) members from MemberList.md"
} else {
    Write-Warn "MemberList.md not found at $memberListPath - NeedThanks will default to true for all authors"
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { Write-Err "GitHub CLI 'gh' not found in PATH."; exit 1 }

Write-Info "Fetching merged PRs for milestone '$Milestone' from $Repo ..."
$searchQuery = "milestone:`"$Milestone`""
$ghCommand = "gh pr list --repo $Repo --state merged --search '$searchQuery' --json number,title,labels,author,url,body --limit 200"
try {
    Invoke-Expression $ghCommand | Out-File -Encoding UTF8 -FilePath $OutputJson
}
catch {
    Write-Err "Failed querying PRs: $_"; exit 1
}

# === STEP 1: Query PRs from GitHub ===
if (-not (Test-Path -LiteralPath $OutputJson)) { Write-Err "JSON output not created: $OutputJson"; exit 1 }

Write-Info "Parsing JSON ..."
$prs = Get-Content $OutputJson | ConvertFrom-Json
if (-not $prs) { Write-Warn "No PRs returned for milestone '$Milestone'"; exit 0 }
$sorted = $prs | Sort-Object { $_.labels[0]?.name }

Write-Info "Fetching Copilot reviews for each PR (longest Copilot-authored body)."
$csvData = $sorted | ForEach-Object {
    $prNumber = $_.number
    Write-Info "Processing PR #$prNumber ..."
    
    # Get Copilot review for this PR
    $copilotOverview = ""
    try {
        $reviewsCommand = "gh pr view $prNumber --repo $repo --json reviews"
        $reviewsJson = Invoke-Expression $reviewsCommand | ConvertFrom-Json
        
        # Collect Copilot reviews (match various author logins). Choose the LONGEST body (more content) vs newest.
        $copilotReviews = $reviewsJson.reviews | Where-Object { 
            ($_.author.login -eq "github-copilot[bot]" -or 
             $_.author.login -eq "copilot" -or 
             $_.author.login -eq "github-copilot" -or
             $_.author.login -like "*copilot*") -and 
            $_.body -and 
            $_.body.Trim() -ne ""
        }
        if ($copilotReviews -and $copilotReviews.Count -gt 0) {
            $longest = $copilotReviews | Sort-Object { $_.body.Length } -Descending | Select-Object -First 1
            $copilotOverview = $longest.body.Replace("`r", "").Replace("`n", " ") -replace '\s+', ' '
            Write-Info "  Copilot review selected (author=$($longest.author.login) length=$($longest.body.Length))"
        } else {
            Write-Warn "  No Copilot reviews found for PR #$prNumber"
        }
    }
    catch {
    Write-Warn "  Could not fetch reviews for PR #$prNumber"
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
    
    # Determine if author needs thanks (not in member list)
    $authorLogin = $_.author.login
    $needThanks = $true
    if ($memberList.Count -gt 0 -and $authorLogin) {
        $needThanks = -not ($memberList -contains $authorLogin)
    }
    
    [PSCustomObject]@{
        Id = $_.number
        Title  = $_.title
        Labels = $labelNames
        Author = $authorLogin
        Url    = $_.url
        Body   = $_.body.Replace("`r", "").Replace("`n", " ") -replace '\s+', ' '  # Make body single-line
        CopilotSummary = $copilotOverview
        NeedThanks = $needThanks
    }
}

# === STEP 3: Output CSV ===
Write-Info "Saving CSV to $OutputCsv ..."
$csvData | Export-Csv $OutputCsv -NoTypeInformation -Encoding UTF8
Write-Info "Done. Rows: $($csvData.Count). CSV: $(Resolve-Path -LiteralPath $OutputCsv)"