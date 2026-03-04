<#
.SYNOPSIS
    Apply labels to PRs from a CSV file.

.DESCRIPTION
    Reads a CSV with Id and Label columns and applies the specified label to each PR via GitHub CLI.
    Supports dry-run mode to preview changes before applying.

.PARAMETER InputCsv
    CSV file with Id and Label columns. Default: prs_to_label.csv

.PARAMETER Repo
    GitHub repository (owner/name). Default: microsoft/PowerToys

.PARAMETER WhatIf
    Dry run - show what would be applied without making changes.

.EXAMPLE
    pwsh ./apply-labels.ps1 -InputCsv 'Generated Files/ReleaseNotes/prs_to_label.csv'

.EXAMPLE
    pwsh ./apply-labels.ps1 -InputCsv 'Generated Files/ReleaseNotes/prs_to_label.csv' -WhatIf

.NOTES
    Requires: gh CLI authenticated with repo write access.
    
    Input CSV format:
    Id,Label
    12345,Product-Advanced Paste
    12346,Product-Settings
#>
[CmdletBinding()] param(
    [Parameter(Mandatory=$false)][string]$InputCsv = 'prs_to_label.csv',
    [Parameter(Mandatory=$false)][string]$Repo = 'microsoft/PowerToys',
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'

function Write-Info($m){ Write-Host "[info] $m" -ForegroundColor Cyan }
function Write-Warn($m){ Write-Host "[warn] $m" -ForegroundColor Yellow }
function Write-Err($m){ Write-Host "[error] $m" -ForegroundColor Red }
function Write-OK($m){ Write-Host "[ok] $m" -ForegroundColor Green }

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { Write-Err "GitHub CLI 'gh' not found in PATH"; exit 1 }
if (-not (Test-Path -LiteralPath $InputCsv)) { Write-Err "Input CSV not found: $InputCsv"; exit 1 }

$rows = Import-Csv -LiteralPath $InputCsv
if (-not $rows) { Write-Info "No rows in CSV."; exit 0 }

$firstCols = $rows[0].PSObject.Properties.Name
if (-not ($firstCols -contains 'Id' -and $firstCols -contains 'Label')) { 
    Write-Err "CSV must contain 'Id' and 'Label' columns"; exit 1 
}

Write-Info "Processing $($rows.Count) label assignments..."
if ($WhatIf) { Write-Warn "DRY RUN - no changes will be made" }

$applied = 0
$skipped = 0
$failed = 0

foreach ($row in $rows) {
    $id = $row.Id
    $label = $row.Label
    
    if ([string]::IsNullOrWhiteSpace($id) -or [string]::IsNullOrWhiteSpace($label)) {
        Write-Warn "Skipping row with empty Id or Label"
        $skipped++
        continue
    }
    
    if ($WhatIf) {
        Write-Info "Would apply label '$label' to PR #$id"
        $applied++
        continue
    }
    
    try {
        gh pr edit $id --repo $Repo --add-label $label 2>&1 | Out-Null
        Write-OK "Applied '$label' to PR #$id"
        $applied++
    } catch {
        Write-Warn "Failed to apply label to PR #${id}: $_"
        $failed++
    }
}

Write-Info ""
Write-Info "Summary: Applied=$applied Skipped=$skipped Failed=$failed"
