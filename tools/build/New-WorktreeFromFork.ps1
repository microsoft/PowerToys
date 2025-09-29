<#!
New-WorktreeFromFork.ps1
Create (or reuse) a worktree from a fork branch specified as:
  <ForkUser>:<ForkBranch>

Usage:
  ./New-WorktreeFromFork.ps1 -Spec user:feature/awesome [-ForkRepo PowerToys] [-RemoteName custom] [-BranchAlias localName] [-Profile VS]

Behavior:
  * Adds a unique remote (fork-xxxxx) if -RemoteName left as default 'fork'
  * Fetches only that remote
  * Creates local tracking branch if missing (fork-<user>-<sanitized-branch> or -BranchAlias)
  * Reuses existing worktree via common library if present
  * Places worktree alongside repo root (hash-based folder naming)

Examples:
  ./New-WorktreeFromFork.ps1 -Spec alice:feature/new-ui
  ./New-WorktreeFromFork.ps1 -Spec bob:bugfix/crash -ForkRepo PowerToys -BranchAlias fork-bob-crash

Manual recovery if this script fails:
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

try {
  Info "Fetching fork remote $RemoteName..."
  git fetch $RemoteName --prune 2>$null | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "Fetch failed for remote $RemoteName" }

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
  # Ensure upstream so future 'git push' works without refspec
  Set-BranchUpstream -LocalBranch $localBranch -RemoteName $RemoteName -RemoteBranchPath $ForkBranch
  $after = Get-WorktreeEntries | Where-Object { $_.Branch -eq $localBranch }
  $path = ($after | Select-Object -First 1).Path
  Show-WorktreeExecutionSummary -CurrentBranch $localBranch -WorktreePath $path
  Warn "Remote $RemoteName in place (URL: $forkUrl)"
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
