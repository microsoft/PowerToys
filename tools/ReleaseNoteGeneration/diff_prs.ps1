param(
    [string]$BaseCsv = ".\sorted_prs_93_round1.csv",
    [string]$AllCsv  = ".\sorted_prs.csv",
    [string]$OutCsv  = ".\sorted_prs_93_incremental.csv",
    [string]$Key     = "Number"
)

# Fail on errors
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $BaseCsv)) {
    throw "Base CSV not found: $BaseCsv"
}
if (-not (Test-Path -LiteralPath $AllCsv)) {
    throw "All CSV not found: $AllCsv"
}

$baseRows = Import-Csv -LiteralPath $BaseCsv
$allRows  = Import-Csv -LiteralPath $AllCsv

# Build a set of existing keys from base
$set = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($row in $baseRows) {
    $val = [string]($row.$Key)
    if ($null -ne $val) { [void]$set.Add($val) }
}

# Filter rows in AllCsv whose key is not in base
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

if ($incremental.Count -gt 0) {
    if ($columns.Count -gt 0) {
        $incremental | Select-Object -Property $columns | Export-Csv -LiteralPath $OutCsv -NoTypeInformation -Encoding UTF8
    } else {
        $incremental | Export-Csv -LiteralPath $OutCsv -NoTypeInformation -Encoding UTF8
    }
} else {
    # Write an empty CSV with headers if we know them
    if ($columns.Count -gt 0) {
        # Create one empty object with the same properties to export just headers
        $obj = [PSCustomObject]@{}
        foreach ($c in $columns) { $obj | Add-Member -NotePropertyName $c -NotePropertyValue $null }
        $obj | Select-Object -Property $columns | Export-Csv -LiteralPath $OutCsv -NoTypeInformation -Encoding UTF8
    } else {
        '' | Out-File -LiteralPath $OutCsv -Encoding UTF8
    }
}

Write-Host ("Incremental rows: {0}" -f $incremental.Count)
Write-Host ("Output: {0}" -f (Resolve-Path -LiteralPath $OutCsv))
