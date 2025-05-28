# Simple script to add AutomationProperties.AutomationId to XAML files
# Usage: .\simple-automation-id-adder.ps1 "filename.xaml"

param(
    [string]$FilePath
)

if (-not $FilePath) {
    Write-Host "Usage: .\simple-automation-id-adder.ps1 'filename.xaml'"
    exit
}

$content = Get-Content -Path $FilePath -Raw -Encoding UTF8

# Simple replacements for common patterns
$content = $content -replace '(\s+x:Uid="([^"]+)")', '$1`r`n    AutomationProperties.AutomationId="$2"'

Set-Content -Path $FilePath -Value $content -Encoding UTF8 -NoNewline

Write-Host "Updated $FilePath" 