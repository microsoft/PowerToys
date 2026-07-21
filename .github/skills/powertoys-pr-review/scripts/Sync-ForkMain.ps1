<#
.SYNOPSIS
    Fast-forward the local clone's main from upstream and push it to the fork.
.DESCRIPTION
    Assumes 'origin' points at microsoft/PowerToys (upstream) and a separate
    remote points at the personal fork. Fetches upstream main, fast-forwards the
    local main (never a merge commit), and pushes main to the fork so mirror
    branches can be rebased on current main.
.PARAMETER ClonePath
    Path to the local PowerToys clone. Defaults to the current directory.
.PARAMETER ForkRemote
    Name of the git remote that points at the fork. Default 'fork'.
.PARAMETER UpstreamRemote
    Name of the git remote that points at microsoft/PowerToys. Default 'origin'.
.EXAMPLE
    ./Sync-ForkMain.ps1 -ClonePath C:\PowerToys -ForkRemote fork
#>
[CmdletBinding()]
param(
    [string] $ClonePath = (Get-Location).Path,
    [string] $ForkRemote = 'fork',
    [string] $UpstreamRemote = 'origin'
)

$ErrorActionPreference = 'Stop'
Push-Location $ClonePath
try {
    git fetch $UpstreamRemote main
    git checkout main
    git merge "$UpstreamRemote/main" --ff-only
    git push $ForkRemote main
    Write-Host "Synced main from $UpstreamRemote and pushed to $ForkRemote."
}
finally {
    Pop-Location
}
