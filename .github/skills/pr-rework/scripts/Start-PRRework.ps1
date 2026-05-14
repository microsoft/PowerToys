<#
.SYNOPSIS
    Iteratively rework a PR to production quality using local review/fix/build/test loops.

.DESCRIPTION
    For a single PR:
    1. Creates or reuses a git worktree for the PR branch
    2. Runs pr-review locally (no GitHub posting) to find issues
    3. Parses medium+ severity findings into a structured list
    4. If no findings → done (PR is clean)
    5. Runs Copilot/Claude CLI to fix the findings in the worktree
    6. Builds changed projects and runs related unit tests
    7. Loops back to step 2 until clean or max iterations reached
    8. Writes a human-readable summary of all changes

    All changes stay LOCAL — no commits, no pushes, no GitHub posting.
    The human reviews the summary and decides whether to push.

    Fully resumable: reads .state.json on restart and picks up from the last phase.

.PARAMETER PRNumber
    PR number to rework.

.PARAMETER CLIType
    AI CLI to use: copilot or claude. Default: copilot.

.PARAMETER Model
    Copilot CLI model override. Default: claude-opus-4.6.

.PARAMETER MaxIterations
    Maximum review/fix loop iterations. Default: 5.

.PARAMETER MinSeverity
    Minimum severity to fix: high, medium, low. Default: medium.

.PARAMETER ReviewTimeoutMin
    Timeout in minutes for the review CLI call. Default: 10.

.PARAMETER FixTimeoutMin
    Timeout in minutes for the fix CLI call. Default: 15.

.PARAMETER Force
    Skip confirmation prompts.

.PARAMETER Fresh
    Discard previous state and start over (keeps worktree).

.PARAMETER SkipTests
    Skip the unit test phase after each fix.

.EXAMPLE
    ./Start-PRRework.ps1 -PRNumber 45365 -CLIType copilot -Model claude-sonnet-4 -Force

.EXAMPLE
    # Resume after crash
    ./Start-PRRework.ps1 -PRNumber 45365 -CLIType copilot -Force

.EXAMPLE
    # Fresh start, skip tests
    ./Start-PRRework.ps1 -PRNumber 45365 -CLIType copilot -Fresh -SkipTests -Force
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [int]$PRNumber,

    [ValidateSet('copilot', 'claude')]
    [string]$CLIType = 'copilot',

    [string]$Model = 'claude-opus-4.6',

    [int]$MaxIterations = 5,

    [ValidateSet('high', 'medium', 'low')]
    [string]$MinSeverity = 'medium',

    [int]$ReviewTimeoutMin = 10,

    [int]$FixTimeoutMin = 15,

    [switch]$Force,

    [switch]$Fresh,

    [switch]$SkipTests
)

$ErrorActionPreference = 'Continue'

# ── Load libraries ──────────────────────────────────────────────────────────
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $scriptDir 'IssueReviewLib.ps1')

$repoRoot = Get-RepoRoot

# Resolve config directory name (.github or .claude) from script location
$_cfgDir = if ($PSScriptRoot -match '[\\/](\.github|\.claude)[\\/]') { $Matches[1] } else { '.github' }
$newWorktreeScript = Join-Path $repoRoot 'tools/build/New-WorktreeFromBranch.ps1'

$genRoot = Join-Path $repoRoot 'Generated Files' 'prRework' $PRNumber
$stateFile = Join-Path $genRoot '.state.json'
$signalFile = Join-Path $genRoot '.signal'
$worktreeInfoFile = Join-Path $genRoot 'worktree-info.json'
$summaryFile = Join-Path $genRoot 'summary.md'

# Severity ranking for filtering
$severityRank = @{ 'high' = 3; 'medium' = 2; 'low' = 1; 'info' = 0 }
$minRank = $severityRank[$MinSeverity]

# ── Helper: State Management ───────────────────────────────────────────────

function Read-State {
    if ((Test-Path $stateFile) -and -not $Fresh) {
        return Get-Content $stateFile -Raw | ConvertFrom-Json
    }
    return $null
}

function Save-State {
    param($State)
    $State.lastUpdatedAt = (Get-Date).ToString('o')
    $State | ConvertTo-Json -Depth 10 | Set-Content $stateFile -Force
}

function New-State {
    param([string]$Branch, [string]$WorktreePath)
    return [PSCustomObject]@{
        prNumber        = $PRNumber
        branch          = $Branch
        worktreePath    = $WorktreePath
        currentIteration = 1
        currentPhase    = 'review'
        maxIterations   = $MaxIterations
        phaseHistory    = @()
        startedAt       = (Get-Date).ToString('o')
        lastUpdatedAt   = (Get-Date).ToString('o')
    }
}

function Add-PhaseRecord {
    param($State, [string]$Phase, [string]$Status, [hashtable]$Extra = @{})
    $record = @{
        iteration = $State.currentIteration
        phase     = $Phase
        status    = $Status
        timestamp = (Get-Date).ToString('o')
    }
    foreach ($k in $Extra.Keys) { $record[$k] = $Extra[$k] }
    # Convert phaseHistory to mutable list if needed
    $history = [System.Collections.ArrayList]@($State.phaseHistory)
    $history.Add([PSCustomObject]$record) | Out-Null
    $State.phaseHistory = $history
    Save-State $State
}

function Get-LastPhaseOfType {
    param($State, [string]$Phase, [int]$Iteration)
    $State.phaseHistory | Where-Object {
        $_.iteration -eq $Iteration -and $_.phase -eq $Phase
    } | Select-Object -Last 1
}

# ── Helper: Worktree Management ────────────────────────────────────────────

