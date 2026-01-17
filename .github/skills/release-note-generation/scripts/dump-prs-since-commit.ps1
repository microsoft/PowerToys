<#
.SYNOPSIS
    Export merged PR metadata between two commits (exclusive start, inclusive end) to JSON and CSV.

.DESCRIPTION
    Identifies merge/squash commits reachable from EndCommit but not StartCommit, extracts PR numbers,
    queries GitHub for metadata plus (optionally) Copilot review/comment summaries, filters labels, then
    emits a JSON artifact and a sorted CSV (first label alphabetical).

.PARAMETER StartCommit
    Exclusive starting commit (SHA, tag, or ref). Commits AFTER this one are considered.

.PARAMETER EndCommit
    Inclusive ending commit (SHA, tag, or ref). If not provided, uses origin/<Branch> when Branch is set; otherwise uses HEAD.

.PARAMETER Repo
    GitHub repository (owner/name). Default: microsoft/PowerToys.

.PARAMETER OutputCsv
    Destination CSV path. Default: sorted_prs.csv.

.PARAMETER OutputJson
    Destination JSON path containing raw PR objects. Default: milestone_prs.json.

.EXAMPLE
    pwsh ./dump-prs-since-commit.ps1 -StartCommit 0123abcd -Branch stable

.EXAMPLE
    pwsh ./dump-prs-since-commit.ps1 -StartCommit 0123abcd -EndCommit 89ef7654 -OutputCsv delta.csv

.NOTES
    Requires: git, gh (authenticated). No Set-StrictMode to keep parity with existing release scripts.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$StartCommit,  # exclusive start (commits AFTER this one)
    [string]$EndCommit,
    [string]$Branch,
    [string]$Repo = "microsoft/PowerToys",
    [string]$OutputDir,
    [string]$OutputCsv = "sorted_prs.csv",
    [string]$OutputJson = "milestone_prs.json"
)

<#
.SYNOPSIS
    Dump merged PR information whose merge commits are reachable from EndCommit but not from StartCommit.
.DESCRIPTION
    Uses git rev-list to compute commits in the (StartCommit, EndCommit] range, extracts PR numbers from merge commit messages,
    queries GitHub (gh CLI) for details, then outputs a CSV.

    PR merge commit messages in PowerToys generally contain patterns like:
        Merge pull request #12345 from ...

.EXAMPLE
    pwsh ./dump-prs-since-commit.ps1 -StartCommit 0123abcd -Branch stable

.EXAMPLE
    pwsh ./dump-prs-since-commit.ps1 -StartCommit 0123abcd -EndCommit 89ef7654 -OutputCsv changes.csv

.NOTES
    Requires: gh CLI authenticated; git available in working directory (must be inside PowerToys repo clone).
    CopilotSummary behavior:
      - Attempts to locate the latest GitHub Copilot authored review (preferred).
      - If no review is found, lazily fetches PR comments to look for a Copilot-authored comment.
      - Normalizes whitespace and strips newlines. Empty when no Copilot activity detected.
      - Run with -Verbose to see whether the summary came from a 'review' or 'comment' source.
#>

function Write-Info($msg) { Write-Host $msg -ForegroundColor Cyan }
function Write-Warn($msg) { Write-Host $msg -ForegroundColor Yellow }
function Write-Err($msg) { Write-Host $msg -ForegroundColor Red }
function Write-DebugMsg($msg) { if ($PSBoundParameters.ContainsKey('Verbose') -or $VerbosePreference -eq 'Continue') { Write-Host "[VERBOSE] $msg" -ForegroundColor DarkGray } }

