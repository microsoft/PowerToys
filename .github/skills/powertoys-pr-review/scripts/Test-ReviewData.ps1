<#
.SYNOPSIS
    Validate PowerToys PR review data before approval or posting.
.DESCRIPTION
    Enforces schema version 2, separates public payloads from internal evidence,
    rejects fork/internal references in public comments, verifies apply-ready
    suggestion blocks, and optionally validates current GitHub diff ranges.
.PARAMETER DataPath
    Path to review-data.json.
.PARAMETER DecisionsPath
    Optional path to review-decisions.json. When provided, validates its hash,
    actions, selected item IDs, and edited public context.
.PARAMETER AllowIncomplete
    Allow queued or in-progress PR entries. Ready entries are still fully validated.
.PARAMETER CheckGitHub
    Verify pinned heads and inline ranges against the current upstream PR diff.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DataPath,
    [string]$DecisionsPath,
    [switch]$AllowIncomplete,
    [switch]$CheckGitHub
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ReviewPayload.Common.ps1')

$reviewData = Read-JsonFile -Path $DataPath
$errors = [System.Collections.Generic.List[string]]::new()
foreach ($errorMessage in Test-ReviewDataDocument -Document $reviewData -AllowIncomplete:$AllowIncomplete -CheckGitHub:$CheckGitHub) {
    $errors.Add($errorMessage)
}

if ($DecisionsPath) {
    $decisions = Read-JsonFile -Path $DecisionsPath
    $hash = Get-ReviewDataHash -Path $DataPath
    foreach ($errorMessage in Test-ReviewDecisionDocument -Decisions $decisions -ReviewData $reviewData -ExpectedHash $hash) {
        $errors.Add($errorMessage)
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Review payload validation passed ($(@($reviewData.prs).Count) PRs)." -ForegroundColor Green
