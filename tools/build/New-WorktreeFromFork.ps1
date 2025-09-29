<#!
.SYNOPSIS
  Create (or reuse) a worktree from a branch in a personal fork: <ForkUser>:<ForkBranch>.

.DESCRIPTION
  Adds a transient uniquely named fork remote (fork-xxxxx) unless -RemoteName specified.
  Fetches only the target branch (fallback full fetch once if needed), creates a local tracking
  branch (fork-<user>-<sanitized-branch> or custom alias), and delegates worktree creation/reuse
  to shared helpers in WorktreeLib.

.PARAMETER Spec
  Fork spec in the form <ForkUser>:<ForkBranch>.

.PARAMETER ForkRepo
  Repository name in the fork (default: PowerToys).

.PARAMETER RemoteName
  Desired remote name; if left as 'fork' a unique suffix will be generated.

.PARAMETER BranchAlias
  Optional local branch name override; defaults to fork-<user>-<sanitized-branch>.

.PARAMETER VSCodeProfile
  VS Code profile to pass through to worktree opening (Default profile by default).

.EXAMPLE
  ./New-WorktreeFromFork.ps1 -Spec alice:feature/new-ui

.EXAMPLE
  ./New-WorktreeFromFork.ps1 -Spec bob:bugfix/crash -BranchAlias fork-bob-crash

.NOTES
  Manual equivalent if this script fails:
    git remote add fork-temp https://github.com/<user>/<repo>.git
    git fetch fork-temp
    git branch --track fork-<user>-<branch> fork-temp/<branch>
    git worktree add ../Repo-XX fork-<user>-<branch>
    code ../Repo-XX
#>
param(
  [string] $Spec,
  [string] $ForkRepo = 'PowerToys',
  [string] $RemoteName = 'fork',
  [string] $BranchAlias,
  [Alias('Profile')][string] $VSCodeProfile = 'Default',
  [switch] $Help
)

. "$PSScriptRoot/WorktreeLib.ps1"
if ($Help -or -not $Spec) { Show-FileEmbeddedHelp -ScriptPath $MyInvocation.MyCommand.Path; return }

$repoRoot = git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) { throw 'Not inside a git repository.' }

# Parse spec
if ($Spec -notmatch '^[^:]+:.+$') { throw "Spec must be <ForkUser>:<ForkBranch>, got '$Spec'" }
$ForkUser,$ForkBranch = $Spec.Split(':',2)

$forkUrl = "https://github.com/$ForkUser/$ForkRepo.git"

# Auto-suffix remote name if user left default 'fork'
$allRemotes = @(git remote 2>$null)
if ($RemoteName -eq 'fork') {
  $chars = 'abcdefghijklmnopqrstuvwxyz0123456789'
  do {
    $suffix = -join ((1..5) | ForEach-Object { $chars[(Get-Random -Max $chars.Length)] })
    $candidate = "fork-$suffix"
  } while ($allRemotes -contains $candidate)
  $RemoteName = $candidate
  Info "Assigned unique remote name: $RemoteName"
}

$existing = $allRemotes | Where-Object { $_ -eq $RemoteName }
if (-not $existing) {
  Info "Adding remote $RemoteName -> $forkUrl"
  git remote add $RemoteName $forkUrl | Out-Null
} else {
  $currentUrl = git remote get-url $RemoteName 2>$null
  if ($currentUrl -ne $forkUrl) { Warn "Remote $RemoteName points to $currentUrl (expected $forkUrl). Using existing." }
}

## Note: Verbose fetch & stale lock auto-clean removed for simplicity.

try {
  Info "Fetching branch '$ForkBranch' from $RemoteName..."
  & git fetch $RemoteName $ForkBranch 1>$null 2>$null
  $fetchExit = $LASTEXITCODE
  if ($fetchExit -ne 0) {
    # Retry full fetch silently once (covers servers not supporting branch-only fetch syntax)
    & git fetch $RemoteName 1>$null 2>$null
    $fetchExit = $LASTEXITCODE
  }
  if ($fetchExit -ne 0) { throw "Fetch failed for remote $RemoteName (branch $ForkBranch)." }

  $remoteRef = "refs/remotes/$RemoteName/$ForkBranch"
  git show-ref --verify --quiet $remoteRef
  if ($LASTEXITCODE -ne 0) { throw "Remote branch not found: $RemoteName/$ForkBranch" }

  $sanitizedBranch = ($ForkBranch -replace '[\\/:*?"<>|]','-')
  if ($BranchAlias) { $localBranch = $BranchAlias } else { $localBranch = "fork-$ForkUser-$sanitizedBranch" }

  git show-ref --verify --quiet "refs/heads/$localBranch"
  if ($LASTEXITCODE -ne 0) {
    Info "Creating local tracking branch $localBranch from $RemoteName/$ForkBranch"
    git branch --track $localBranch "$RemoteName/$ForkBranch" 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create local tracking branch $localBranch" }
  } else { Info "Local branch $localBranch already exists." }

  New-WorktreeForExistingBranch -Branch $localBranch -VSCodeProfile $VSCodeProfile
  # Ensure upstream so future 'git push' works
  Set-BranchUpstream -LocalBranch $localBranch -RemoteName $RemoteName -RemoteBranchPath $ForkBranch
  $after = Get-WorktreeEntries | Where-Object { $_.Branch -eq $localBranch }
  $path = ($after | Select-Object -First 1).Path
  Show-WorktreeExecutionSummary -CurrentBranch $localBranch -WorktreePath $path
  Warn "Remote $RemoteName ready (URL: $forkUrl)"
  $hasUp = git rev-parse --abbrev-ref --symbolic-full-name "$localBranch@{upstream}" 2>$null
  if ($hasUp) { Info "Push with: git push (upstream: $hasUp)" } else { Warn 'Upstream not set; run: git push -u <remote> <local>:<remoteBranch>' }
} catch {
  Err "Error: $($_.Exception.Message)"
  Warn 'Manual steps:'
  Info "  git remote add temp-fork $forkUrl"
  Info "  git fetch temp-fork"
  Info "  git branch --track fork-<user>-<branch> temp-fork/$ForkBranch"
  Info '  git worktree add ../<Repo>-XX fork-<user>-<branch>'
  Info '  code ../<Repo>-XX'
  exit 1
}