# Load member list from Generated Files/ReleaseNotes/MemberList.md (internal team - no thanks needed)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..\..\..\..")
$defaultMemberListPath = Join-Path $repoRoot "Generated Files\ReleaseNotes\MemberList.md"
$memberListPath = $defaultMemberListPath
if ($OutputDir) {
    $memberListFromOutputDir = Join-Path $OutputDir "MemberList.md"
    if (Test-Path $memberListFromOutputDir) {
        $memberListPath = $memberListFromOutputDir
    }
}
$memberList = @()
if (Test-Path $memberListPath) {
    $memberListContent = Get-Content $memberListPath -Raw
    # Extract usernames - skip markdown code fence lines, get all non-empty lines
    $memberList = ($memberListContent -split "`n") | Where-Object { $_ -notmatch '^\s*```' -and $_.Trim() -ne '' } | ForEach-Object { $_.Trim() }
    if (-not $memberList -or $memberList.Count -eq 0) {
        Write-Err "MemberList.md is empty at $memberListPath"
        exit 1
    }
    Write-DebugMsg "Loaded $($memberList.Count) members from MemberList.md"
} else {
    Write-Err "MemberList.md not found at $memberListPath"
    exit 1
}

# Validate we are in a git repo
#if (-not (Test-Path .git)) {
#    Write-Err "Current directory does not appear to be the root of a git repository."
#    exit 1
#}

# Resolve output directory (if specified)
if ($OutputDir) {
    if (-not (Test-Path $OutputDir)) {
        New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    }
    if (-not [System.IO.Path]::IsPathRooted($OutputCsv)) {
        $OutputCsv = Join-Path $OutputDir $OutputCsv
    }
    if (-not [System.IO.Path]::IsPathRooted($OutputJson)) {
        $OutputJson = Join-Path $OutputDir $OutputJson
    }
}

# Resolve commits
try {
    if ($Branch) {
        Write-Info "Fetching latest '$Branch' from origin (with tags)..."
        git fetch origin $Branch --tags | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "git fetch origin $Branch --tags failed" }
    }

    $startSha = (git rev-parse --verify $StartCommit) 2>$null
    if (-not $startSha) { throw "StartCommit '$StartCommit' not found" }
    if ($Branch) {
        $branchRef = $Branch
        $branchSha = (git rev-parse --verify $branchRef) 2>$null
        if (-not $branchSha) {
            $branchRef = "origin/$Branch"
            $branchSha = (git rev-parse --verify $branchRef) 2>$null
        }
        if (-not $branchSha) { throw "Branch '$Branch' not found" }
        if (-not $PSBoundParameters.ContainsKey('EndCommit') -or [string]::IsNullOrWhiteSpace($EndCommit)) {
            $EndCommit = $branchRef
        }
    }
    if (-not $PSBoundParameters.ContainsKey('EndCommit') -or [string]::IsNullOrWhiteSpace($EndCommit)) {
        $EndCommit = "HEAD"
    }
    $endSha = (git rev-parse --verify $EndCommit) 2>$null
    if (-not $endSha) { throw "EndCommit '$EndCommit' not found" }
}
catch {
    Write-Err $_
    exit 1
}

Write-Info "Collecting commits between $startSha..$endSha (excluding start, including end)."
    # Get list of commits reachable from end but not from start.
    # IMPORTANT: In PowerShell, the .. operator creates a numeric/char range. If $startSha and $endSha look like hex strings,
    # `$startSha..$endSha` must be passed as a single string argument.
    $rangeArg = "$startSha..$endSha"
    $commitList = git rev-list $rangeArg

# Normalize list (filter out empty strings)
$normalizedCommits = $commitList | Where-Object { $_ -and $_.Trim() -ne '' }
$commitCount = ($normalizedCommits | Measure-Object).Count
Write-DebugMsg ("Raw commitList length (including blanks): {0}" -f (($commitList | Measure-Object).Count))
Write-DebugMsg ("Normalized commit count: {0}" -f $commitCount)
if ($commitCount -eq 0) {
    Write-Warn "No commits found in specified range ($startSha..$endSha)."; exit 0
}
Write-DebugMsg ("First 5 commits: {0}" -f (($normalizedCommits | Select-Object -First 5) -join ', '))

