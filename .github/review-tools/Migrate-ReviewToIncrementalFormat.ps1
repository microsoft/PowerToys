<#
.SYNOPSIS
    Migrates existing PR review overview files to include review metadata for incremental reviews.

.DESCRIPTION
    This one-time migration script adds a review metadata section to existing 00-OVERVIEW.md files
    that were created before the incremental review feature was implemented. The metadata section
    includes the current HEAD SHA, timestamp, review mode, and branch information, which enables
    incremental review functionality for future review iterations.

.PARAMETER PullRequestNumbers
    Array of specific PR numbers to migrate. If omitted, migrates all PR reviews found in the reviews folder.

.PARAMETER ReviewsFolderPath
    Path to the folder containing PR review subfolders. If not specified, defaults to
    "Generated Files\prReview" relative to the repository root.

.PARAMETER RepositoryOwner
    The GitHub repository owner. Defaults to "microsoft".

.PARAMETER RepositoryName
    The GitHub repository name. Defaults to "PowerToys".

.EXAMPLE
    .\Migrate-ReviewToIncrementalFormat.ps1
    Migrates all existing PR review folders found in the default location.

.EXAMPLE
    .\Migrate-ReviewToIncrementalFormat.ps1 -PullRequestNumbers 42374,42658,42762
    Migrates only the specified PR reviews.

.EXAMPLE
    .\Migrate-ReviewToIncrementalFormat.ps1 -ReviewsFolderPath "D:\CustomPath\Reviews"
    Migrates all reviews in a custom folder location.

.NOTES
    Requires GitHub CLI (gh) to be installed and authenticated.
    Run 'gh auth login' if not already authenticated.
    
    This script:
    - Fetches current PR state from GitHub
    - Adds metadata section after the "Changed files" line
    - Skips reviews that already have metadata
    - Preserves existing content

.LINK
    https://cli.github.com/
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false, HelpMessage = "Specific PR numbers to migrate")]
    [int[]]$PullRequestNumbers,

    [Parameter(Mandatory = $false, HelpMessage = "Path to reviews folder")]
    [string]$ReviewsFolderPath,

    [Parameter(Mandatory = $false, HelpMessage = "Repository owner")]
    [string]$RepositoryOwner = "microsoft",

    [Parameter(Mandatory = $false, HelpMessage = "Repository name")]
    [string]$RepositoryName = "PowerToys"
)

<#
.SYNOPSIS
    Adds review metadata section to a single PR overview file.

.DESCRIPTION
    Internal helper function that processes one overview file and adds the review metadata section
    if it doesn't already exist.

.PARAMETER OverviewFilePath
    Full path to the 00-OVERVIEW.md file to process.

.PARAMETER PullRequestNumber
    The PR number associated with this overview file.

.OUTPUTS
    String indicating the result: "migrated", "skipped", or "failed"
#>
function Add-ReviewMetadataSection {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OverviewFilePath,

        [Parameter(Mandatory = $true)]
        [int]$PullRequestNumber
    )

    if (-not (Test-Path $OverviewFilePath)) {
        Write-Warning "Overview not found: $OverviewFilePath"
        return "failed"
    }

    $fileContent = Get-Content $OverviewFilePath -Raw

    # Check if metadata section already exists
    if ($fileContent -match '## Review metadata') {
        Write-Host "  ✓ Already has review metadata" -ForegroundColor Green
        return "skipped"
    }

    Write-Host "  📝 Adding review metadata section..." -ForegroundColor Yellow

    # Fetch current PR state from GitHub
    try {
        $pullRequestData = gh pr view $PullRequestNumber --json headRefOid,headRefName,baseRefName,baseRefOid | ConvertFrom-Json
    } catch {
        Write-Warning "  Failed to fetch PR data: $_"
        return "failed"
    }

    # Find the insertion point (after "Changed files" line)
    $contentLines = $fileContent -split "`r?`n"
    $insertLineIndex = -1
    for ($i = 0; $i -lt $contentLines.Count; $i++) {
        if ($contentLines[$i] -match '^\*\*Changed files:') {
            $insertLineIndex = $i + 1
            break
        }
    }

    if ($insertLineIndex -eq -1) {
        Write-Warning "  Could not find insertion point (line starting with '**Changed files:')"
        return "failed"
    }

    # Build metadata section
    $currentTimestamp = Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ"
    $metadataSection = @"

## Review metadata
**Last reviewed SHA:** $($pullRequestData.headRefOid)
**Last review timestamp:** $currentTimestamp
**Review mode:** Full
**Base ref:** $($pullRequestData.baseRefName)
**Head ref:** $($pullRequestData.headRefName)
"@

    # Insert metadata into content
    $updatedLines = @($contentLines[0..($insertLineIndex - 1)]) + $metadataSection.Split("`n") + @($contentLines[$insertLineIndex..($contentLines.Count - 1)])
    $updatedContent = $updatedLines -join "`n"

    # Write updated content back to file
    Set-Content -Path $OverviewFilePath -Value $updatedContent -NoNewline

    Write-Host "  ✅ Added metadata (SHA: $($pullRequestData.headRefOid.Substring(0, 7)))" -ForegroundColor Green
    return "migrated"
}

#
# Main script logic
#

Write-Host "=== Migrating PR Reviews to Incremental Format ===" -ForegroundColor Cyan
Write-Host ""

# Resolve reviews folder path if not provided
if (-not $ReviewsFolderPath) {
    $repositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $ReviewsFolderPath = Join-Path $repositoryRoot "Generated Files\prReview"
}

# Determine which PRs to migrate
if ($PullRequestNumbers) {
    $targetPullRequests = $PullRequestNumbers
} else {
    # Find all PR review folders (folders with numeric names)
    if (-not (Test-Path $ReviewsFolderPath)) {
        Write-Error "Reviews folder not found: $ReviewsFolderPath"
        exit 1
    }

    $targetPullRequests = Get-ChildItem -Path $ReviewsFolderPath -Directory | 
                          Where-Object { $_.Name -match '^\d+$' } |
                          ForEach-Object { [int]$_.Name }
}

Write-Host "Found $($targetPullRequests.Count) PR review folder(s)" -ForegroundColor Cyan
Write-Host ""

# Process each PR review
$migratedCount = 0
$skippedCount = 0
$failedCount = 0

foreach ($prNumber in $targetPullRequests) {
    Write-Host "PR #$prNumber" -ForegroundColor White
    $overviewFilePath = Join-Path $ReviewsFolderPath "$prNumber\00-OVERVIEW.md"

    $migrationResult = Add-ReviewMetadataSection -OverviewFilePath $overviewFilePath -PullRequestNumber $prNumber
    
    switch ($migrationResult) {
        "migrated" { $migratedCount++ }
        "skipped"  { $skippedCount++ }
        "failed"   { $failedCount++ }
    }
}

# Display summary
Write-Host ""
Write-Host "=== Migration Summary ===" -ForegroundColor Cyan
Write-Host "Migrated: $migratedCount" -ForegroundColor Green
Write-Host "Skipped: $skippedCount" -ForegroundColor Yellow
Write-Host "Failed: $failedCount" -ForegroundColor Red
