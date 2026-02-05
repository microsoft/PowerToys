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
$scriptPath = Join-Path $repoRoot '.github\skills\issue-fix\scripts\Start-IssueAutoFix.ps1'

$results = $IssueNumbers | ForEach-Object -Parallel {
    param($issue)

    $repoRoot = $using:repoRoot
    $scriptPath = $using:scriptPath
    $cliType = $using:CLIType
    $model = $using:Model
    $force = $using:Force

    Set-Location $repoRoot

    $args = @('-IssueNumber', $issue, '-CLIType', $cliType)
    if ($model) {
        $args += @('-Model', $model)
    }
    if ($force) {
        $args += '-Force'
    }

    try {
        & $scriptPath @args | Out-Default
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