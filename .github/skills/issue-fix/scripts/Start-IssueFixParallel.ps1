<#
.SYNOPSIS
    Run issue-fix in parallel from a single terminal.

.PARAMETER IssueNumbers
    Issue numbers to fix.

.PARAMETER ThrottleLimit
    Maximum parallel tasks.

.PARAMETER CLIType
    AI CLI type (copilot/claude/gh-copilot/vscode/auto).

.PARAMETER Model
    Copilot CLI model to use (e.g., gpt-5.2-codex).

.PARAMETER Force
    Skip confirmation prompts in Start-IssueAutoFix.ps1.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [int[]]$IssueNumbers,

    [int]$ThrottleLimit = 5,

    [ValidateSet('claude', 'copilot', 'gh-copilot', 'vscode', 'auto')]
    [string]$CLIType = 'copilot',

    [string]$Model,

    [switch]$Force
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')

# Resolve config directory name (.github or .claude) from script location
$_cfgDir = if ($PSScriptRoot -match '[\\/](\.github|\.claude)[\\/]') { $Matches[1] } else { '.github' }
$scriptPath = Join-Path $repoRoot "$_cfgDir\skills\issue-fix\scripts\Start-IssueAutoFix.ps1"

$results = $IssueNumbers | ForEach-Object -Parallel {
    $issue = $PSItem
    $repoRoot = $using:repoRoot
    $scriptPath = $using:scriptPath
    $cliType = $using:CLIType
    $model = $using:Model
    $force = $using:Force

    Set-Location $repoRoot

    if (-not $issue) {
        return [pscustomobject]@{
            IssueNumber = $issue
            ExitCode = 1
            Error = 'Issue number is empty.'
        }
    }

    $params = @{
        IssueNumber = [int]$issue
        CLIType = $cliType
    }
    if ($model) {
        $params.Model = $model
    }
    if ($force) {
        $params.Force = $true
    }

    try {
        & $scriptPath @params | Out-Default
        [pscustomobject]@{
            IssueNumber = $issue
            ExitCode = $LASTEXITCODE
        }
    }
    catch {
        [pscustomobject]@{
            IssueNumber = $issue
            ExitCode = 1
            Error = $_.Exception.Message
        }
    }
} -ThrottleLimit $ThrottleLimit

$results
