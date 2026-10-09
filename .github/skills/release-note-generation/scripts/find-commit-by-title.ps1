<#
.SYNOPSIS
    Find a commit on a branch that has the same subject line as a reference commit.

.DESCRIPTION
    Given a commit SHA (often from a release tag) and a branch name, this script
    resolves the reference commit's subject, then searches the branch history for
    commits with the exact same subject line. Useful when the release tag commit
    is not reachable from your current branch history.

.PARAMETER Commit
    The reference commit SHA or ref (e.g., v0.96.1 or a full SHA).

.PARAMETER Branch
    The branch to search (e.g., stable or main). Defaults to stable.

.PARAMETER RepoPath
    Path to the local repo. Defaults to current directory.

.EXAMPLE
    pwsh ./find-commit-by-title.ps1 -Commit b62f6421845f7e5c92b8186868d98f46720db442 -Branch stable
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Commit,
    [string]$Branch = "stable",
    [string]$RepoPath = "."
)

function Write-Info($msg) { Write-Host $msg -ForegroundColor Cyan }
function Write-Err($msg) { Write-Host $msg -ForegroundColor Red }

Push-Location $RepoPath
try {
    Write-Info "Fetching latest '$Branch' from origin (with tags)..."
    git fetch origin $Branch --tags | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "git fetch origin $Branch --tags failed" }

    $commitSha = (git rev-parse --verify $Commit) 2>$null
    if (-not $commitSha) { throw "Commit '$Commit' not found" }

    $subject = (git show -s --format=%s $commitSha) 2>$null
    if (-not $subject) { throw "Unable to read subject for '$commitSha'" }

    $branchRef = $Branch
    $branchSha = (git rev-parse --verify $branchRef) 2>$null
    if (-not $branchSha) {
        $branchRef = "origin/$Branch"
        $branchSha = (git rev-parse --verify $branchRef) 2>$null
    }
    if (-not $branchSha) { throw "Branch '$Branch' not found" }

    Write-Info "Reference commit: $commitSha"
    Write-Info "Reference title:  $subject"
    Write-Info "Searching branch: $branchRef"

    $matches = git log $branchRef --format="%H|%s" | Where-Object { $_ -match '\|' }
    $results = @()
    foreach ($line in $matches) {
        $parts = $line -split '\|', 2
        if ($parts.Count -eq 2 -and $parts[1] -eq $subject) {
            $results += [PSCustomObject]@{ Sha = $parts[0]; Title = $parts[1] }
        }
    }

    if (-not $results -or $results.Count -eq 0) {
        Write-Info "No matching commit found on $branchRef for the given title."
        exit 0
    }

    Write-Info ("Found {0} matching commit(s):" -f $results.Count)
    $results | ForEach-Object { Write-Host ("{0}  {1}" -f $_.Sha, $_.Title) }
}
catch {
    Write-Err $_
    exit 1
}
finally {
    Pop-Location
}
