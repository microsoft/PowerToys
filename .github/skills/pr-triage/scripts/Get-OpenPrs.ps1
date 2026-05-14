<#
.SYNOPSIS
    Fetches all open pull requests from a GitHub repository with core metadata.

.DESCRIPTION
    Queries GitHub for all open PRs and returns structured data suitable for triage.
    Computes derived fields like age, staleness, and size category.

.PARAMETER Repository
    GitHub repository in owner/repo format. Default: microsoft/PowerToys

.PARAMETER PRNumbers
    Specific PR numbers to fetch. If provided, list/query filters are skipped.

.PARAMETER Limit
    Maximum number of PRs to fetch. Default: 500

.PARAMETER ExcludeDrafts
    If set, excludes draft PRs from results.

.PARAMETER MinAgeDays
    Only include PRs older than this many days. Default: 0 (all)

.PARAMETER Labels
    Filter by label(s). Multiple labels use AND logic.

.PARAMETER OutputPath
    Path to save JSON output. If not specified, outputs to pipeline.

.EXAMPLE
    .\Get-OpenPrs.ps1 -Repository "microsoft/PowerToys" -MinAgeDays 30
    Fetches all open PRs older than 30 days.

.EXAMPLE
    .\Get-OpenPrs.ps1 -ExcludeDrafts -OutputPath ".\all-prs.json"
    Fetches non-draft PRs and saves to JSON file.

.NOTES
    Requires: gh CLI authenticated with repo access.
#>
[CmdletBinding()]
param(
    [string]$Repository = "microsoft/PowerToys",
    [int[]]$PRNumbers,
    [int]$Limit = 500,
    [switch]$ExcludeDrafts,
    [int]$MinAgeDays = 0,
    [string[]]$Labels,
    [string]$OutputPath,
    [string]$LogPath
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path (Get-Location) 'Get-OpenPrs.log'
}

$logDir = Split-Path -Parent $LogPath
if (-not [string]::IsNullOrWhiteSpace($logDir) -and -not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

"[$(Get-Date -Format o)] Starting Get-OpenPrs" | Out-File -FilePath $LogPath -Encoding utf8 -Append

function Write-LogHost {
    [CmdletBinding()]
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [object[]]$Object,
        [object]$ForegroundColor,
        [object]$BackgroundColor,
        [switch]$NoNewline,
        [Object]$Separator
    )

    $message = [string]::Join(' ', ($Object | ForEach-Object { [string]$_ }))
    "[$(Get-Date -Format o)] $message" | Out-File -FilePath $LogPath -Encoding utf8 -Append

    $invokeParams = @{}
    if ($PSBoundParameters.ContainsKey('ForegroundColor') -and -not [string]::IsNullOrWhiteSpace([string]$ForegroundColor)) { $invokeParams.ForegroundColor = $ForegroundColor }
    if ($PSBoundParameters.ContainsKey('BackgroundColor') -and -not [string]::IsNullOrWhiteSpace([string]$BackgroundColor)) { $invokeParams.BackgroundColor = $BackgroundColor }
    if ($NoNewline) { $invokeParams.NoNewline = $true }
    if ($PSBoundParameters.ContainsKey('Separator')) { $invokeParams.Separator = $Separator }

    Microsoft.PowerShell.Utility\Write-Host @invokeParams -Object $message
}

function Write-Info($msg) { Write-LogHost $msg -ForegroundColor Cyan }
function Write-Warn($msg) { Write-LogHost $msg -ForegroundColor Yellow }

