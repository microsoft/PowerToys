<#!
.SYNOPSIS
    Create (or reuse) a worktree for an existing local or remote (origin) branch.

.DESCRIPTION
    Normalizes origin/<name> to <name>. If the branch does not exist locally (and -NoFetch is not
    provided) it will fetch and create a tracking branch from origin. Reuses any existing worktree
    bound to the branch; otherwise creates a new one adjacent to the repository root.

.PARAMETER Branch
    Branch name (local or origin/<name> form) to materialize as a worktree.

.PARAMETER VSCodeProfile
    VS Code profile to open (Default).

.PARAMETER NoFetch
    Skip fetch if branch missing locally; script will error instead of creating it.

.EXAMPLE
    ./New-WorktreeFromBranch.ps1 -Branch feature/login

.EXAMPLE
    ./New-WorktreeFromBranch.ps1 -Branch origin/bugfix/nullref

.EXAMPLE
    ./New-WorktreeFromBranch.ps1 -Branch release/v1 -NoFetch

.NOTES
    Manual recovery:
        git fetch origin && git checkout <branch>
        git worktree add ../RepoName-XX <branch>
        code ../RepoName-XX --profile Default
#>

param(
    [string] $Branch,
    [Alias('Profile')][string] $VSCodeProfile = 'Default',
    [switch] $NoFetch,
    [switch] $Help
)
. "$PSScriptRoot/WorktreeLib.ps1"

if ($Help -or -not $Branch) { Show-FileEmbeddedHelp -ScriptPath $MyInvocation.MyCommand.Path; return }

# Normalize origin/<name> to <name>
if ($Branch -match '^(origin|upstream|main|master)/.+') {
    if ($Branch -match '^(origin|upstream)/(.+)$') { $Branch = $Matches[2] }
}

try {
    git show-ref --verify --quiet "refs/heads/$Branch"
    if ($LASTEXITCODE -ne 0) {
        if (-not $NoFetch) {
            Warn "Local branch '$Branch' not found; attempting remote fetch..."
            git fetch --all --prune 2>$null | Out-Null
            $remoteRef = "origin/$Branch"
            git show-ref --verify --quiet "refs/remotes/$remoteRef"
            if ($LASTEXITCODE -eq 0) {
                git branch --track $Branch $remoteRef 2>$null | Out-Null
                if ($LASTEXITCODE -ne 0) { throw "Failed to create tracking branch '$Branch' from $remoteRef" }
                Info "Created local tracking branch '$Branch' from $remoteRef."
            } else { throw "Branch '$Branch' not found locally or on origin. Use git fetch or specify a valid branch." }
        } else { throw "Branch '$Branch' does not exist locally (remote fetch disabled with -NoFetch)." }
    }

    New-WorktreeForExistingBranch -Branch $Branch -VSCodeProfile $VSCodeProfile
    $after = Get-WorktreeEntries | Where-Object { $_.Branch -eq $Branch }
    $path = ($after | Select-Object -First 1).Path
    Show-WorktreeExecutionSummary -CurrentBranch $Branch -WorktreePath $path
} catch {
    Err "Error: $($_.Exception.Message)"
    Warn 'Manual steps:'
    Info '  git fetch origin'
    Info "  git checkout $Branch  (or: git branch --track $Branch origin/$Branch)"
    Info '  git worktree add ../<Repo>-XX <branch>'
    Info '  code ../<Repo>-XX'
    exit 1
}
