<#
.SYNOPSIS
    Collects normalized PowerToys release-note metadata for explicit PRs.

.DESCRIPTION
    Preview releases already have a semantic PR set, so this script skips
    milestone and label mutation. Existing labels are filtered using the
    release-note conventions and unlabeled PRs are assigned to General.

.EXAMPLE
    .\collect-pr-metadata.ps1 -DeltaPath .\delta-prs.json -OutputDirectory .\preview-154000000
#>
[CmdletBinding()]
param(
    [int[]]$PrNumbers,
    [string]$DeltaPath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$Repo = "microsoft/PowerToys",
    [string]$MemberListPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($DeltaPath) {
    $delta = @(Get-Content -LiteralPath $DeltaPath -Raw | ConvertFrom-Json)
    $PrNumbers = @($delta | ForEach-Object { [int]$_.number })
}

$PrNumbers = @($PrNumbers | Where-Object { $_ -gt 0 } | Sort-Object -Unique)
if (-not $PSBoundParameters.ContainsKey("PrNumbers") -and -not $DeltaPath) {
    throw "Provide either -PrNumbers or -DeltaPath."
}
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI ('gh') is required. Install it and run 'gh auth login'."
}

if (-not $MemberListPath) {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path
    $MemberListPath = Join-Path $repoRoot "Generated Files\ReleaseNotes\MemberList.md"
}

if (-not (Test-Path -LiteralPath $MemberListPath -PathType Leaf)) {
    throw "Required PowerToys member list not found: $MemberListPath"
}

$members = @(
    Get-Content -LiteralPath $MemberListPath |
        Where-Object { $_ -notmatch '^\s*```' -and -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { $_.Trim() }
)
if ($members.Count -eq 0) {
    throw "Required PowerToys member list is empty: $MemberListPath"
}

$memberSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($member in $members) {
    [void]$memberSet.Add($member)
}

$rows = @()
foreach ($number in $PrNumbers) {
    Write-Host "Fetching PR #$number..." -ForegroundColor Cyan
    $json = gh pr view $number `
        --repo $Repo `
        --json number,title,labels,author,url,body,mergedAt
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) {
        throw "Failed to fetch required PR #$number from $Repo."
    }

    $pr = $json | ConvertFrom-Json
    $labels = @($pr.labels | ForEach-Object { $_.name } | Where-Object {
        $_ -like "Product-*" -or
        $_ -like "Area-*" -or
        $_ -like "GitHub*" -or
        $_ -like "*Plugin" -or
        $_ -like "Issue-*"
    })
    if ($labels.Count -eq 0) {
        $labels = @("General")
    }

    $author = [string]$pr.author.login
    $needThanks = if ($author -and -not $memberSet.Contains($author)) { $author } else { "" }
    $body = if ($pr.body) {
        (([string]$pr.body -replace "`r", "") -replace "`n", " ") -replace "\s+", " "
    }
    else {
        ""
    }

    $rows += [pscustomobject]@{
        Id = [int]$pr.number
        Title = [string]$pr.title
        Labels = ($labels -join ", ")
        Author = $author
        Url = [string]$pr.url
        Body = $body
        CopilotSummary = ""
        NeedThanks = $needThanks
        MergedAt = [string]$pr.mergedAt
    }
}

$sorted = @($rows | Sort-Object @{ Expression = { ($_.Labels -split ",")[0] } }, Id)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$jsonPath = Join-Path $OutputDirectory "milestone_prs.json"
$csvPath = Join-Path $OutputDirectory "sorted_prs.csv"

ConvertTo-Json -InputObject $sorted -Depth 6 | Set-Content -LiteralPath $jsonPath -Encoding utf8
if ($sorted.Count -gt 0) {
    $sorted | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding utf8
}
else {
    '"Id","Title","Labels","Author","Url","Body","CopilotSummary","NeedThanks","MergedAt"' |
        Set-Content -LiteralPath $csvPath -Encoding utf8
}

[pscustomobject]@{
    count = $sorted.Count
    jsonPath = (Resolve-Path -LiteralPath $jsonPath).Path
    csvPath = (Resolve-Path -LiteralPath $csvPath).Path
}