# Build gh query
if ($PRNumbers -and $PRNumbers.Count -gt 0) {
    Write-Info "Fetching selected PRs from ${Repository}: $($PRNumbers -join ', ')"
    $rawPrs = @()
    foreach ($n in ($PRNumbers | Sort-Object -Unique)) {
        try {
            $pr = gh pr view $n --repo $Repository --json number,title,author,createdAt,updatedAt,headRefName,baseRefName,labels,assignees,reviewRequests,isDraft,mergeable,url,additions,deletions,changedFiles,state | ConvertFrom-Json
            if ($pr -and $pr.state -eq 'OPEN') {
                $rawPrs += $pr
            } else {
                Write-Warn "PR #$n is not OPEN (skipped)."
            }
        } catch {
            Write-Warn "Failed to fetch PR #$n (skipped): $($_.Exception.Message)"
        }
    }
} else {
    $ghArgs = @(
        "pr", "list",
        "--repo", $Repository,
        "--state", "open",
        "--limit", $Limit,
        "--json", "number,title,author,createdAt,updatedAt,headRefName,baseRefName,labels,assignees,reviewRequests,isDraft,mergeable,url,additions,deletions,changedFiles"
    )

    if ($Labels) {
        foreach ($label in $Labels) {
            $ghArgs += "--label"
            $ghArgs += $label
        }
    }

    Write-Info "Fetching open PRs from $Repository..."
    $rawPrs = & gh @ghArgs | ConvertFrom-Json
}

if (-not $rawPrs) {
    Write-Warn "No PRs found."
    return
}

Write-Info "Found $($rawPrs.Count) open PRs. Processing..."

$now = Get-Date

# Process each PR
$processedPrs = $rawPrs | ForEach-Object {
    $pr = $_
    $createdAt = [DateTime]::Parse($pr.createdAt)
    $updatedAt = [DateTime]::Parse($pr.updatedAt)
    $ageInDays = [Math]::Floor(($now - $createdAt).TotalDays)
    $daysSinceUpdate = [Math]::Floor(($now - $updatedAt).TotalDays)
    $linesChanged = $pr.additions + $pr.deletions
    
    # Size category
    $sizeCategory = switch ($true) {
        ($linesChanged -lt 10)  { "XS" }
        ($linesChanged -lt 50)  { "S" }
        ($linesChanged -lt 200) { "M" }
        ($linesChanged -lt 500) { "L" }
        default                 { "XL" }
    }
    
    # Extract label names
    $labelNames = $pr.labels | ForEach-Object { $_.name }
    
    # Author login
    $authorLogin = $pr.author.login
    
    [PSCustomObject]@{
        Number = $pr.number
        Title = $pr.title
        Author = $authorLogin
        Url = $pr.url
        CreatedAt = $pr.createdAt
        UpdatedAt = $pr.updatedAt
        AgeInDays = $ageInDays
        DaysSinceUpdate = $daysSinceUpdate
        BaseRefName = $pr.baseRefName
        HeadRefName = $pr.headRefName
        Labels = $labelNames
        Assignees = ($pr.assignees | ForEach-Object { $_.login })
        ReviewRequests = ($pr.reviewRequests | ForEach-Object { $_.login })
        IsDraft = $pr.isDraft
        Mergeable = $pr.mergeable
        Additions = $pr.additions
        Deletions = $pr.deletions
        ChangedFiles = $pr.changedFiles
        LinesChanged = $linesChanged
        SizeCategory = $sizeCategory
    }
}

# Apply filters
if ($ExcludeDrafts) {
    $processedPrs = $processedPrs | Where-Object { -not $_.IsDraft }
}

if ($MinAgeDays -gt 0) {
    $processedPrs = $processedPrs | Where-Object { $_.AgeInDays -ge $MinAgeDays }
}

# Build output object
$output = [PSCustomObject]@{
    CollectedAt = $now.ToString("o")
    Repository = $Repository
    TotalCount = $processedPrs.Count
    Filters = @{
        ExcludeDrafts = $ExcludeDrafts.IsPresent
        MinAgeDays = $MinAgeDays
        Labels = $Labels
    }
    Prs = @($processedPrs)
}

Write-Info "Processed $($processedPrs.Count) PRs after filtering."

# Output
if ($OutputPath) {
    $output | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputPath -Encoding UTF8
    Write-Info "Saved to $OutputPath"
} else {
    $output | ConvertTo-Json -Depth 10
}
