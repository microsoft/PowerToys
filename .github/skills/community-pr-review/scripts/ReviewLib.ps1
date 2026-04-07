# ReviewLib.ps1 - Shared helpers for community PR review workflow

#region Console Output Helpers
function Info { param([string]$Message) Write-Host $Message -ForegroundColor Cyan }
function Warn { param([string]$Message) Write-Host $Message -ForegroundColor Yellow }
function Err  { param([string]$Message) Write-Host $Message -ForegroundColor Red }
function Success { param([string]$Message) Write-Host $Message -ForegroundColor Green }
#endregion

#region Repository Helpers
function Get-RepoRoot {
    $root = git rev-parse --show-toplevel 2>$null
    if (-not $root) { throw 'Not inside a git repository.' }
    return (Resolve-Path $root).Path
}

function Get-SkillsRoot {
    $repoRoot = Get-RepoRoot
    if (Test-Path (Join-Path $repoRoot '.github/skills')) { return '.github/skills' }
    if (Test-Path (Join-Path $repoRoot '.claude/skills')) { return '.claude/skills' }
    throw 'No skills directory found (.github/skills or .claude/skills).'
}
#endregion

#region Signal File Helpers
function Write-Signal {
    param(
        [string]$OutputDir,
        [hashtable]$Data
    )
    $signalPath = Join-Path $OutputDir '.signal'
    $Data['timestamp'] = (Get-Date).ToString('o')
    $Data | ConvertTo-Json -Depth 5 | Set-Content $signalPath -Force
    Info "Signal written: $signalPath"
}
#endregion

#region Build Helpers
function Get-BuildErrorLog {
    param([string]$BuildDir)
    $errorLogs = Get-ChildItem -Path $BuildDir -Filter 'build.*.errors.log' -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending
    if ($errorLogs) {
        return $errorLogs[0].FullName
    }
    return $null
}
#endregion