<#
    Extract PR numbers from commits.
    Patterns handled:
      1. Merge commits:   'Merge pull request #12345 from ...'
      2. Squash commits:  'Some feature change (#12345)' (GitHub default squash format)
    We collect both. If a commit matches both (unlikely), it's deduped later.
#>
# Extract PR numbers from merge or squash commits
$mergeCommits = @()
foreach ($c in $normalizedCommits) {
    $subject = git show -s --format=%s $c
    $matched = $false
    # Pattern 1: Traditional merge commit
    if ($subject -match 'Merge pull request #([0-9]+) ') {
        $prNumber = [int]$matches[1]
        $mergeCommits += [PSCustomObject]@{ Sha = $c; Pr = $prNumber; Subject = $subject; Pattern = 'merge' }
        Write-DebugMsg "Matched merge PR #$prNumber in commit $c"
        $matched = $true
    }
    # Pattern 2: Squash merge subject line with ' (#12345)' at end (allow possible whitespace before paren)
    if ($subject -match '\(#([0-9]+)\)$') {
        $prNumber2 = [int]$matches[1]
        # Avoid duplicate object if pattern 1 already captured same number for same commit
        if (-not ($mergeCommits | Where-Object { $_.Sha -eq $c -and $_.Pr -eq $prNumber2 })) {
            $mergeCommits += [PSCustomObject]@{ Sha = $c; Pr = $prNumber2; Subject = $subject; Pattern = 'squash' }
            Write-DebugMsg "Matched squash PR #$prNumber2 in commit $c"
        }
        $matched = $true
    }
    if (-not $matched) {
        Write-DebugMsg "No PR pattern in commit $c : $subject"
    }
}

if (-not $mergeCommits -or $mergeCommits.Count -eq 0) {
    Write-Warn "No merge commits with PR numbers found in range."; exit 0
}

# Deduplicate PR numbers (in case of revert or merges across branches)
$prNumbers = $mergeCommits | Select-Object -ExpandProperty Pr -Unique | Sort-Object
Write-Info ("Found {0} unique PRs: {1}" -f $prNumbers.Count, ($prNumbers -join ', '))
Write-DebugMsg ("Total merge commits examined: {0}" -f $mergeCommits.Count)

# Query GitHub for each PR
$prDetails = @()
function Get-CopilotSummaryFromPrJson {
    param(
        [Parameter(Mandatory=$true)]$PrJson,
        [switch]$VerboseMode
    )
    # Returns a hashtable with Summary and Source keys.
    $result = @{ Summary = ""; Source = "" }
    if (-not $PrJson) { return $result }

    $candidateAuthors = @(
        'github-copilot[bot]', 'github-copilot', 'copilot'
    )

    # 1. Reviews (preferred) – pick the LONGEST valid Copilot body, not the most recent
    $reviews = $PrJson.reviews
    if ($reviews) {
        $copilotReviews = $reviews | Where-Object {
            ($candidateAuthors -contains $_.author.login -or $_.author.login -like '*copilot*') -and $_.body -and $_.body.Trim() -ne ''
        }
        if ($copilotReviews) {
            $longest = $copilotReviews | Sort-Object { $_.body.Length } -Descending | Select-Object -First 1
            if ($longest) {
                $body = $longest.body
                $norm = ($body -replace "`r", '') -replace "`n", ' '
                $norm = $norm -replace '\s+', ' '
                $result.Summary = $norm
                $result.Source = 'review'
                if ($VerboseMode) { Write-DebugMsg "Selected Copilot review length=$($body.Length) (longest)." }
                return $result
            }
        }
    }

    # 2. Comments fallback (some repos surface Copilot summaries as PR comments rather than review objects)
    if ($null -eq $PrJson.comments) {
        try {
            # Lazy fetch comments only if needed
            $commentsJson = gh pr view $PrJson.number --repo $Repo --json comments 2>$null | ConvertFrom-Json
            if ($commentsJson -and $commentsJson.comments) {
                $PrJson | Add-Member -NotePropertyName comments -NotePropertyValue $commentsJson.comments -Force
            }
        } catch {
            if ($VerboseMode) { Write-DebugMsg "Failed to fetch comments for PR #$($PrJson.number): $_" }
        }
    }
    if ($PrJson.comments) {
        $copilotComments = $PrJson.comments | Where-Object {
            ($candidateAuthors -contains $_.author.login -or $_.author.login -like '*copilot*') -and $_.body -and $_.body.Trim() -ne ''
        }
        if ($copilotComments) {
            $longestC = $copilotComments | Sort-Object { $_.body.Length } -Descending | Select-Object -First 1
            if ($longestC) {
                $body = $longestC.body
                $norm = ($body -replace "`r", '') -replace "`n", ' '
                $norm = $norm -replace '\s+', ' '
                $result.Summary = $norm
                $result.Source = 'comment'
                if ($VerboseMode) { Write-DebugMsg "Selected Copilot comment length=$($body.Length) (longest)." }
                return $result
            }
        }
    }

    return $result
}