function Get-OrCreateWorktree {
    param([string]$Branch)

    # Check if the main repo IS on that branch already
    Push-Location $repoRoot
    try {
        $currentBranch = git branch --show-current 2>$null
        if ($currentBranch -eq $Branch) {
            Info "Main repo is on branch '$Branch' — using repo root as worktree"
            return $repoRoot
        }
    } finally { Pop-Location }

    # Delegate to New-WorktreeFromBranch.ps1 — handles fetch, reuse, submodules
    # But first check if worktree already exists to avoid calling the script
    # (which opens VS Code windows via code --new-window).
    . (Join-Path $repoRoot 'tools/build/WorktreeLib.ps1')
    $existingEntry = Get-WorktreeEntries | Where-Object { $_.Branch -eq $Branch } | Select-Object -First 1
    if ($existingEntry) {
        Info "Reusing existing worktree for '$Branch': $($existingEntry.Path)"
    } else {
        Info "Creating worktree for branch '$Branch' via New-WorktreeFromBranch.ps1..."
        $null = & $newWorktreeScript -Branch $Branch 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "New-WorktreeFromBranch.ps1 failed for branch '$Branch' (exit $LASTEXITCODE)"
        }
    }

    # Read back the worktree path from git worktree list
    # (dot-source already done above, skip redundant load)
    $entry = Get-WorktreeEntries | Where-Object { $_.Branch -eq $Branch } | Select-Object -First 1
    if (-not $entry) {
        throw "Worktree for branch '$Branch' not found after creation"
    }
    $worktreePath = $entry.Path

    # Copy config dirs to worktree (agents, skills, instructions, prompts, top-level md)
    # These aren't on the PR branch so the CLI can't find them without this.
    $sourceCfg = Join-Path $repoRoot $_cfgDir
    $destCfg   = Join-Path $worktreePath $_cfgDir
    if (Test-Path $sourceCfg) {
        if (-not (Test-Path $destCfg)) {
            New-Item -ItemType Directory -Path $destCfg -Force | Out-Null
        }
        foreach ($sub in @('agents', 'skills', 'instructions', 'prompts')) {
            $src = Join-Path $sourceCfg $sub
            $dst = Join-Path $destCfg $sub
            if ((Test-Path $src) -and -not (Test-Path $dst)) {
                Copy-Item -Path $src -Destination $dst -Recurse -Force
                Info "Copied $_cfgDir/$sub to worktree"
            }
        }
        # Top-level instruction file (copilot-instructions.md / CLAUDE.md)
        foreach ($mdFile in @('copilot-instructions.md', 'CLAUDE.md')) {
            $src = Join-Path $sourceCfg $mdFile
            $dst = Join-Path $destCfg $mdFile
            if ((Test-Path $src) -and -not (Test-Path $dst)) {
                Copy-Item -Path $src -Destination $dst -Force
                Info "Copied $_cfgDir/$mdFile to worktree"
            }
        }
    }

    Info "Worktree ready at: $worktreePath"
    return $worktreePath
}

# ── Helper: Run CLI with Timeout ───────────────────────────────────────────

function Invoke-CLIWithTimeout {
    param(
        [string]$Prompt,
        [string]$WorkDir,
        [int]$TimeoutMinutes,
        [string]$LogFile,
        [string]$AgentName
    )

    $mcpConfigPath = Join-Path $repoRoot "$_cfgDir/skills/pr-rework/references/mcp-config.json"
    $mcpConfig = "@$mcpConfigPath"

    # Find the copilot.ps1 bootstrapper that lives next to copilot.bat.
    # We call copilot.ps1 directly to avoid the .bat wrapper which triggers
    # "Terminate batch job (Y/N)?" prompts in interactive terminals.
    function Resolve-CopilotPS1 {
        $candidates = Get-Command copilot -CommandType Application -ErrorAction SilentlyContinue -All
        foreach ($c in $candidates) {
            $dir = Split-Path $c.Source -Parent
            $ps1 = Join-Path $dir 'copilot.ps1'
            if (Test-Path $ps1) { return $ps1 }
        }
        return $null
    }

    # Ensure log directory exists.
    $logDir = Split-Path $LogFile -Parent
    if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }

    # Launch the CLI as a child pwsh process with Start-Process for full I/O.
    # Start-Job was used previously but it breaks the CLI's interactive tool
    # protocol — the CLI's file-edit and shell tool calls silently fail because
    # Start-Job captures all output streams (*> redirect) in a separate runspace
    # that doesn't share the real filesystem working directory. Start-Process
    # gives the CLI a real process with proper working directory and I/O.

    switch ($CLIType) {
        'copilot' {
            $copilotPS1 = Resolve-CopilotPS1
            if (-not $copilotPS1) { throw 'Cannot find copilot.ps1 bootstrapper. Is GitHub Copilot CLI installed?' }
            Info "  Using copilot CLI: $copilotPS1"

            # Write a thin wrapper script that the child process will execute.
            # This avoids argument-escaping hell with Start-Process -ArgumentList.
            $wrapperScript = Join-Path $logDir '_cli-wrapper.ps1'
            $modelArg = if ($Model) { "--model '$Model'" } else { '' }
            $agentArg = if ($AgentName) { "--agent '$AgentName'" } else { '' }
            @"
`$ErrorActionPreference = 'Continue'
Set-Location -LiteralPath '$($WorkDir -replace "'","''")'
& '$($copilotPS1 -replace "'","''")' --additional-mcp-config '$($mcpConfig -replace "'","''")' $agentArg -p @'
$Prompt
'@ --allow-all -s $modelArg *> '$($LogFile -replace "'","''")'
exit `$LASTEXITCODE
"@ | Set-Content $wrapperScript -Force
            $psi = [System.Diagnostics.ProcessStartInfo]::new()
            $psi.FileName = 'pwsh'
            $psi.Arguments = "-nop -nol -noe -File `"$wrapperScript`""
            $psi.WorkingDirectory = $WorkDir
            $psi.UseShellExecute = $false
            $psi.CreateNoWindow = $true
            $proc = [System.Diagnostics.Process]::new()
            $proc.StartInfo = $psi
            [void]$proc.Start()
        }
        'claude' {
            $wrapperScript = Join-Path $logDir '_cli-wrapper.ps1'
            $agentArg = if ($AgentName) { "--agent '$AgentName'" } else { '' }
            @"
`$ErrorActionPreference = 'Continue'
Set-Location -LiteralPath '$($WorkDir -replace "'","''")'
& claude --print --dangerously-skip-permissions $agentArg --prompt @'
$Prompt
'@ *> '$($LogFile -replace "'","''")'
exit `$LASTEXITCODE
"@ | Set-Content $wrapperScript -Force
            $psi = [System.Diagnostics.ProcessStartInfo]::new()
            $psi.FileName = 'pwsh'
            $psi.Arguments = "-nop -nol -noe -File `"$wrapperScript`""
            $psi.WorkingDirectory = $WorkDir
            $psi.UseShellExecute = $false
            $psi.CreateNoWindow = $true
            $proc = [System.Diagnostics.Process]::new()
            $proc.StartInfo = $psi
            [void]$proc.Start()
        }
    }

    # Wait with timeout + early-exit detection.
    # The CLI often finishes its work (writes all files) but hangs indefinitely.
    # We poll the log file: if it stops growing for 60s after reaching 500+ bytes,
    # the CLI is done but hung — kill it and return success.
    $timeoutMs = $TimeoutMinutes * 60 * 1000
    $pollMs = 15000          # check every 15 seconds
    $staleLimit = 60000      # 60s of no growth = done
    $minLogBytes = 500       # minimum log size before early-exit kicks in
    $elapsed = 0
    $lastLogSize = 0
    $staleSince = $null
    $earlyExit = $false

    while ($elapsed -lt $timeoutMs) {
        $exited = $proc.WaitForExit($pollMs)
        if ($exited) { break }
        $elapsed += $pollMs

        # Check log file growth
        if (Test-Path $LogFile) {
            $currentSize = (Get-Item $LogFile).Length
            if ($currentSize -ne $lastLogSize) {
                $lastLogSize = $currentSize
                $staleSince = $null  # reset stale timer
            } elseif ($currentSize -ge $minLogBytes) {
                if (-not $staleSince) { $staleSince = $elapsed }
                elseif (($elapsed - $staleSince) -ge $staleLimit) {
                    Info "  CLI log stable for 60s at $([math]::Round($currentSize/1024,1))KB — early exit (work complete)"
                    $earlyExit = $true
                    try { $proc.Kill($true) } catch { }
                    break
                }
            }
        }
    }

    if ($earlyExit) {
        $proc.Dispose()
        return @{ Success = $true; TimedOut = $false; ExitCode = 0 }
    }

    if (-not $exited) {
        try { $proc.Kill($true) } catch { }
        Warn "CLI timed out after $TimeoutMinutes minutes"
        $proc.Dispose()
        return @{ Success = $false; TimedOut = $true; ExitCode = -1 }
    }

    $exitCode = $proc.ExitCode
    if ($null -eq $exitCode) { $exitCode = 0 }
    $proc.Dispose()

    return @{ Success = ($exitCode -eq 0); TimedOut = $false; ExitCode = $exitCode }
}

