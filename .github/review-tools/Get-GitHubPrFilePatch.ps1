<#
.SYNOPSIS
    Retrieves the unified diff patch for a specific file in a GitHub pull request.

.DESCRIPTION
    This script fetches the patch content (unified diff format) for a specified file
    within a pull request. It uses the GitHub CLI (gh) to query the GitHub API and
    retrieve file change information.

.PARAMETER PullRequestNumber
    The pull request number to query.

.PARAMETER FilePath
    The relative path to the file in the repository (e.g., "src/modules/main.cpp").

.PARAMETER RepositoryOwner
    The GitHub repository owner. Defaults to "microsoft".

.PARAMETER RepositoryName
    The GitHub repository name. Defaults to "PowerToys".

.EXAMPLE
    .\Get-GitHubPrFilePatch.ps1 -PullRequestNumber 42374 -FilePath "src/modules/cmdpal/main.cpp"
    Retrieves the patch for main.cpp in PR #42374.

.EXAMPLE
    .\Get-GitHubPrFilePatch.ps1 -PullRequestNumber 42374 -FilePath "README.md" -RepositoryOwner "myorg" -RepositoryName "myrepo"
    Retrieves the patch from a different repository.

.NOTES
    Requires GitHub CLI (gh) to be installed and authenticated.
    Run 'gh auth login' if not already authenticated.

.LINK
    https://cli.github.com/
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = "Pull request number")]
    [int]$PullRequestNumber,

    [Parameter(Mandatory = $true, HelpMessage = "Relative path to the file in the repository")]
    [string]$FilePath,

    [Parameter(Mandatory = $false, HelpMessage = "Repository owner")]
    [string]$RepositoryOwner = "microsoft",

    [Parameter(Mandatory = $false, HelpMessage = "Repository name")]
    [string]$RepositoryName = "PowerToys"
)

# Construct GitHub API path for pull request files
$apiPath = "repos/$RepositoryOwner/$RepositoryName/pulls/$PullRequestNumber/files?per_page=250"

# Query GitHub API to get all files in the pull request
try {
    $pullRequestFiles = gh api $apiPath | ConvertFrom-Json
} catch {
    Write-Error "Failed to query GitHub API for PR #$PullRequestNumber. Ensure gh CLI is authenticated. Details: $_"
    exit 1
}

# Find the matching file in the pull request
$matchedFile = $pullRequestFiles | Where-Object { $_.filename -eq $FilePath }

if (-not $matchedFile) {
    Write-Error "File '$FilePath' not found in PR #$PullRequestNumber."
    exit 1
}

# Check if patch content exists
if (-not $matchedFile.patch) {
    Write-Warning "File '$FilePath' has no patch content (possibly binary or too large)."
    return
}

# Output the patch content
$matchedFile.patch
