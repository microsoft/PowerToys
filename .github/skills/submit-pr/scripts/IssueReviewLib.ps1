# IssueReviewLib.ps1 - Minimal helpers for PR submission workflow
# Part of the PowerToys GitHub Copilot/Claude Code issue review system
# This is a trimmed version - submit-pr only needs console helpers and repo root

#region Console Output Helpers
function Info { param([string]$Message) Write-Host $Message -ForegroundColor Cyan }
function Warn { param([string]$Message) Write-Host $Message -ForegroundColor Yellow }
function Err  { param([string]$Message) Write-Host $Message -ForegroundColor Red }
function Success { param([string]$Message) Write-Host $Message -ForegroundColor Green }
#endregion

#region Repository Helpers
function Get-RepoRoot {
    $root = git rev-parse --show-toplevel 2>$null
    if (-not $root) { throw 'Not inside a git repository.' }
    return (Resolve-Path $root).Path
}
#endregion
