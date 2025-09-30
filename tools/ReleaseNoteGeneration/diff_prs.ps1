<#
.SYNOPSIS
    Produce an incremental PR CSV containing rows present in a newer full export but absent from a baseline export.

.DESCRIPTION
    Compares two previously generated sorted PR CSV files (same schema). Any row whose key column value
    (defaults to 'Number') does not exist in the baseline file is emitted to a new incremental CSV, preserving
    the original column order. If no new rows are found, an empty CSV (with headers when determinable) is written.

.PARAMETER BaseCsv
    Path to the baseline (earlier) PR CSV.

.PARAMETER AllCsv
    Path to the newer full PR CSV containing superset (or equal set) of rows.

.PARAMETER OutCsv
    Path to write the incremental CSV containing only new rows.

.PARAMETER Key
    Column name used as unique identifier (defaults to 'Number'). Must exist in both CSVs.

.EXAMPLE
    pwsh ./diff_prs.ps1 -BaseCsv sorted_prs_prev.csv -AllCsv sorted_prs.csv -OutCsv sorted_prs_incremental.csv

.NOTES
    Requires: PowerShell 7+, both CSVs with identical column schemas.
    Exit code 0 on success (even if zero incremental rows). Throws on missing files.
#>

[CmdletBinding()] param(
    [Parameter(Mandatory=$false)][string]$BaseCsv = "./sorted_prs_93_round1.csv",
    [Parameter(Mandatory=$false)][string]$AllCsv  = "./sorted_prs.csv",
    [Parameter(Mandatory=$false)][string]$OutCsv  = "./sorted_prs_93_incremental.csv",
    [Parameter(Mandatory=$false)][string]$Key     = "Number"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Info($m) { Write-Host "[info] $m" -ForegroundColor Cyan }
function Write-Warn($m) { Write-Host "[warn] $m" -ForegroundColor Yellow }

if (-not (Test-Path -LiteralPath $BaseCsv)) { throw "Base CSV not found: $BaseCsv" }
if (-not (Test-Path -LiteralPath $AllCsv)) { throw "All CSV not found: $AllCsv" }

# Load CSVs
$baseRows = Import-Csv -LiteralPath $BaseCsv
$allRows  = Import-Csv -LiteralPath $AllCsv

if (-not $baseRows) { Write-Warn "Base CSV has no rows." }
if (-not $allRows)  { Write-Warn "All CSV has no rows." }

# Validate key presence
if ($baseRows -and -not ($baseRows[0].PSObject.Properties.Name -contains $Key)) { throw "Key column '$Key' not found in base CSV." }
if ($allRows  -and -not ($allRows[0].PSObject.Properties.Name -contains $Key))  { throw "Key column '$Key' not found in all CSV." }

# Build a set of existing keys from base
$set = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($row in $baseRows) {
    $val = [string]($row.$Key)
    if ($null -ne $val) { [void]$set.Add($val) }
}

# Filter rows in AllCsv whose key is not in base (these are the new / incremental rows)
$incremental = @()
foreach ($row in $allRows) {
    $val = [string]($row.$Key)
    if (-not $set.Contains($val)) { $incremental += $row }
}

# Preserve column order from the All CSV
$columns = @()
if ($allRows.Count -gt 0) {
    $columns = $allRows[0].PSObject.Properties.Name
}

try {
    if ($incremental.Count -gt 0) {
        if ($columns.Count -gt 0) {
            $incremental | Select-Object -Property $columns | Export-Csv -LiteralPath $OutCsv -NoTypeInformation -Encoding UTF8
        } else {
            $incremental | Export-Csv -LiteralPath $OutCsv -NoTypeInformation -Encoding UTF8
        }
    } else {
        # Write an empty CSV with headers if we know them (facilitates downstream tooling expecting header row)
        if ($columns.Count -gt 0) {
            $obj = [PSCustomObject]@{}
            foreach ($c in $columns) { $obj | Add-Member -NotePropertyName $c -NotePropertyValue $null }
            $obj | Select-Object -Property $columns | Export-Csv -LiteralPath $OutCsv -NoTypeInformation -Encoding UTF8
        } else {
            '' | Out-File -LiteralPath $OutCsv -Encoding UTF8
        }
    }
    Write-Info ("Incremental rows: {0}" -f $incremental.Count)
    Write-Info ("Output: {0}" -f (Resolve-Path -LiteralPath $OutCsv))
}
catch {
    Write-Host "[error] Failed writing output CSV: $_" -ForegroundColor Red
    exit 1
}
