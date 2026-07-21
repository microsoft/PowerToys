<#
.SYNOPSIS
    Resolve the current teammate's PowerToys fork configuration.
.DESCRIPTION
    Detects the fork owner (from the authenticated gh account), the fork repo,
    the git remote that points at the fork, and a local clone path. Nothing is
    tied to a specific account -- 'gh api user' resolves whoever is logged in.
.EXAMPLE
    ./Get-ForkConfig.ps1
    Prints the resolved configuration and returns it as an object.
#>
[CmdletBinding()]
param(
    [string] $ClonePath
)

$ErrorActionPreference = 'Stop'

$forkOwner = (gh api user --jq '.login').Trim()
if (-not $forkOwner) { throw "Could not resolve the GitHub login. Run 'gh auth login' first." }
$forkRepo = "$forkOwner/PowerToys"

# Fork remote name: a remote whose URL points at the fork owner's PowerToys (not microsoft).
$forkRemote = git remote -v 2>$null |
    Select-String "github.com[:/](.+)/PowerToys" |
    Where-Object { $_ -notmatch 'microsoft' } |
    ForEach-Object { ($_ -split '\s+')[0] } |
    Select-Object -First 1
if (-not $forkRemote) { $forkRemote = 'fork' }

if (-not $ClonePath) {
    $ClonePath = @(
        'C:\PowerToys',
        "$env:USERPROFILE\source\repos\PowerToys",
        "$env:USERPROFILE\git\PowerToys"
    ) | Where-Object { Test-Path "$_\.git" } | Select-Object -First 1
}

$config = [pscustomobject]@{
    ForkOwner  = $forkOwner
    ForkRepo   = $forkRepo
    ForkRemote = $forkRemote
    ClonePath  = $ClonePath
}
Write-Host "Fork owner: $forkOwner | repo: $forkRepo | remote: $forkRemote | clone: $ClonePath"
return $config
