<#
.SYNOPSIS
    Show commit/uncommitted status for issue/* worktrees.
#>
[CmdletBinding()]
param()

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')
Set-Location $repoRoot

git worktree list | Select-String "issue/" | ForEach-Object {
    $path = ($_ -split "\s+")[0]
    $branch = ($_ -split "\s+")[2] -replace "\[|\]",""
    $ahead = (git -C $path rev-list main..HEAD --count 2>$null)
    $uncommitted = (git -C $path status --porcelain 2>$null | Measure-Object).Count
    [pscustomobject]@{
        Branch = $branch
        CommitsAhead = $ahead
        Uncommitted = $uncommitted
        Path = $path
    }
}