# ── Helper: Isolated Build Execution ───────────────────────────────────────

function Invoke-BuildIsolated {
    <#
    .SYNOPSIS
        Runs a build command in an isolated process with a SEPARATE console
        window (hidden). This prevents MSBuild's console signal handlers from
        killing the parent pwsh process.

        Uses Start-Process -WindowStyle Hidden to create a completely separate
        console session. The child inherits the parent's env vars (including
        VSINSTALLDIR, PATH with MSBuild, etc.) so build tools work correctly.

        IMPORTANT: The parent process MUST have VS developer environment variables
        already set (e.g., launched from a VS Developer Command Prompt or terminal
        with Enter-VsDevShell already run). The child build process checks
        $env:VSINSTALLDIR and skips Enter-VsDevShell, avoiding the crash.
    #>
    param(
        [string]$Command,
        [string[]]$Arguments,
        [string]$WorkDir,
        [string]$LogFile,
        [int]$TimeoutSeconds = 600
    )

    $combinedLog = if ($LogFile) { $LogFile } else { [System.IO.Path]::GetTempFileName() }

    # If Command is already cmd.exe, extract the inner command from Arguments.
    if ($Command -match '(?i)^cmd(\.exe)?$' -and $Arguments.Count -ge 2 -and $Arguments[0] -match '(?i)^/[ck]$') {
        $innerCmd = ($Arguments | Select-Object -Skip 1) -join ' '
    } else {
        $innerCmd = if ($Arguments) { "`"$Command`" $($Arguments -join ' ')" } else { "`"$Command`"" }
    }

    # Create wrapper .cmd that does redirection and captures exit code
    $exitCodeFile = [System.IO.Path]::GetTempFileName()
    $wrapperFile = [System.IO.Path]::GetTempFileName() + '.cmd'
    @"
@echo off
cd /d "$WorkDir"
call $innerCmd > "$combinedLog" 2>&1
echo %ERRORLEVEL% > "$exitCodeFile"
"@ | Set-Content $wrapperFile -Force -Encoding ASCII

    try {
        # Start-Process -WindowStyle Hidden creates a separate console session,
        # isolating MSBuild's CTRL+C handlers from the parent pwsh process.
        $timeoutMs = $TimeoutSeconds * 1000
        $buildProc = Start-Process -FilePath 'cmd.exe' `
            -ArgumentList "/c `"$wrapperFile`"" `
            -WindowStyle Hidden -PassThru
        $exited = $buildProc.WaitForExit($timeoutMs)
        if (-not $exited) {
            try { $buildProc.Kill() } catch {}
            Warn "Build timed out after ${TimeoutSeconds}s"
        }

        $exitCode = if (Test-Path $exitCodeFile) {
            $raw = (Get-Content $exitCodeFile -Raw).Trim()
            if ($raw -match '^\d+$') { [int]$raw } else { -1 }
        } else { -1 }

        $stdoutText = if (Test-Path $combinedLog) { Get-Content $combinedLog -Raw -ErrorAction SilentlyContinue } else { '' }

        return @{ ExitCode = $exitCode; Stdout = $stdoutText; Stderr = '' }
    }
    finally {
        Remove-Item $wrapperFile -ErrorAction SilentlyContinue
        Remove-Item $exitCodeFile -ErrorAction SilentlyContinue
        if (-not $LogFile) { Remove-Item $combinedLog -ErrorAction SilentlyContinue }
    }
}

# ── Phase: Review ──────────────────────────────────────────────────────────

