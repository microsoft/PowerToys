<#
.SYNOPSIS
    Group PR rows by their Labels column and emit per-label CSV files.

.DESCRIPTION
    Reads a milestone PR CSV (usually produced by dump-prs-information / dump-prs-since-commit scripts),
    splits rows by label list, normalizes/sorts individual labels, and writes one CSV per unique label combination.
    Each output preserves the original row ordering within that subset and column order from the source.

.PARAMETER CsvPath
    Input CSV containing PR rows with a 'Labels' column (comma-separated list).

.PARAMETER OutDir
    Output directory to place grouped CSVs (created if missing). Default: 'grouped_csv'.

.NOTES
    Label combinations are joined using ' | ' when multiple labels present. Filenames are sanitized (invalid characters,
    whitespace collapsed) and truncated to <= 120 characters.
#>
param(
    [string]$CsvPath = "sorted_prs.csv",
    [string]$OutDir = "grouped_csv"
)

$ErrorActionPreference = 'Stop'

function Write-Info($msg) { Write-Host "[info] $msg" -ForegroundColor Cyan }
function Write-Warn($msg) { Write-Host "[warn] $msg" -ForegroundColor Yellow }

if (-not (Test-Path -LiteralPath $CsvPath)) { throw "CSV not found: $CsvPath" }

Write-Info "Reading CSV: $CsvPath"
$rows = Import-Csv -LiteralPath $CsvPath
Write-Info ("Loaded {0} rows" -f $rows.Count)

function ConvertTo-SafeFileName {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][string]$Name
    )
    if ([string]::IsNullOrWhiteSpace($Name)) { return 'Unnamed' }
    $s = $Name -replace '[<>:"/\\|?*]', '-'             # invalid path chars
    $s = $s -replace '\s+', '-'                          # spaces to dashes
    $s = $s -replace '-{2,}', '-'                         # collapse dashes
    $s = $s.Trim('-')
    if ($s.Length -gt 120) { $s = $s.Substring(0,120).Trim('-') }
    if ([string]::IsNullOrWhiteSpace($s)) { return 'Unnamed' }
    return $s
}

# Build groups keyed by normalized, sorted label combinations. Preserve original CSV row order.
$groups = @{}
foreach ($row in $rows) {
    $labelsRaw = $row.Labels
    if ([string]::IsNullOrWhiteSpace($labelsRaw)) {
        $labelParts = @('Unlabeled')
    } else {
        $parts = $labelsRaw -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
        if (-not $parts -or $parts.Count -eq 0) { $labelParts = @('Unlabeled') }
        else { $labelParts = $parts | Sort-Object }
    }

    $key = ($labelParts -join ' | ')
    if (-not $groups.ContainsKey($key)) { $groups[$key] = New-Object System.Collections.ArrayList }
    [void]$groups[$key].Add($row)
}

if (-not (Test-Path -LiteralPath $OutDir)) {
    Write-Info "Creating output directory: $OutDir"
    New-Item -ItemType Directory -Path $OutDir | Out-Null
}

Write-Info ("Generating {0} grouped CSV file(s) into: {1}" -f $groups.Count, $OutDir)

foreach ($key in $groups.Keys) {
    $labelParts = if ($key -eq 'Unlabeled') { @('Unlabeled') } else { $key -split '\s\|\s' }
    $safeName = ($labelParts | ForEach-Object { ConvertTo-SafeFileName -Name $_ }) -join '-'
    $filePath = Join-Path $OutDir ("$safeName.csv")

    # Keep same columns and order
    $groups[$key] | Export-Csv -LiteralPath $filePath -NoTypeInformation -Encoding UTF8
}

Write-Info "Done. Sample output files:"
Get-ChildItem -LiteralPath $OutDir | Select-Object -First 10 Name | Format-Table -HideTableHeaders
