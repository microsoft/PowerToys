<#
.SYNOPSIS
    Assign milestone 'PowerToys 0.95' to PRs with empty milestone entries from a two-column CSV.

.DESCRIPTION
    Reads a CSV (default: prs_with_milestone.csv) with columns Id,Milestone. For each row whose Milestone value
    is blank (null/empty/whitespace), checks on GitHub if the PR already has a milestone. If not, assigns the
    target milestone (default: 'PowerToys 0.95') by resolving its numeric milestone number once and PATCHing
    the PR via the Issues API (gh api).

.PARAMETER CsvPath
    Input CSV path (must include Id and Milestone columns). Default: prs_with_milestone.csv

.PARAMETER Repo
    GitHub repository owner/name. Default: microsoft/PowerToys

.PARAMETER MilestoneTitle
    Title of the milestone to assign. Default: 'PowerToys 0.95'

.PARAMETER WhatIf
    Dry run; show intended actions without calling the API.

.EXAMPLE
    pwsh ./set-milestones-missing.ps1

.EXAMPLE
    pwsh ./set-milestones-missing.ps1 -WhatIf

.EXAMPLE
    pwsh ./set-milestones-missing.ps1 -Repo yourfork/PowerToys -MilestoneTitle 'PowerToys 0.96'

.NOTES
    Requires: gh CLI authenticated with repo scope (read/write issues). PRs are issues under the hood.
#>
[CmdletBinding()] param(
    [Parameter(Mandatory=$false)][string]$CsvPath = 'prs_with_milestone.csv',
    [Parameter(Mandatory=$false)][string]$Repo = 'microsoft/PowerToys',
    [Parameter(Mandatory=$false)][string]$MilestoneTitle = 'PowerToys 0.95',
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
function Write-Info($m){ Write-Host "[info] $m" -ForegroundColor Cyan }
function Write-Warn($m){ Write-Host "[warn] $m" -ForegroundColor Yellow }
function Write-Err($m){ Write-Host "[error] $m" -ForegroundColor Red }

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { Write-Err "GitHub CLI 'gh' not found in PATH"; exit 1 }
if (-not (Test-Path -LiteralPath $CsvPath)) { Write-Err "CSV not found: $CsvPath"; exit 1 }

$rows = Import-Csv -LiteralPath $CsvPath
if (-not $rows) { Write-Info "No rows in CSV."; exit 0 }
$firstCols = $rows[0].PSObject.Properties.Name
if (-not ($firstCols -contains 'Id' -and $firstCols -contains 'Milestone')) { Write-Err "CSV must contain 'Id' and 'Milestone' columns"; exit 1 }

Write-Info "Resolving milestone id for '$MilestoneTitle' ..."
# Fetch all open milestones and find the one with matching title
$milestonesRaw = gh api repos/$Repo/milestones --paginate --jq '.[] | {number,title,state}'
$msObj = $milestonesRaw | ConvertFrom-Json | Where-Object { $_.title -eq $MilestoneTitle -and $_.state -eq 'open' } | Select-Object -First 1
if (-not $msObj) { Write-Err "Milestone '$MilestoneTitle' not found (ensure it is open)"; exit 1 }
$msNumber = $msObj.number
Write-Info "Milestone number: $msNumber"

# Collect candidates with empty milestone cell
$candidates = $rows | Where-Object { [string]::IsNullOrWhiteSpace($_.Milestone) }
Write-Info ("Found {0} rows with empty milestone column." -f $candidates.Count)
if ($candidates.Count -eq 0) { Write-Info "Nothing to update."; exit 0 }

$summary = New-Object System.Collections.Generic.List[object]

foreach ($r in $candidates) {
    $id = $r.Id
    if (-not $id) { continue }
    try {
        $existing = gh pr view $id --repo $Repo --json milestone --jq '.milestone.title // ""'
        if ($existing) {
            $summary.Add([PSCustomObject]@{ Id=$id; Action='Skip (already has milestone)'; Milestone=$existing; Status='OK' }) | Out-Null
            continue
        }
        if ($WhatIf) {
            $summary.Add([PSCustomObject]@{ Id=$id; Action='Would set'; Milestone=$MilestoneTitle; Status='DRYRUN' }) | Out-Null
            continue
        }
        gh api -X PATCH -H 'Accept: application/vnd.github+json' repos/$Repo/issues/$id -f milestone=$msNumber | Out-Null
        $summary.Add([PSCustomObject]@{ Id=$id; Action='Set'; Milestone=$MilestoneTitle; Status='OK' }) | Out-Null
    }
    catch {
        $errText = $_ | Out-String
        $summary.Add([PSCustomObject]@{ Id=$id; Action='Failed'; Milestone=$MilestoneTitle; Status=$errText.Trim() }) | Out-Null
        Write-Warn ("Failed to set milestone for PR #{0}: {1}" -f $id, ($errText.Trim()))
    }
}

$summary | Sort-Object Id | Format-Table
$updated =  ($summary | Where-Object { $_.Action -eq 'Set' }).Count
$skipped =  ($summary | Where-Object { $_.Action -like 'Skip*' }).Count
$failed  =  ($summary | Where-Object { $_.Action -eq 'Failed' }).Count
Write-Info ("Updated: {0}  Skipped: {1}  Failed: {2}" -f $updated, $skipped, $failed)

# Return objects for further scripting
return $summary
