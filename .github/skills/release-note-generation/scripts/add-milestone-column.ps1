<#
.SYNOPSIS
    Generate a two‑column CSV (Id,Milestone) from sorted_prs.csv assigning a milestone when missing.

.DESCRIPTION
    Reads an input PR export CSV (default: sorted_prs.csv) that contains at least an Id column.
    For each PR Id, this script (optionally) queries GitHub via the gh CLI to determine the current milestone.
    If the PR has no milestone (or gh query is skipped with -Offline), a default milestone value is assigned
    (default: 'PowerToys 0.95'). The resulting table (Id,Milestone) is written to an output CSV.

    By default the script DOES query GitHub (unless -Offline is specified) so you get the *actual* existing
    milestone when set. If you only want to blindly assign the default value to every row, pass -Offline.

.PARAMETER InputCsv
    Path to the source CSV containing at least an Id column. Default: sorted_prs.csv

.PARAMETER OutputCsv
    Path to write the resulting two‑column CSV. Default: prs_with_milestone.csv

.PARAMETER Repo
    GitHub repository (owner/name) used when querying PR metadata. Default: microsoft/PowerToys

.PARAMETER DefaultMilestone
    Milestone title to assign when a PR has no milestone (or when -Offline). Default: 'PowerToys 0.97'

.PARAMETER Offline
    When supplied, skip all GitHub lookups and assign DefaultMilestone to every PR.

.EXAMPLE
    pwsh ./add-milestone-column.ps1

.EXAMPLE
    pwsh ./add-milestone-column.ps1 -DefaultMilestone 'PowerToys 0.96' -OutputCsv new.csv

.EXAMPLE
    pwsh ./add-milestone-column.ps1 -Offline | tee output.csv

.NOTES
    Requires: gh CLI (unless -Offline). The gh CLI must be authenticated with repo read scope.
    The output format is always: "Id","Milestone".
#>
[CmdletBinding()] param(
    [Parameter(Mandatory=$false)][string]$InputCsv = 'sorted_prs.csv',
    [Parameter(Mandatory=$false)][string]$OutputCsv = 'prs_with_milestone.csv',
    [Parameter(Mandatory=$false)][string]$Repo = 'microsoft/PowerToys',
    [Parameter(Mandatory=$false)][string]$DefaultMilestone = 'PowerToys 0.97',
    [switch]$Offline
)

$ErrorActionPreference = 'Stop'

function Write-Info($m){ Write-Host "[info] $m" -ForegroundColor Cyan }
function Write-Warn($m){ Write-Host "[warn] $m" -ForegroundColor Yellow }
function Write-Err($m){ Write-Host "[error] $m" -ForegroundColor Red }

if (-not (Test-Path -LiteralPath $InputCsv)) { Write-Err "Input CSV not found: $InputCsv"; exit 1 }

$rows = Import-Csv -LiteralPath $InputCsv
if (-not $rows) { Write-Warn "Input CSV has no rows."; @() | Export-Csv -NoTypeInformation -LiteralPath $OutputCsv; exit 0 }
if (-not ($rows[0].PSObject.Properties.Name -contains 'Id')) { Write-Err "Input CSV missing required 'Id' column."; exit 1 }

if (-not $Offline) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { Write-Err "GitHub CLI 'gh' not found. Use -Offline to skip lookups."; exit 1 }
}

# Cache to avoid re-querying duplicates
$milestoneCache = @{}
$result = New-Object System.Collections.Generic.List[object]

$index = 0
foreach ($row in $rows) {
    $index++
    $id = $row.Id
    if (-not $id) { Write-Warn "Row $index missing Id; skipping"; continue }

    $ms = $null
    if ($Offline) {
        $ms = $DefaultMilestone
    } else {
        if ($milestoneCache.ContainsKey($id)) {
            $ms = $milestoneCache[$id]
        } else {
            try {
                $json = gh pr view $id --repo $Repo --json milestone 2>$null | ConvertFrom-Json
                if ($json -and $json.milestone -and $json.milestone.title) {
                    $ms = $json.milestone.title
                } else {
                    $ms = ""
                }
            } catch {
                Write-Warn "Failed to fetch PR #$id milestone: $_. Using default."; $ms = $DefaultMilestone
            }
            $milestoneCache[$id] = $ms
        }
    }

    $result.Add([PSCustomObject]@{ Id = $id; Milestone = $ms }) | Out-Null
}

$result | Export-Csv -LiteralPath $OutputCsv -NoTypeInformation -Encoding UTF8
Write-Info ("Wrote {0} rows -> {1}" -f $result.Count, (Resolve-Path -LiteralPath $OutputCsv))

# Emit to pipeline for easy chaining if user didn't capture file
return $result
