# Clear Copilot context files
# This script removes AGENTS.md and related copilot instruction files

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $repoRoot) {
    $repoRoot = (Get-Location).Path
}

$filesToRemove = @(
    "AGENTS.md",
    ".github\instructions\runner-settings-ui.instructions.md",
    ".github\instructions\common-libraries.instructions.md"
)

foreach ($file in $filesToRemove) {
    $filePath = Join-Path $repoRoot $file
    if (Test-Path $filePath) {
        Remove-Item $filePath -Force
        Write-Host "Removed: $filePath" -ForegroundColor Green
    } else {
        Write-Host "Not found: $filePath" -ForegroundColor Yellow
    }
}

Write-Host "Done." -ForegroundColor Cyan