foreach ($pr in $prNumbers) {
    Write-Info "Fetching PR #$pr ..."
    try {
        # Include comments only if Verbose asked; if not, we lazily pull when reviews are missing
        $fields = 'number,title,labels,author,url,body,reviews'
        if ($PSBoundParameters.ContainsKey('Verbose')) { $fields += ',comments' }
        $json = gh pr view $pr --repo $Repo --json $fields 2>$null | ConvertFrom-Json
        if ($null -eq $json) { throw "Empty response" }

        $copilot = Get-CopilotSummaryFromPrJson -PrJson $json -VerboseMode:($PSBoundParameters.ContainsKey('Verbose'))
        if ($copilot.Summary -and $copilot.Source -and $PSBoundParameters.ContainsKey('Verbose')) {
            Write-DebugMsg "Copilot summary source=$($copilot.Source) chars=$($copilot.Summary.Length)"
        } elseif (-not $copilot.Summary) {
            Write-DebugMsg "No Copilot summary found for PR #$pr"
        }

        # Filter labels
        $filteredLabels = $json.labels | Where-Object {
            ($_.name -like "Product-*") -or 
            ($_.name -like "Area-*") -or 
            ($_.name -like "GitHub*") -or 
            ($_.name -like "*Plugin") -or 
            ($_.name -like "Issue-*")
        }
        $labelNames = ($filteredLabels | ForEach-Object { $_.name }) -join ", "

        $bodyValue = if ($json.body) { ($json.body -replace "`r", '') -replace "`n", ' ' } else { '' }
        $bodyValue = $bodyValue -replace '\s+', ' '

        # Determine if author needs thanks (not in member list)
        $authorLogin = $json.author.login
        $needThanks = $true
        if ($memberList.Count -gt 0 -and $authorLogin) {
            $needThanks = -not ($memberList -contains $authorLogin)
        }

        $prDetails += [PSCustomObject]@{
            Id = $json.number
            Title = $json.title
            Labels = $labelNames
            Author = $authorLogin
            Url = $json.url
            Body = $bodyValue
            CopilotSummary = $copilot.Summary
            NeedThanks = $needThanks
        }
    }
    catch {
        $err = $_
        Write-Warn ("Failed to fetch PR #{0}: {1}" -f $pr, $err)
    }
}

if (-not $prDetails) { Write-Warn "No PR details fetched."; exit 0 }

# Sort by Labels like original script (first label alphabetical)
$sorted = $prDetails | Sort-Object { ($_.Labels -split ',')[0] }

# Output JSON raw (optional)
$sorted | ConvertTo-Json -Depth 6 | Out-File -Encoding UTF8 $OutputJson

Write-Info "Saving CSV to $OutputCsv ..."
$sorted | Export-Csv $OutputCsv -NoTypeInformation
Write-Host "✅ Done. Generated $($prDetails.Count) PR rows." -ForegroundColor Green
