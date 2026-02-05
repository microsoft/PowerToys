<#
.SYNOPSIS
    Run pr-fix in parallel from a single terminal.

.PARAMETER PRNumbers
    PR numbers to fix.

.PARAMETER ThrottleLimit
    Maximum parallel tasks.

.PARAMETER CLIType
    AI CLI type (copilot/claude).

.PARAMETER Model
    Copilot CLI model to use (e.g., gpt-5.2-codex).

.PARAMETER Force
    Skip confirmation prompts in Start-PRFix.ps1.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [int[]]$PRNumbers,

    [int]$ThrottleLimit = 3,

    [ValidateSet('claude', 'copilot')]
    [string]$CLIType = 'copilot',

    [string]$Model,

    [switch]$Force
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')
$scriptPath = Join-Path $repoRoot '.github\skills\pr-fix\scripts\Start-PRFix.ps1'

$results = $PRNumbers | ForEach-Object -Parallel {
    param($pr)

    $repoRoot = $using:repoRoot
    $scriptPath = $using:scriptPath
    $cliType = $using:CLIType
    $model = $using:Model
    $force = $using:Force

    Set-Location $repoRoot

    $branch = (gh pr view $pr --json headRefName -q .headRefName)
    $worktree = (git worktree list | Select-String $branch | ForEach-Object { ($_ -split '\s+')[0] })

    if (-not $worktree) {
        return [pscustomobject]@{
            PRNumber = $pr
            ExitCode = 1
            Error = "Worktree not found for branch $branch"
        }
    }

    Set-Location $worktree

    $args = @('-PRNumber', $pr, '-CLIType', $cliType)
    if ($model) {
        $args += @('-Model', $model)
    }
    if ($force) {
        $args += '-Force'
    }

    try {
        & $scriptPath @args | Out-Default
        [pscustomobject]@{
            PRNumber = $pr
            ExitCode = $LASTEXITCODE
        }
    }
    catch {
        [pscustomobject]@{
            PRNumber = $pr
            ExitCode = 1
            Error = $_.Exception.Message
        }
    }
} -ThrottleLimit $ThrottleLimit

$results