<#
.SYNOPSIS
    Display status of all pr-rework sessions from Generated Files.

.DESCRIPTION
    Scans Generated Files/prRework/ directories and reads .state.json and
    .signal files to show a table of PR rework progress.

.PARAMETER PRNumber
    Optional: show status for a specific PR only.

.PARAMETER Detailed
    Show full phase history for each PR.

.EXAMPLE
    ./Get-PRReworkStatus.ps1

.EXAMPLE
    ./Get-PRReworkStatus.ps1 -PRNumber 45365 -Detailed
#>
[CmdletBinding()]
param(
    [int]$PRNumber,

    [switch]$Detailed
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptDir 'IssueReviewLib.ps1')
$repoRoot = Get-RepoRoot

$prReworkRoot = Join-Path $repoRoot 'Generated Files' 'prRework'

if (-not (Test-Path $prReworkRoot)) {
    Write-Host "No pr-rework data found at: $prReworkRoot" -ForegroundColor Yellow
    return
}

$dirs = Get-ChildItem -Path $prReworkRoot -Directory
if ($PRNumber) {
    $dirs = $dirs | Where-Object { $_.Name -eq "$PRNumber" }
}

if ($dirs.Count -eq 0) {
    Write-Host "No pr-rework sessions found$(if ($PRNumber) { " for PR #$PRNumber" })." -ForegroundColor Yellow
    return
}

$results = foreach ($dir in $dirs) {
    $stateFile = Join-Path $dir.FullName '.state.json'
    $signalFile = Join-Path $dir.FullName '.signal'

    $prNum = $dir.Name

    # Read state
    $state = $null
    if (Test-Path $stateFile) {
        try { $state = Get-Content $stateFile -Raw | ConvertFrom-Json } catch {}
    }

    # Read signal
    $signal = $null
    if (Test-Path $signalFile) {
        try { $signal = Get-Content $signalFile -Raw | ConvertFrom-Json } catch {}
    }

    # Determine status
    $status = 'unknown'
    if ($signal) {
        $status = $signal.status
    } elseif ($state) {
        $status = "iter-$($state.currentIteration)/$($state.maxIterations) ($($state.currentPhase))"
    }

    # Count iterations with data
    $iterDirs = Get-ChildItem -Path $dir.FullName -Directory -Filter 'iteration-*' -ErrorAction SilentlyContinue
    $iterCount = $iterDirs.Count

    # Get latest findings count
    $latestFindings = 0
    if ($iterDirs.Count -gt 0) {
        $latestIterDir = $iterDirs | Sort-Object Name | Select-Object -Last 1
        $findingsFile = Join-Path $latestIterDir.FullName 'findings.json'
        if (Test-Path $findingsFile) {
            try {
                $findings = Get-Content $findingsFile -Raw | ConvertFrom-Json
                $latestFindings = $findings.Count
            } catch {}
        }
    }

    # Worktree path
    $worktreePath = ''
    if ($state -and $state.worktreePath) { $worktreePath = $state.worktreePath }

    # Build result
    [PSCustomObject]@{
        PR             = $prNum
        Status         = $status
        Iterations     = $iterCount
        Phase          = if ($state) { $state.currentPhase } else { '-' }
        Findings       = $latestFindings
        Branch         = if ($state) { $state.branch } else { '-' }
        WorktreePath   = $worktreePath
        LastUpdated    = if ($state) { $state.lastUpdatedAt } else { '-' }
    }
}

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host " PR REWORK STATUS" -ForegroundColor Cyan
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

$results | Format-Table @(
    @{Label = 'PR'; Expression = { $_.PR }; Width = 8}
    @{Label = 'Status'; Expression = { $_.Status }; Width = 25}
    @{Label = 'Iter'; Expression = { $_.Iterations }; Width = 5}
    @{Label = 'Phase'; Expression = { $_.Phase }; Width = 10}
    @{Label = 'Findings'; Expression = { $_.Findings }; Width = 9}
    @{Label = 'Branch'; Expression = { $_.Branch }; Width = 30}
    @{Label = 'Last Updated'; Expression = { $_.LastUpdated }; Width = 25}
) -AutoSize

# Summary
$total = $results.Count
$done = ($results | Where-Object { $_.Status -eq 'success' }).Count
$maxed = ($results | Where-Object { $_.Status -eq 'max-iterations' }).Count
$failed = ($results | Where-Object { $_.Status -eq 'failure' }).Count
$running = $total - $done - $maxed - $failed

Write-Host ""
Write-Host "Total: $total | Clean: $done | Max-Iter: $maxed | Failed: $failed | Running/Pending: $running" -ForegroundColor $(
    if ($failed -gt 0) { 'Yellow' } elseif ($maxed -gt 0) { 'DarkYellow' } else { 'Green' }
)

# ── Detailed view ──────────────────────────────────────────────────────────
if ($Detailed) {
    foreach ($r in $results) {
        $stateFile = Join-Path $prReworkRoot $r.PR '.state.json'
        if (-not (Test-Path $stateFile)) { continue }

        $state = Get-Content $stateFile -Raw | ConvertFrom-Json

        Write-Host ""
        Write-Host ("─" * 60) -ForegroundColor DarkCyan
        Write-Host " PR #$($r.PR) — Phase History" -ForegroundColor DarkCyan
        Write-Host ("─" * 60) -ForegroundColor DarkCyan
        Write-Host ""

        if ($state.phaseHistory -and $state.phaseHistory.Count -gt 0) {
            $state.phaseHistory | Format-Table @(
                @{Label = 'Iter'; Expression = { $_.iteration }; Width = 5}
                @{Label = 'Phase'; Expression = { $_.phase }; Width = 10}
                @{Label = 'Status'; Expression = { $_.status }; Width = 12}
                @{Label = 'Timestamp'; Expression = { $_.timestamp }; Width = 25}
            ) -AutoSize
        } else {
            Write-Host "  No phase history recorded." -ForegroundColor DarkGray
        }

        # Show worktree path for easy access
        if ($r.WorktreePath) {
            Write-Host "  Worktree: $($r.WorktreePath)" -ForegroundColor DarkGray
        }

        # Show latest findings
        $latestIterDir = Get-ChildItem -Path (Join-Path $prReworkRoot $r.PR) -Directory -Filter 'iteration-*' -ErrorAction SilentlyContinue |
            Sort-Object Name | Select-Object -Last 1
        if ($latestIterDir) {
            $findingsFile = Join-Path $latestIterDir.FullName 'findings.json'
            if (Test-Path $findingsFile) {
                $findings = Get-Content $findingsFile -Raw | ConvertFrom-Json
                if ($findings.Count -gt 0) {
                    Write-Host ""
                    Write-Host "  Latest Findings ($($findings.Count)):" -ForegroundColor DarkYellow
                    foreach ($f in $findings) {
                        $sevColor = switch ($f.severity) {
                            'high'   { 'Red' }
                            'medium' { 'Yellow' }
                            'low'    { 'DarkGray' }
                            default  { 'White' }
                        }
                        Write-Host "    [$($f.id)] $($f.severity.ToUpper().PadRight(7)) $($f.file):$($f.line) — $($f.title)" -ForegroundColor $sevColor
                    }
                }
            }
        }
    }
}

Write-Host ""
return $results
