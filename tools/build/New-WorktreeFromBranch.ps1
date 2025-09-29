<#!
New-WorktreeFromBranch.ps1
Create (or reuse) a worktree for an existing or remotely tracked branch.

Usage:
    ./New-WorktreeFromBranch.ps1 -Branch <name> [-Profile VSCodeProfile] [-NoFetch]

Behavior:
    * Normalizes origin/<name> to <name>
    * If branch missing locally and -NoFetch not specified, fetches and creates tracking branch from origin
    * Reuses an existing worktree if already present for the branch
    * Places new worktree alongside the repo root with hashed suffix

Examples:
    ./New-WorktreeFromBranch.ps1 -Branch feature/login
    ./New-WorktreeFromBranch.ps1 -Branch origin/bugfix/nullref
    ./New-WorktreeFromBranch.ps1 -Branch release/v1 -NoFetch

Manual recovery (if this script fails):
    1. Ensure the branch exists locally:  git fetch origin && git checkout <branch>
    2. Create worktree manually:         git worktree add ../RepoName-XX <branch>
    3. Open in VS Code:                  code ../RepoName-XX --profile Default
    4. If duplicate worktree error:      git worktree list (find existing path and open it)
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
