<#!
Delete-Worktree.ps1
Remove a git worktree (and optionally its local branch and orphan fork remote).

Usage:
    ./Delete-Worktree.ps1 -Pattern <branchOrPathFragment> [-Force] [-KeepBranch] [-KeepRemote]

Pattern matching:
    * If no wildcard, treated as substring match on branch or path
    * Supports *, ? wildcards
    * Lists matches if multiple found

Behavior:
    * Validates not deleting primary repo root
    * If -Force: discards changes (reset/clean) and on failure performs aggressive cleanup
    * Deletes branch unless -KeepBranch
    * If deleted branch tracked a non-origin remote with no other tracking branches, removes that remote unless -KeepRemote

Examples:
    ./Delete-Worktree.ps1 -Pattern feature/login
    ./Delete-Worktree.ps1 -Pattern fork-user-featureX -Force
    ./Delete-Worktree.ps1 -Pattern hotfix -KeepBranch

Manual recovery if script cannot clean up:
    1. List worktrees:              git worktree list --porcelain
    2. Prune stale links:           git worktree prune
    3. If directory still present:  Remove-Item -LiteralPath <path> -Recurse -Force
    4. Detach branch if needed:     git branch -D <branch>
    5. Remove orphan remote:        git remote remove <remote>
    6. Final prune:                 git worktree prune
#>

param(
    [string] $Pattern,
    [switch] $Force,
    [switch] $KeepBranch,
    [switch] $KeepRemote,
    [switch] $Help
)
. "$PSScriptRoot/WorktreeLib.ps1"
if ($Help -or -not $Pattern) { Show-FileEmbeddedHelp -ScriptPath $MyInvocation.MyCommand.Path; return }
try {
    $repoRoot = Get-RepoRoot
    $entries = Get-WorktreeEntries
    if (-not $entries -or $entries.Count -eq 0) { throw 'No worktrees found.' }
    $hasWildcard = $Pattern -match '[\*\?]'
    $matchPattern = if ($hasWildcard) { $Pattern } else { "*${Pattern}*" }
    $found = $entries | Where-Object { $_.Branch -and ( $_.Branch -like $matchPattern -or $_.Path -like $matchPattern ) }
    if (-not $found -or $found.Count -eq 0) { throw "No worktree matches pattern '$Pattern'" }
    if ($found.Count -gt 1) {
        Warn 'Pattern matches multiple worktrees:'
        $found | ForEach-Object { Info ("  {0}  {1}" -f $_.Branch, $_.Path) }
        return
    }
    $target = $found | Select-Object -First 1
    $branch = $target.Branch
    $folder = $target.Path
    if (-not $branch) { throw 'Resolved worktree has no branch (detached); refusing removal.' }
    try { $folder = (Resolve-Path -LiteralPath $folder -ErrorAction Stop).ProviderPath } catch {}
    $primary = (Resolve-Path -LiteralPath $repoRoot).ProviderPath
    if ([IO.Path]::GetFullPath($folder).TrimEnd('\\/') -ieq [IO.Path]::GetFullPath($primary).TrimEnd('\\/')) { throw 'Refusing to remove the primary worktree (repository root).' }
    $status = git -C $folder status --porcelain 2>$null
    if ($LASTEXITCODE -ne 0) { throw "Unable to get git status for $folder" }
    if (-not $Force -and $status) { throw 'Worktree has uncommitted changes. Use -Force to discard.' }
    if ($Force -and $status) {
        Warn '[Force] Discarding local changes'
        git -C $folder reset --hard HEAD | Out-Null
        git -C $folder clean -fdx | Out-Null
    }
    if ($Force) { git worktree remove --force $folder } else { git worktree remove $folder }
    if ($LASTEXITCODE -ne 0) {
        $exit1 = $LASTEXITCODE
        $errMsg = "git worktree remove failed (exit $exit1)"
        if ($Force) {
            Warn 'Primary removal failed; performing aggressive fallback (Force implies brute).'
            try { git -C $folder submodule deinit -f --all 2>$null | Out-Null } catch {}
            try { git -C $folder clean -fdx 2>$null | Out-Null } catch {}
            try { Get-ChildItem -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object { try { $_.IsReadOnly = $false } catch {} } } catch {}
            if (Test-Path $folder) { try { Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction Stop } catch { Err "Manual directory removal failed: $($_.Exception.Message)" } }
            git worktree prune 2>$null | Out-Null
            if (Test-Path $folder) { throw "$errMsg and aggressive cleanup did not fully remove directory: $folder" } else { Info "Aggressive cleanup removed directory $folder." }
        } else {
            throw "$errMsg. Rerun with -Force to attempt aggressive cleanup."
        }
    }
    # Determine upstream before potentially deleting branch
    $upRemote = Get-BranchUpstreamRemote -Branch $branch
    $looksForkName = $branch -like 'fork-*'

    if (-not $KeepBranch) {
        git branch -D $branch 2>$null | Out-Null
        if (-not $KeepRemote -and $upRemote -and $upRemote -ne 'origin') {
            $otherTracking = git for-each-ref --format='%(refname:short)|%(upstream:short)' refs/heads 2>$null |
                Where-Object { $_ -and ($_ -notmatch "^$branch\|") } |
                ForEach-Object { $parts = $_.Split('|',2); if ($parts[1] -match '^(?<r>[^/]+)/'){ $parts[0],$Matches.r } } |
                Where-Object { $_[1] -eq $upRemote }
            if (-not $otherTracking) {
                Warn "Removing orphan remote '$upRemote' (no more tracking branches)"
                git remote remove $upRemote 2>$null | Out-Null
                if ($LASTEXITCODE -ne 0) { Warn "Failed to remove remote '$upRemote' (you may remove manually)." }
            } else { Info "Remote '$upRemote' retained (other branches still track it)." }
        } elseif ($looksForkName -and -not $KeepRemote -and -not $upRemote) {
            Warn 'Branch looks like a fork branch (name pattern), but has no upstream remote; nothing to clean.'
        }
    }

    Info "Removed worktree ($branch) at $folder."; if (-not $KeepBranch) { Info 'Branch deleted.' }
    Show-WorktreeExecutionSummary -CurrentBranch $branch
} catch {
    Err "Error: $($_.Exception.Message)"
    Warn 'Manual cleanup guidelines:'
    Info '  git worktree list --porcelain'
    Info '  git worktree prune'
    Info '  # If still present:'
    Info '  Remove-Item -LiteralPath <path> -Recurse -Force'
    Info '  git branch -D <branch>   (if you also want to drop local branch)'
    Info '  git remote remove <remote>  (if orphan fork remote remains)'
    Info '  git worktree prune'
    exit 1
}