function Invoke-ReviewPhase {
    param($State, [int]$Iteration)

    $iterDir = Join-Path $genRoot "iteration-$Iteration"
    $reviewDir = Join-Path $iterDir 'review'
    if (-not (Test-Path $reviewDir)) { New-Item -ItemType Directory -Path $reviewDir -Force | Out-Null }

    # Build the previous-findings reference for iteration 2+
    $prevFindingsArg = ''
    if ($Iteration -gt 1) {
        $prevFindingsFile = Join-Path $genRoot "iteration-$($Iteration - 1)" 'findings.json'
        if (Test-Path $prevFindingsFile) {
            $prevFindingsArg = "`nPrevious iteration findings are at: $prevFindingsFile"
        }
    }

    $prompt = @"
You are reviewing PR #$PRNumber locally in a worktree. Do NOT post any comments to GitHub.

Follow the LOCAL review methodology from $_cfgDir/skills/pr-rework/references/rework-local-review.prompt.md

Inputs:
- pr_number: $PRNumber
- output_dir: $reviewDir
- iteration: $Iteration
$prevFindingsArg

CRITICAL — DATA SOURCE (use two-dot diff to include uncommitted fix changes):
- Get changed files via: git diff origin/main --name-only
- Get file diffs via: git diff origin/main -- <file>
- Read file content via: cat <file> or Get-Content <file>
- Do NOT use gh pr view, gh api, Get-GitHubRawFile.ps1, or any GitHub API
- Do NOT use three-dot diff (main...HEAD) — it misses uncommitted changes
- The worktree contains the LATEST local state including uncommitted fix changes
- IMPORTANT: Always use 'origin/main' (not 'main') to avoid stale local refs

Write all step files to: $reviewDir/
For each finding, use mcp-review-comment blocks with severity, file, line, and description.
"@

    Info "  Running local review (timeout: ${ReviewTimeoutMin}m)..."
    $State.currentPhase = 'review'
    Add-PhaseRecord $State 'review' 'in-progress'

    $logFile = Join-Path $iterDir 'review-cli.log'
    # Remove stale log from previous runs to avoid false-positive fallback
    if (Test-Path $logFile) { Remove-Item $logFile -Force -ErrorAction SilentlyContinue }
    $result = Invoke-CLIWithTimeout -Prompt $prompt -WorkDir $State.worktreePath `
        -TimeoutMinutes $ReviewTimeoutMin -LogFile $logFile -AgentName 'ReviewPR'

    if ($result.TimedOut) {
        Add-PhaseRecord $State 'review' 'timeout'
        Warn "  Review timed out — will retry on next run"
        return $false
    }

    # Check if review files were created
    $overviewFile = Join-Path $reviewDir '00-OVERVIEW.md'
    $stepFiles = Get-ChildItem -Path $reviewDir -Filter '*.md' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^\d{2}-' }

    if ($stepFiles.Count -ge 3 -or (Test-Path $overviewFile)) {
        Add-PhaseRecord $State 'review' 'done' @{ stepFiles = $stepFiles.Count }
        Info "  Review complete ($($stepFiles.Count) step files)"
        return $true
    }

    # Review may have run via non-redirect path; check if log file has content
    # Only trust the log if the CLI exited successfully (exit code 0)
    if ($result.ExitCode -eq 0 -and (Test-Path $logFile) -and (Get-Item $logFile).Length -gt 100) {
        Add-PhaseRecord $State 'review' 'done' @{ stepFiles = 0; note = 'output-in-log-only' }
        Info "  Review ran but files may be in alternate location. Checking..."
        # Also check the standard prReview output path (CLI may have ignored our custom path)
        $altPath = Join-Path $repoRoot "Generated Files/prReview/$PRNumber"
        if (Test-Path $altPath) {
            $altSteps = Get-ChildItem -Path $altPath -Filter '*.md' -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -match '^\d{2}-' }
            if ($altSteps.Count -gt 0) {
                Info "  Found $($altSteps.Count) step files in standard prReview path, copying..."
                Copy-Item -Path "$altPath\*" -Destination $reviewDir -Recurse -Force
                return $true
            }
        }
        # CLI exited 0 but produced no review files anywhere — treat as failure
        Add-PhaseRecord $State 'review' 'failed' @{ exitCode = 0; note = 'no-output-files' }
        Warn "  Review CLI exited 0 but produced no step files"
        return $false
    }

    Add-PhaseRecord $State 'review' 'failed' @{ exitCode = $result.ExitCode }
    Warn "  Review produced no output files"
    return $false
}

# ── Phase: Parse Findings ──────────────────────────────────────────────────

function Invoke-ParsePhase {
    param($State, [int]$Iteration)

    $iterDir = Join-Path $genRoot "iteration-$Iteration"
    $reviewDir = Join-Path $iterDir 'review'
    $findingsFile = Join-Path $iterDir 'findings.json'

    $findings = @()
    $stepFiles = Get-ChildItem -Path $reviewDir -Filter '*.md' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^\d{2}-' }

    foreach ($file in $stepFiles) {
        $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
        if (-not $content) { continue }

        $stepName = $file.BaseName

        # Parse mcp-review-comment blocks (machine-readable findings)
        $commentPattern = '(?s)```mcp-review-comment\s*\n(.+?)```'
        $commentMatches = [regex]::Matches($content, $commentPattern)
        foreach ($m in $commentMatches) {
            try {
                $parsed = $m.Groups[1].Value | ConvertFrom-Json
                $sev = if ($parsed.severity) { $parsed.severity.ToLower() } else { 'info' }
                if (($severityRank.ContainsKey($sev)) -and ($severityRank[$sev] -ge $minRank)) {
                    # Accept both pr-review format (start_line/end_line) and local format (line/endLine)
                    $parsedLine    = if ($parsed.line)    { $parsed.line }    elseif ($parsed.start_line) { $parsed.start_line } else { 0 }
                    $parsedEndLine = if ($parsed.endLine) { $parsed.endLine } elseif ($parsed.end_line)   { $parsed.end_line }   else { 0 }
                    $findings += [PSCustomObject]@{
                        id          = "F-{0:D3}" -f ($findings.Count + 1)
                        step        = $stepName
                        severity    = $sev
                        file        = $parsed.file
                        line        = $parsedLine
                        endLine     = $parsedEndLine
                        title       = if ($parsed.title) { $parsed.title } else { '' }
                        description = $parsed.body
                        suggestedFix = if ($parsed.suggestedFix) { $parsed.suggestedFix } else { '' }
                    }
                }
            } catch {
                # Not valid JSON, skip
            }
        }

        # Also parse severity markers in plain text (e.g., "**Severity: high**")
        $textPattern = '(?mi)^\*?\*?severity:\s*(high|medium|low)\*?\*?\s*$'
        $sevMatches = [regex]::Matches($content, $textPattern)
        # If we found mcp blocks, skip text parsing to avoid duplicates
        if ($commentMatches.Count -eq 0) {
            # Heuristic: parse heading-based findings
            $sectionPattern = '(?ms)^###?\s+(.+?)$\s*(?:.*?severity:\s*(high|medium|low).*?)(?=^###?\s|\z)'
            $secMatches = [regex]::Matches($content, $sectionPattern)
            foreach ($sm in $secMatches) {
                $sev = $sm.Groups[2].Value.ToLower()
                if (($severityRank.ContainsKey($sev)) -and ($severityRank[$sev] -ge $minRank)) {
                    $findings += [PSCustomObject]@{
                        id          = "F-$($findings.Count + 1)".PadLeft(5, '0').Replace('F-0', 'F-')
                        step        = $stepName
                        severity    = $sev
                        file        = ''
                        line        = 0
                        endLine     = 0
                        title       = $sm.Groups[1].Value.Trim()
                        description = $sm.Value.Substring(0, [Math]::Min(500, $sm.Value.Length))
                        suggestedFix = ''
                    }
                }
            }
        }
    }

    # Deduplicate by file+line+title
    $unique = @{}
    foreach ($f in $findings) {
        $key = "$($f.file):$($f.line):$($f.title)"
        if (-not $unique.ContainsKey($key)) { $unique[$key] = $f }
    }
    $findings = @($unique.Values)

    # Assign sequential IDs
    for ($i = 0; $i -lt $findings.Count; $i++) {
        $findings[$i].id = "F-{0:D3}" -f ($i + 1)
    }

    $findings | ConvertTo-Json -Depth 5 | Set-Content $findingsFile -Force

    Add-PhaseRecord $State 'parse' 'done' @{
        totalFindings = $findings.Count
        high = ($findings | Where-Object severity -eq 'high').Count
        medium = ($findings | Where-Object severity -eq 'medium').Count
        low = ($findings | Where-Object severity -eq 'low').Count
    }

    Info "  Parsed $($findings.Count) actionable findings (high: $(($findings | Where-Object severity -eq 'high').Count), medium: $(($findings | Where-Object severity -eq 'medium').Count))"
    return $findings
}

# ── Phase: Fix ─────────────────────────────────────────────────────────────

function Invoke-FixPhase {
    param($State, [int]$Iteration, [array]$Findings)

    $iterDir = Join-Path $genRoot "iteration-$Iteration"
    $findingsFile = Join-Path $iterDir 'findings.json'
    $fixLogFile = Join-Path $iterDir 'fix.log'

    # Build the fix prompt with inline findings summary for the CLI
    $findingsSummary = ($Findings | ForEach-Object {
        "- [$($_.id)] $($_.severity.ToUpper()) in $($_.file):$($_.line) — $($_.title): $($_.description)"
    }) -join "`n"

    # Feed previous iteration's build/test failures to help the AI fix them
    $buildErrArg = ''
    $testErrArg = ''
    if ($Iteration -gt 1) {
        $prevIterDir = Join-Path $genRoot "iteration-$($Iteration - 1)"
        $prevBuildLog = Join-Path $prevIterDir 'build.log'
        $prevTestLog = Join-Path $prevIterDir 'test.log'
        if (Test-Path $prevBuildLog) { $buildErrArg = "Build errors from previous iteration: $prevBuildLog" }
        if (Test-Path $prevTestLog) { $testErrArg = "Test failures from previous iteration: $prevTestLog" }
    }

    $prompt = @"
You are fixing review findings for PR #$PRNumber. All changes stay LOCAL — do NOT commit, push, or post to GitHub.

Read the detailed prompt at $_cfgDir/skills/pr-rework/references/rework-fix.prompt.md for full instructions.

The findings file is at: $findingsFile

Here is a summary of the $($Findings.Count) findings to fix:

$findingsSummary

$buildErrArg
$testErrArg

After fixing, build the changed projects using tools/build/build.cmd scoped to the changed directories.
If there are related unit test projects (*UnitTests*), build and run them too.

CRITICAL:
- Do NOT commit or push any changes
- Do NOT post comments to GitHub or resolve threads
- DO fix all findings listed above
- DO build and verify the fix compiles
- DO run related unit tests if found
"@

    Info "  Running fix pass (timeout: ${FixTimeoutMin}m, findings: $($Findings.Count))..."
    $State.currentPhase = 'fix'
    Add-PhaseRecord $State 'fix' 'in-progress'

    # Remove stale log from previous runs
    if (Test-Path $fixLogFile) { Remove-Item $fixLogFile -Force -ErrorAction SilentlyContinue }
    $result = Invoke-CLIWithTimeout -Prompt $prompt -WorkDir $State.worktreePath `
        -TimeoutMinutes $FixTimeoutMin -LogFile $fixLogFile -AgentName 'FixPR'

    if ($result.TimedOut) {
        Add-PhaseRecord $State 'fix' 'timeout'
        Warn "  Fix timed out after $FixTimeoutMin minutes"
        return $false
    }

    # Check if any files were modified (both staged and unstaged)
    Push-Location $State.worktreePath
    try {
        $unstaged = git diff --name-only 2>$null
        $staged = git diff --staged --name-only 2>$null
        $modified = @($unstaged) + @($staged) | Where-Object { $_ } | Sort-Object -Unique
        $modCount = ($modified | Measure-Object).Count
    } finally { Pop-Location }

    Add-PhaseRecord $State 'fix' 'done' @{ filesModified = $modCount; exitCode = $result.ExitCode }
    Info "  Fix pass complete ($modCount files modified)"
    return $true
}

# ── Phase: Build ───────────────────────────────────────────────────────────

function Invoke-BuildPhase {
    param($State, [int]$Iteration)

    $iterDir = Join-Path $genRoot "iteration-$Iteration"
    $buildLog = Join-Path $iterDir 'build.log'

    Info "  Building changed projects..."
    $State.currentPhase = 'build'
    Add-PhaseRecord $State 'build' 'in-progress'

    Push-Location $State.worktreePath
    try {
        # Find changed files (two-dot diff includes uncommitted working tree changes)
        $changedFiles = git diff --name-only origin/main 2>$null
        $changedDirs = $changedFiles | ForEach-Object { Split-Path $_ -Parent } | Sort-Object -Unique |
            Where-Object { $_ -match '^src/' }

        if ($changedDirs.Count -eq 0) {
            Add-PhaseRecord $State 'build' 'skipped' @{ reason = 'no-src-changes' }
            Info "  No src/ changes to build"
            return $true
        }

        # Collect ALL changed project directories (not just the first)
        $buildDirs = @()
        foreach ($dir in $changedDirs) {
            $fullDir = Join-Path $State.worktreePath $dir
            if (Test-Path $fullDir) {
                $projFiles = Get-ChildItem -Path $fullDir -Filter '*.csproj' -ErrorAction SilentlyContinue
                if (-not $projFiles) {
                    $projFiles = Get-ChildItem -Path $fullDir -Filter '*.vcxproj' -ErrorAction SilentlyContinue
                }
                foreach ($pf in $projFiles) {
                    if ($pf.DirectoryName -notin $buildDirs) {
                        $buildDirs += $pf.DirectoryName
                    }
                }
            }
        }

        if ($buildDirs.Count -eq 0) {
            # Fall back to the first changed src/ directory
            $buildDirs = @(Join-Path $State.worktreePath $changedDirs[0])
        }

        $buildScript = Join-Path $State.worktreePath 'tools/build/build.cmd'
        if (-not (Test-Path $buildScript)) {
            $buildScript = Join-Path $repoRoot 'tools/build/build.cmd'
        }

        $allBuildOk = $true
        $buildOutput = @()
        foreach ($buildDir in $buildDirs) {
            Info "  Building: $(Split-Path $buildDir -Leaf)"
            $bResult = Invoke-BuildIsolated -Command 'cmd.exe' `
                -Arguments @('/c', "`"$buildScript`" -Path `"$buildDir`"") `
                -WorkDir $State.worktreePath `
                -LogFile $null `
                -TimeoutSeconds 600
            $buildOutput += $bResult.Stdout
            if ($bResult.ExitCode -ne 0) { $allBuildOk = $false }
        }
        $buildOutput | Out-File $buildLog -Force

        if ($allBuildOk) {
            Add-PhaseRecord $State 'build' 'done' @{ exitCode = 0; projects = $buildDirs.Count }
            Info "  Build succeeded ($($buildDirs.Count) project(s))"
            return $true
        } else {
            Add-PhaseRecord $State 'build' 'failed' @{ exitCode = 1; projects = $buildDirs.Count }
            Warn "  Build failed. Errors logged to: $buildLog"
            return $false
        }
    } finally { Pop-Location }
}

