<#
.SYNOPSIS
    Downloads and displays the content of a file from a GitHub repository at a specific git reference.

.DESCRIPTION
    This script fetches the raw content of a file from a GitHub repository using GitHub's raw content API.
    It can optionally display line numbers and supports any valid git reference (branch, tag, or commit SHA).

.PARAMETER FilePath
    The relative path to the file in the repository (e.g., "src/modules/main.cpp").

.PARAMETER GitReference
    The git reference (branch name, tag, or commit SHA) to fetch the file from. Defaults to "main".

.PARAMETER RepositoryOwner
    The GitHub repository owner. Defaults to "microsoft".

.PARAMETER RepositoryName
    The GitHub repository name. Defaults to "PowerToys".

.PARAMETER ShowLineNumbers
    When specified, displays line numbers before each line of content.

.PARAMETER StartLineNumber
    The starting line number to use when ShowLineNumbers is enabled. Defaults to 1.

.EXAMPLE
    .\Get-GitHubRawFile.ps1 -FilePath "README.md" -GitReference "main"
    Downloads and displays the README.md file from the main branch.

.EXAMPLE
    .\Get-GitHubRawFile.ps1 -FilePath "src/runner/main.cpp" -GitReference "dev/feature-branch" -ShowLineNumbers
    Downloads main.cpp from a feature branch and displays it with line numbers.

.EXAMPLE
    .\Get-GitHubRawFile.ps1 -FilePath "LICENSE" -GitReference "abc123def" -ShowLineNumbers -StartLineNumber 10
    Downloads the LICENSE file from a specific commit and displays it with line numbers starting at 10.

.NOTES
    Requires internet connectivity to access GitHub's raw content API.
    Does not require GitHub CLI authentication for public repositories.

.LINK
    https://docs.github.com/en/rest/repos/contents
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = "Relative path to the file in the repository")]
    [string]$FilePath,

    [Parameter(Mandatory = $false, HelpMessage = "Git reference (branch, tag, or commit SHA)")]
    [string]$GitReference = "main",

    [Parameter(Mandatory = $false, HelpMessage = "Repository owner")]
    [string]$RepositoryOwner = "microsoft",

    [Parameter(Mandatory = $false, HelpMessage = "Repository name")]
    [string]$RepositoryName = "PowerToys",

    [Parameter(Mandatory = $false, HelpMessage = "Display line numbers before each line")]
    [switch]$ShowLineNumbers,

    [Parameter(Mandatory = $false, HelpMessage = "Starting line number for display")]
    [int]$StartLineNumber = 1
)

# Construct the raw content URL
$rawContentUrl = "https://raw.githubusercontent.com/$RepositoryOwner/$RepositoryName/$GitReference/$FilePath"

# Fetch the file content from GitHub
try {
    $response = Invoke-WebRequest -UseBasicParsing -Uri $rawContentUrl
} catch {
    Write-Error "Failed to fetch file from $rawContentUrl. Details: $_"
    exit 1
}

# Split content into individual lines
$contentLines = $response.Content -split "`n"

# Display the content with or without line numbers
if ($ShowLineNumbers) {
    $currentLineNumber = $StartLineNumber
    foreach ($line in $contentLines) {
        Write-Output ("{0:d4}: {1}" -f $currentLineNumber, $line)
        $currentLineNumber++
    }
} else {
    $contentLines | ForEach-Object { Write-Output $_ }
}
