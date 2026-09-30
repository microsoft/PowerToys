# A descriptor-authored PowerScript: this file carries NO @powerscript.* header. Its entire contract
# lives in the sibling word_count.ps1.tool.json (an MCP Tool). PowerScripts passes the selected files
# as trailing positional arguments (see x-execute), and also in $env:POWERSCRIPTS_FILES.

param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Files
)

foreach ($file in $Files) {
    if (-not (Test-Path -LiteralPath $file)) {
        Write-Error "File not found: $file"
        continue
    }

    $text = Get-Content -LiteralPath $file -Raw
    $words = ($text -split '\s+' | Where-Object { $_ -ne '' }).Count
    Write-Output ("{0}: {1} words" -f (Split-Path -Leaf $file), $words)
}
