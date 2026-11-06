function Get-GitHubTagCommit {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Repo,
        [Parameter(Mandatory)][string]$Tag
    )

    $stderrPath = [System.IO.Path]::GetTempFileName()
    try {
        $json = & gh api "repos/$Repo/commits/$Tag" 2>$stderrPath
        $exitCode = $LASTEXITCODE
        $stderr = Get-Content -LiteralPath $stderrPath -Raw -ErrorAction SilentlyContinue
        if ($exitCode -eq 0) {
            $sha = [string]($json | ConvertFrom-Json).sha
            if ($sha -notmatch "^[0-9a-fA-F]{40}$") {
                throw "GitHub returned an invalid commit for tag '$Tag'."
            }
            return $sha.ToLowerInvariant()
        }
        if ($stderr -match "(?s)(HTTP 404|Not Found|No commit found for SHA:.*HTTP 422)") {
            return $null
        }
        throw "Failed to resolve Git tag '$Tag' in '$Repo'. $stderr"
    }
    finally {
        Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

function Assert-GitHubTagTarget {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Tag,
        [AllowNull()][AllowEmptyString()][string]$ResolvedCommit,
        [Parameter(Mandatory)][string]$TargetCommit
    )

    if ([string]::IsNullOrWhiteSpace($ResolvedCommit)) {
        return
    }
    if ($ResolvedCommit -notmatch "^[0-9a-fA-F]{40}$") {
        throw "Resolved commit for Git tag '$Tag' is invalid."
    }
    if ($ResolvedCommit -ne $TargetCommit) {
        throw "Git tag '$Tag' resolves to '$ResolvedCommit', not target '$TargetCommit'."
    }
}