# ── Phase: Test ────────────────────────────────────────────────────────────

function Invoke-TestPhase {
    param($State, [int]$Iteration)

    if ($SkipTests) {
        Info "  Tests skipped (-SkipTests)"
        Add-PhaseRecord $State 'test' 'skipped' @{ reason = 'user-skipped' }
        return $true
    }

    $iterDir = Join-Path $genRoot "iteration-$Iteration"
    $testLog = Join-Path $iterDir 'test.log'

    Info "  Discovering unit tests..."
    $State.currentPhase = 'test'
    Add-PhaseRecord $State 'test' 'in-progress'

    Push-Location $State.worktreePath
    try {
        # Find changed modules from git diff (two-dot includes uncommitted changes)
        $changedFiles = git diff --name-only origin/main 2>$null
        $modules = $changedFiles | ForEach-Object {
            if ($_ -match 'src/modules/(\w+)/') { $Matches[1] }
            elseif ($_ -match 'src/settings-ui/') { 'Settings' }
            elseif ($_ -match 'src/common/') { 'Common' }
            elseif ($_ -match 'src/runner/') { 'Runner' }
        } | Sort-Object -Unique | Where-Object { $_ }

        if ($modules.Count -eq 0) {
            Add-PhaseRecord $State 'test' 'skipped' @{ reason = 'no-module-changes' }
            Info "  No module changes detected — skipping tests"
            return $true
        }

        # Search for test projects using targeted directory scans
        $testProjects = @()
        foreach ($mod in $modules) {
            # Build specific search directories for this module rather than
            # scanning the entire worktree recursively (which is very slow).
            $searchDirs = @(
                (Join-Path $State.worktreePath "src/modules/$mod"),
                (Join-Path $State.worktreePath "src/settings-ui"),
                (Join-Path $State.worktreePath "src/common")
            ) | Where-Object { Test-Path $_ }

            foreach ($searchDir in $searchDirs) {
                $found = Get-ChildItem -Path $searchDir -Filter '*.csproj' -Recurse -Depth 3 -ErrorAction SilentlyContinue |
                    Where-Object { $_.BaseName -match 'Test' } | Select-Object -First 1
                if ($found) {
                    $testProjects += $found.FullName
                    break
                }
            }
        }

        if ($testProjects.Count -eq 0) {
            Add-PhaseRecord $State 'test' 'skipped' @{ reason = 'no-test-projects' }
            Info "  No test projects found for modules: $($modules -join ', ')"
            return $true
        }

        Info "  Found $($testProjects.Count) test project(s)"

        # Build and run tests
        $allPassed = $true
        $totalPassed = 0
        $totalFailed = 0

        foreach ($testProj in $testProjects) {
            $testDir = Split-Path $testProj -Parent
            Info "  Running tests: $(Split-Path $testProj -Leaf)"

            # Build the test project first
            $buildScript = Join-Path $State.worktreePath 'tools/build/build.cmd'
            if (-not (Test-Path $buildScript)) { $buildScript = Join-Path $repoRoot 'tools/build/build.cmd' }
            Invoke-BuildIsolated -Command 'cmd.exe' `
                -Arguments @('/c', "`"$buildScript`" -Path `"$testDir`"") `
                -WorkDir $State.worktreePath `
                -LogFile $null `
                -TimeoutSeconds 600 | Out-Null

            # Try to find the test DLL in the project's bin directory
            $testDlls = Get-ChildItem -Path $testDir -Filter '*Test*.dll' -Recurse -Depth 5 -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -match '\\bin\\' -and $_.FullName -notmatch '\\ref\\' }

            if ($testDlls.Count -eq 0) {
                Info "    No test DLLs found after build"
                continue
            }

            foreach ($dll in $testDlls) {
                $testOutput = dotnet vstest $dll.FullName 2>&1
                $testOutput | Out-File $testLog -Append -Force

                $passMatch = $testOutput | Select-String 'Passed:\s*(\d+)'
                $failMatch = $testOutput | Select-String 'Failed:\s*(\d+)'

                if ($passMatch) { $totalPassed += [int]$passMatch.Matches[0].Groups[1].Value }
                if ($failMatch) {
                    $failCount = [int]$failMatch.Matches[0].Groups[1].Value
                    $totalFailed += $failCount
                    if ($failCount -gt 0) { $allPassed = $false }
                }
            }
        }

        if ($allPassed) {
            Add-PhaseRecord $State 'test' 'done' @{ passed = $totalPassed; failed = 0 }
            Info "  Tests passed ($totalPassed passed, 0 failed)"
            return $true
        } else {
            Add-PhaseRecord $State 'test' 'failed' @{ passed = $totalPassed; failed = $totalFailed }
            Warn "  Tests failed ($totalPassed passed, $totalFailed failed). Log: $testLog"
            return $false
        }
    } finally { Pop-Location }
}

# ── Write Summary ──────────────────────────────────────────────────────────

function Write-ReworkSummary {
    param($State)

    $sb = [System.Text.StringBuilder]::new()
    $sb.AppendLine("# PR Rework Summary — PR #$PRNumber") | Out-Null
    $sb.AppendLine("") | Out-Null
    $sb.AppendLine("**Branch**: $($State.branch)") | Out-Null
    $sb.AppendLine("**Worktree**: $($State.worktreePath)") | Out-Null
    $sb.AppendLine("**Iterations**: $($State.currentIteration)") | Out-Null
    $sb.AppendLine("**Started**: $($State.startedAt)") | Out-Null
    $sb.AppendLine("**Completed**: $(Get-Date -Format 'o')") | Out-Null
    $sb.AppendLine("") | Out-Null

    # Changed files summary
    Push-Location $State.worktreePath
    try {
        $changedFiles = git diff --name-only origin/main 2>$null
        $uncommitted = git diff --name-only 2>$null
    } finally { Pop-Location }

    $sb.AppendLine("## Changed Files") | Out-Null
    $sb.AppendLine("") | Out-Null
    if ($uncommitted) {
        $sb.AppendLine("### Uncommitted Changes (from rework)") | Out-Null
        foreach ($f in $uncommitted) { $sb.AppendLine("- ``$f``") | Out-Null }
        $sb.AppendLine("") | Out-Null
    }
    if ($changedFiles) {
        $sb.AppendLine("### All Changes vs main") | Out-Null
        $maxFiles = 30
        $shown = @($changedFiles) | Select-Object -First $maxFiles
        foreach ($f in $shown) { $sb.AppendLine("- ``$f``") | Out-Null }
        $totalChanged = @($changedFiles).Count
        if ($totalChanged -gt $maxFiles) {
            $sb.AppendLine("_...and $($totalChanged - $maxFiles) more files_") | Out-Null
        }
        $sb.AppendLine("") | Out-Null
    }

    # Iteration history
    $sb.AppendLine("## Iteration History") | Out-Null
    $sb.AppendLine("") | Out-Null
    for ($i = 1; $i -le $State.currentIteration; $i++) {
        $sb.AppendLine("### Iteration $i") | Out-Null
        $iterPhases = $State.phaseHistory | Where-Object { $_.iteration -eq $i }
        $sb.AppendLine("") | Out-Null
        $sb.AppendLine("| Phase | Status | Details |") | Out-Null
        $sb.AppendLine("|-------|--------|---------|") | Out-Null
        foreach ($p in $iterPhases) {
            $details = ($p.PSObject.Properties | Where-Object {
                $_.Name -notin @('iteration', 'phase', 'status', 'timestamp')
            } | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ', '
            $sb.AppendLine("| $($p.phase) | $($p.status) | $details |") | Out-Null
        }
        $sb.AppendLine("") | Out-Null

        # Show findings count for this iteration
        $findingsFile = Join-Path $genRoot "iteration-$i" 'findings.json'
        if (Test-Path $findingsFile) {
            $findings = Get-Content $findingsFile -Raw | ConvertFrom-Json
            $sb.AppendLine("Findings: $($findings.Count) actionable") | Out-Null
            $sb.AppendLine("") | Out-Null
        }
    }

    $sb.AppendLine("## Next Steps") | Out-Null
    $sb.AppendLine("") | Out-Null
    $sb.AppendLine("Review the changes in the worktree and decide:") | Out-Null
    $sb.AppendLine("") | Out-Null
    $sb.AppendLine("``````powershell") | Out-Null
    $sb.AppendLine("# Review the diff") | Out-Null
    $sb.AppendLine("cd `"$($State.worktreePath)`"") | Out-Null
    $sb.AppendLine("git diff") | Out-Null
    $sb.AppendLine("") | Out-Null
    $sb.AppendLine("# If satisfied, stage and push:") | Out-Null
    $sb.AppendLine("git add -A") | Out-Null
    $sb.AppendLine("git commit -m `"fix: address review findings for PR #$PRNumber`"") | Out-Null
    $sb.AppendLine("git push") | Out-Null
    $sb.AppendLine("``````") | Out-Null

    $sb.ToString() | Set-Content $summaryFile -Force
    Info "Summary written to: $summaryFile"
}

# ════════════════════════════════════════════════════════════════════════════
# MAIN
# ════════════════════════════════════════════════════════════════════════════

try {
    Write-Host ""
    Write-Host ("=" * 70) -ForegroundColor Cyan
    Write-Host " PR REWORK — PR #$PRNumber" -ForegroundColor Cyan
    Write-Host ("=" * 70) -ForegroundColor Cyan
    Write-Host ""

    # Ensure output directory exists
    if (-not (Test-Path $genRoot)) {
        New-Item -ItemType Directory -Path $genRoot -Force | Out-Null
    }

    # ── Get PR info ────────────────────────────────────────────────────────
    $prInfo = gh pr view $PRNumber --json state,headRefName,url,title 2>$null | ConvertFrom-Json
    if (-not $prInfo) { throw "PR #$PRNumber not found" }
    if ($prInfo.state -ne 'OPEN') {
        Warn "PR #$PRNumber is $($prInfo.state), not OPEN"
        return [PSCustomObject]@{
            PRNumber      = $PRNumber
            Status        = 'Skipped'
            Iterations    = 0
            FinalFindings = -1
            WorktreePath  = ''
            SummaryPath   = ''
            Error         = "PR is $($prInfo.state), not OPEN"
        }
    }

    Info "PR:     #$PRNumber — $($prInfo.title)"
    Info "Branch: $($prInfo.headRefName)"
    Info "CLI:    $CLIType $(if ($Model) { "(model: $Model)" })"
    Info "Max iterations: $MaxIterations"
    Info "Min severity:   $MinSeverity"
    Info ""

    # ── Resume or fresh start ──────────────────────────────────────────────
    $state = Read-State
    if ($state) {
        Info "Resuming from previous state (iteration $($state.currentIteration), phase: $($state.currentPhase))"

        # Verify worktree still exists
        if (-not (Test-Path $state.worktreePath)) {
            Warn "Previous worktree not found at $($state.worktreePath) — recreating..."
            $state.worktreePath = Get-OrCreateWorktree -Branch $prInfo.headRefName
        }
    } else {
        # Fresh start — create worktree
        $worktreePath = Get-OrCreateWorktree -Branch $prInfo.headRefName
        $state = New-State -Branch $prInfo.headRefName -WorktreePath $worktreePath

        # Save worktree info for external tools
        @{
            prNumber     = $PRNumber
            branch       = $prInfo.headRefName
            worktreePath = $worktreePath
            createdAt    = (Get-Date).ToString('o')
        } | ConvertTo-Json | Set-Content $worktreeInfoFile -Force

        Save-State $state
    }

    Info "Worktree: $($state.worktreePath)"

    # ── Confirm ────────────────────────────────────────────────────────────
    if (-not $Force) {
        $confirm = Read-Host "Rework PR #$PRNumber with up to $MaxIterations iterations? (y/N)"
        if ($confirm -notmatch '^[yY]') { Info "Cancelled."; return }
    }

    # ── Verify VS environment ─────────────────────────────────────────────
    # Build processes use Start-Process -WindowStyle Hidden to create isolated
    # console sessions. This prevents MSBuild's console signal handlers from
    # killing the parent pwsh process. The child build process will call
    # Enter-VsDevShell on its own if needed.

    $essentialsDone = $state.phaseHistory | Where-Object { $_.phase -eq 'build-essentials' -and $_.status -eq 'done' }
    if (-not $essentialsDone) {
        Info "Running build-essentials (one-time NuGet restore + baseline build)..."
        Add-PhaseRecord $state 'build-essentials' 'in-progress'

        $essentialsLog = Join-Path $genRoot 'build-essentials.log'
        $buildEssentials = Join-Path $state.worktreePath 'tools/build/build-essentials.cmd'
        if (-not (Test-Path $buildEssentials)) {
            $buildEssentials = Join-Path $repoRoot 'tools/build/build-essentials.cmd'
        }

        $bResult = Invoke-BuildIsolated -Command 'cmd.exe' `
            -Arguments @('/c', "`"$buildEssentials`"") `
            -WorkDir $state.worktreePath `
            -LogFile $essentialsLog `
            -TimeoutSeconds 600

        $essExitCode = $bResult.ExitCode

        if ($essExitCode -eq 0) {
            Add-PhaseRecord $state 'build-essentials' 'done' @{ exitCode = 0 }
            Info "Build-essentials succeeded"
        } else {
            Add-PhaseRecord $state 'build-essentials' 'failed' @{ exitCode = $essExitCode }
            Warn "Build-essentials failed (exit code $essExitCode) — the PR may already have build issues."
            Warn "Log: $essentialsLog"
            Warn "Continuing anyway — the review/fix loop will attempt to address build errors."
        }
    } else {
        Info "Build-essentials already completed (skipping)"
    }

    # ── Main Loop ──────────────────────────────────────────────────────────
    $startIter = $state.currentIteration
    for ($iter = $startIter; $iter -le $MaxIterations; $iter++) {

        $state.currentIteration = $iter
        Save-State $state

        Write-Host ""
        Write-Host ("─" * 50) -ForegroundColor DarkCyan
        Write-Host " Iteration $iter / $MaxIterations" -ForegroundColor DarkCyan
        Write-Host ("─" * 50) -ForegroundColor DarkCyan

        $iterDir = Join-Path $genRoot "iteration-$iter"
        if (-not (Test-Path $iterDir)) { New-Item -ItemType Directory -Path $iterDir -Force | Out-Null }

        # ── Check if review phase already done for this iteration (resume) ──
        $reviewDone = Get-LastPhaseOfType $state 'review' $iter
        if (-not $reviewDone -or $reviewDone.status -notin @('done')) {
            $reviewOk = Invoke-ReviewPhase -State $state -Iteration $iter
            if (-not $reviewOk) {
                Warn "Review phase failed/timed out in iteration $iter — stopping"
                break
            }
        } else {
            Info "  Review already done for iteration $iter (resuming)"
        }

        # ── Parse findings ──
        $parseDone = Get-LastPhaseOfType $state 'parse' $iter
        if (-not $parseDone -or $parseDone.status -ne 'done') {
            $findings = Invoke-ParsePhase -State $state -Iteration $iter
        } else {
            $findingsFile = Join-Path $iterDir 'findings.json'
            if (Test-Path $findingsFile) {
                $findings = Get-Content $findingsFile -Raw | ConvertFrom-Json
            } else {
                $findings = @()
            }
            Info "  Findings already parsed ($($findings.Count) actionable)"
        }

        # ── Check if done ──
        if ($findings.Count -eq 0) {
            Write-Host ""
            Write-Host "  ✅ No actionable findings — PR is CLEAN!" -ForegroundColor Green
            Add-PhaseRecord $state 'done' 'success' @{ finalFindings = 0 }

            # Write signal
            @{
                status              = 'success'
                prNumber            = $PRNumber
                timestamp           = (Get-Date).ToString('o')
                iterations          = $iter
                finalFindingsCount  = 0
                worktreePath        = $state.worktreePath
            } | ConvertTo-Json | Set-Content $signalFile -Force

            Write-ReworkSummary -State $state

            Write-Host ""
            Write-Host ("=" * 70) -ForegroundColor Green
            Write-Host " PR #$PRNumber is CLEAN after $iter iteration(s)" -ForegroundColor Green
            Write-Host " Worktree: $($state.worktreePath)" -ForegroundColor Green
            Write-Host " Summary:  $summaryFile" -ForegroundColor Green
            Write-Host ("=" * 70) -ForegroundColor Green
            Write-Host ""
            Write-Host "Review changes and push when ready:" -ForegroundColor Yellow
            Write-Host "  cd `"$($state.worktreePath)`"" -ForegroundColor White
            Write-Host "  git diff" -ForegroundColor White
            Write-Host "  git add -A && git commit -m `"fix: address review findings`" && git push" -ForegroundColor White

            return [PSCustomObject]@{
                PRNumber         = $PRNumber
                Status           = 'Clean'
                Iterations       = $iter
                FinalFindings    = 0
                WorktreePath     = $state.worktreePath
                SummaryPath      = $summaryFile
            }
        }

        Info "  $($findings.Count) findings to fix — proceeding to fix phase"

        # ── Fix ──
        $fixDone = Get-LastPhaseOfType $state 'fix' $iter
        if (-not $fixDone -or $fixDone.status -notin @('done')) {
            $fixOk = Invoke-FixPhase -State $state -Iteration $iter -Findings $findings
            if (-not $fixOk) {
                Warn "Fix phase failed/timed out in iteration $iter"
                # Continue to next iteration anyway — review will detect remaining issues
            }
        } else {
            Info "  Fix already done for iteration $iter (resuming)"
        }

        # ── Build ──
        $buildDone = Get-LastPhaseOfType $state 'build' $iter
        if (-not $buildDone -or $buildDone.status -notin @('done', 'skipped')) {
            $buildOk = Invoke-BuildPhase -State $state -Iteration $iter
            if (-not $buildOk) {
                Warn "Build failed — fix phase in next iteration will receive build errors"
                # Don't break — next iteration's fix will see the build log
            }
        } else {
            Info "  Build already done for iteration $iter (resuming)"
        }

        # ── Test ──
        $testDone = Get-LastPhaseOfType $state 'test' $iter
        if (-not $testDone -or $testDone.status -notin @('done', 'skipped')) {
            $testOk = Invoke-TestPhase -State $state -Iteration $iter
            if (-not $testOk) {
                Warn "Tests failed — fix phase in next iteration will receive test failures"
            }
        } else {
            Info "  Test already done for iteration $iter (resuming)"
        }
    }

    # ── Final verification review after the last fix ──
    # The loop above reviewed iteration N, fixed, built, tested — but never
    # re-reviewed to confirm the fixes worked. Run one more review-only pass.
    $verifyIter = $MaxIterations + 1
    $state.currentIteration = $verifyIter
    Save-State $state

    Write-Host ""
    Write-Host ("─" * 50) -ForegroundColor DarkCyan
    Write-Host " Final verification review" -ForegroundColor DarkCyan
    Write-Host ("─" * 50) -ForegroundColor DarkCyan

    $verifyDir = Join-Path $genRoot "iteration-$verifyIter"
    if (-not (Test-Path $verifyDir)) { New-Item -ItemType Directory -Path $verifyDir -Force | Out-Null }

    $verifyReviewOk = Invoke-ReviewPhase -State $state -Iteration $verifyIter
    $finalFindingsCount = 0
    if ($verifyReviewOk) {
        $verifyFindings = Invoke-ParsePhase -State $state -Iteration $verifyIter
        $finalFindingsCount = $verifyFindings.Count
        if ($finalFindingsCount -eq 0) {
            Write-Host "  ✅ Final verification: PR is CLEAN!" -ForegroundColor Green
            Add-PhaseRecord $state 'done' 'success' @{ finalFindings = 0 }

            @{
                status             = 'success'
                prNumber           = $PRNumber
                timestamp          = (Get-Date).ToString('o')
                iterations         = $MaxIterations
                finalFindingsCount = 0
                worktreePath       = $state.worktreePath
            } | ConvertTo-Json | Set-Content $signalFile -Force

            Write-ReworkSummary -State $state
            return [PSCustomObject]@{
                PRNumber      = $PRNumber
                Status        = 'Clean'
                Iterations    = $MaxIterations
                FinalFindings = 0
                WorktreePath  = $state.worktreePath
                SummaryPath   = $summaryFile
            }
        }
        Info "  Final verification: $finalFindingsCount findings remain"
    } else {
        Warn "  Final verification review failed — using last known findings count"
        $lastFindingsFile = Join-Path $genRoot "iteration-$MaxIterations" 'findings.json'
        if (Test-Path $lastFindingsFile) {
            $finalFindingsCount = (Get-Content $lastFindingsFile -Raw | ConvertFrom-Json).Count
        }
    }

    @{
        status              = 'max-iterations'
        prNumber            = $PRNumber
        timestamp           = (Get-Date).ToString('o')
        iterations          = $MaxIterations
        finalFindingsCount  = $finalFindingsCount
        worktreePath        = $state.worktreePath
    } | ConvertTo-Json | Set-Content $signalFile -Force

    Write-ReworkSummary -State $state

    Write-Host ""
    Write-Host ("=" * 70) -ForegroundColor Yellow
    Write-Host " PR #$PRNumber — max iterations ($MaxIterations) reached" -ForegroundColor Yellow
    Write-Host " Remaining findings: $finalFindingsCount" -ForegroundColor Yellow
    Write-Host " Worktree: $($state.worktreePath)" -ForegroundColor Yellow
    Write-Host " Summary:  $summaryFile" -ForegroundColor Yellow
    Write-Host ("=" * 70) -ForegroundColor Yellow

    return [PSCustomObject]@{
        PRNumber         = $PRNumber
        Status           = 'MaxIterations'
        Iterations       = $MaxIterations
        FinalFindings    = $finalFindingsCount
        WorktreePath     = $state.worktreePath
        SummaryPath      = $summaryFile
    }
}
catch {
    Err "Error: $($_.Exception.Message)"

    # Write failure signal
    @{
        status    = 'failure'
        prNumber  = $PRNumber
        timestamp = (Get-Date).ToString('o')
        error     = $_.Exception.Message
    } | ConvertTo-Json | Set-Content $signalFile -Force

    return [PSCustomObject]@{
        PRNumber = $PRNumber
        Status   = 'Failed'
        Error    = $_.Exception.Message
    }
}
