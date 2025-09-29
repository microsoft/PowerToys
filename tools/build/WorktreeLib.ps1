# WorktreeLib.ps1 - shared helpers

function Info { param([string]$Message) Write-Host $Message -ForegroundColor Cyan }
function Warn { param([string]$Message) Write-Host $Message -ForegroundColor Yellow }
function Err  { param([string]$Message) Write-Host $Message -ForegroundColor Red }

function Get-RepoRoot {
  $root = git rev-parse --show-toplevel 2>$null
  if (-not $root) { throw 'Not inside a git repository.' }
  return $root
}

function Get-WorktreeBasePath {
  param([string]$RepoRoot)
  # Always use parent of repo root (folder that contains the main repo directory)
  $parent = Split-Path -Parent $RepoRoot
  if (-not (Test-Path $parent)) { throw "Parent path for repo root not found: $parent" }
  return (Resolve-Path $parent).ProviderPath
}

function Get-ShortHashFromString {
  param([Parameter(Mandatory)][string]$Text)
  $md5 = [System.Security.Cryptography.MD5]::Create()
  try {
    $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
    $digest = $md5.ComputeHash($bytes)
    return -join ($digest[0..1] | ForEach-Object { $_.ToString('x2') })
  } finally { $md5.Dispose() }
}

function Initialize-SubmodulesIfAny {
  param([string]$RepoRoot,[string]$WorktreePath)
  $hasGitmodules = Test-Path (Join-Path $RepoRoot '.gitmodules')
  if ($hasGitmodules) {
    git -C $WorktreePath submodule sync --recursive | Out-Null
    git -C $WorktreePath submodule update --init --recursive | Out-Null
    return $true
  }
  return $false
}

function New-WorktreeForExistingBranch {
  param(
    [Parameter(Mandatory)][string] $Branch,
    [Parameter(Mandatory)][string] $VSCodeProfile
  )
  $repoRoot = Get-RepoRoot
  git show-ref --verify --quiet "refs/heads/$Branch"; if ($LASTEXITCODE -ne 0) { throw "Branch '$Branch' does not exist locally." }

  # Detect existing worktree for this branch
  $entries = Get-WorktreeEntries
  $match = $entries | Where-Object { $_.Branch -eq $Branch } | Select-Object -First 1
  if ($match) {
    Info "Reusing existing worktree for '$Branch': $($match.Path)"
    code --new-window "$($match.Path)" --profile "$VSCodeProfile" | Out-Null
    return
  }

  $safeBranch = ($Branch -replace '[\\/:*?"<>|]','-')
  $hash = Get-ShortHashFromString -Text $safeBranch
  $folderName = "$(Split-Path -Leaf $repoRoot)-$hash"
  $base = Get-WorktreeBasePath -RepoRoot $repoRoot
  $folder = Join-Path $base $folderName
  git worktree add $folder $Branch
  $inited = Initialize-SubmodulesIfAny -RepoRoot $repoRoot -WorktreePath $folder
  code --new-window "$folder" --profile "$VSCodeProfile" | Out-Null
  Info "Created worktree for branch '$Branch' at $folder."; if ($inited) { Info 'Submodules initialized.' }
}

function Get-WorktreeEntries {
  # Returns objects with Path and Branch (branch without refs/heads/ prefix)
  $lines = git worktree list --porcelain 2>$null
  if (-not $lines) { return @() }
  $entries = @(); $current=@{}
  foreach($l in $lines){
    if ($l -eq '') { if ($current.path -and $current.branch){ $entries += ,([pscustomobject]@{ Path=$current.path; Branch=($current.branch -replace '^refs/heads/','') }) }; $current=@{}; continue }
    if ($l -like 'worktree *'){ $current.path = ($l -split ' ',2)[1] }
    elseif ($l -like 'branch *'){ $current.branch = ($l -split ' ',2)[1].Trim() }
  }
  if ($current.path -and $current.branch){ $entries += ,([pscustomobject]@{ Path=$current.path; Branch=($current.branch -replace '^refs/heads/','') }) }
  return ($entries | Sort-Object Path,Branch -Unique)
}

function Get-BranchUpstreamRemote {
  param([Parameter(Mandatory)][string]$Branch)
  # Returns remote name if branch has an upstream, else $null
  $ref = git rev-parse --abbrev-ref --symbolic-full-name "$Branch@{upstream}" 2>$null
  if ($LASTEXITCODE -ne 0 -or -not $ref) { return $null }
  if ($ref -match '^(?<remote>[^/]+)/.+$') { return $Matches.remote }
  return $null
}

function Show-IssueFarmCommonFooter {
  Info '--- Common Manual Steps ---'
  Info 'List worktree:      git worktree list --porcelain'
  Info 'List branches:       git branch -vv'
  Info 'List remotes:        git remote -v'
  Info 'Prune worktree:     git worktree prune'
  Info 'Remove worktree dir: Remove-Item -Recurse -Force <path>'
  Info 'Reset branch:        git reset --hard HEAD'
}

function Show-WorktreeExecutionSummary {
  param(
    [string]$CurrentBranch,
    [string]$WorktreePath
  )
  Info '--- Summary ---'
  if ($CurrentBranch) { Info "Branch:        $CurrentBranch" }
  if ($WorktreePath) { Info "Worktree path:  $WorktreePath" }
  $entries = Get-WorktreeEntries
  if ($entries.Count -gt 0) {
    Info 'Existing worktrees:'
    $entries | ForEach-Object { Info ("  {0} -> {1}" -f $_.Branch,$_.Path) }
  }
  Info 'Remotes:'
  git remote -v 2>$null | Sort-Object | Get-Unique | ForEach-Object { Info "  $_" }
}

function Show-FileEmbeddedHelp {
  param([string]$ScriptPath)
  if (-not (Test-Path $ScriptPath)) { throw "Cannot load help; file missing: $ScriptPath" }
  $content = Get-Content -LiteralPath $ScriptPath -ErrorAction Stop
  $inBlock=$false
  foreach($line in $content){
    if ($line -match '^<#!') { $inBlock=$true; continue }
    if ($line -match '#>$') { break }
    if ($inBlock) { Write-Host $line }
  }
  Show-IssueFarmCommonFooter
}

function Set-BranchUpstream {
  param(
    [Parameter(Mandatory)][string]$LocalBranch,
    [Parameter(Mandatory)][string]$RemoteName,
    [Parameter(Mandatory)][string]$RemoteBranchPath
  )
  $current = git rev-parse --abbrev-ref --symbolic-full-name "$LocalBranch@{upstream}" 2>$null
  if (-not $current) {
  Info "Setting upstream: $LocalBranch -> $RemoteName/$RemoteBranchPath"
    git branch --set-upstream-to "$RemoteName/$RemoteBranchPath" $LocalBranch 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) { Warn "Failed to set upstream automatically. Run: git branch --set-upstream-to $RemoteName/$RemoteBranchPath $LocalBranch" }
    return
  }
  if ($current -ne "$RemoteName/$RemoteBranchPath") {
  Warn "Upstream mismatch ($current != $RemoteName/$RemoteBranchPath); updating..."
    git branch --set-upstream-to "$RemoteName/$RemoteBranchPath" $LocalBranch 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) { Warn "Could not update upstream; manual fix: git branch --set-upstream-to $RemoteName/$RemoteBranchPath $LocalBranch" } else { Info 'Upstream corrected.' }
  } else { Info "Upstream already: $current" }
}
