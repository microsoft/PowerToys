<#
.SYNOPSIS
    Shared task-runner library: file-based progress, parallel execution, timeout,
    heartbeat liveness, and crash-resume — all driven by result files on disk
    (no separate JSON state).

.DESCRIPTION
    Design principles:
    ─────────────────
    a. PARALLEL CONTROL  — Start-TaskBatch queues PowerShell jobs up to a
       configurable throttle limit, polling for completion every N seconds.

    b. TIMEOUT / LIVENESS — Every running task writes a heartbeat file
       (.heartbeat) periodically.  The supervisor detects stale heartbeats
       and kills the job, then optionally retries.

    c. RESUMABLE VIA RESULT FILES — Each step of each task writes a
       well-known output file (e.g., enriched.json, categorized.json,
       summary.md).  Get-StepStatus inspects the task folder to decide
       what has already completed.  No extra state JSON is required.

    d. SELF-CONTAINED STEPS — Every step is a script block that receives
       the task folder path.  A step is "done" when its expected output
       file exists and is non-empty.

    File layout per task:
        <RunRoot>/<TaskId>/
            .started          — touched when the task begins
            .heartbeat        — updated every HeartbeatSec seconds
            .completed        — touched on success (contains exit code 0)
            .failed           — touched on failure (contains error text)
            .timeout          — touched when killed by supervisor
            run-YYYYMMDD-HHmmss.log — full stdout/stderr capture (unique per attempt)
            step1-collect.json
            step2-enrich.json
            step3-categorize.json
            ...               — any file the step produces

    e. LOG CAPTURE — Every job attempt writes a timestamped log file
       (run-<ts>.log) capturing all stdout/stderr.  Each retry gets its
       own unique file.  Test-LogAlive checks whether the latest log was
       written to recently (secondary liveness: even when heartbeat is
       stale, a growing log means the CLI is still producing output).

    Usage:
        . ./TaskRunner.ps1          # dot-source
        Start-TaskBatch -Tasks $list -RunRoot $dir -ThrottleLimit 5

.NOTES
    Dot-source this file; all functions become available in the caller's scope.
#>

#requires -Version 7.0

# ── Console helpers ─────────────────────────────────────────────────────────

function Write-TRInfo  { param([string]$M) Write-Host "  [TR] $M" -ForegroundColor Cyan }
function Write-TRWarn  { param([string]$M) Write-Host "  [TR] $M" -ForegroundColor Yellow }
function Write-TRErr   { param([string]$M) Write-Host "  [TR] $M" -ForegroundColor Red }
function Write-TROk    { param([string]$M) Write-Host "  [TR] $M" -ForegroundColor Green }

# ── Step status (resume detection) ──────────────────────────────────────────

function Get-StepStatus {
    <#
    .SYNOPSIS
        Inspect a task folder and return which steps have completed.
    .DESCRIPTION
        Looks for expected output files to decide whether a step is done.
        Returns a hashtable: @{ 'step1-collect' = $true; 'step2-enrich' = $false; ... }
    .PARAMETER TaskDir
        Absolute path to the task folder.
    .PARAMETER Steps
        Array of step descriptors: @( @{ Name='step1-collect'; OutputFile='step1-collect.json' }, ... )
    #>
    param(
        [Parameter(Mandatory)]
        [string]$TaskDir,

        [Parameter(Mandatory)]
        [array]$Steps
    )

    $status = [ordered]@{}
    foreach ($step in $Steps) {
        $outFile = Join-Path $TaskDir $step.OutputFile
        $done = (Test-Path $outFile) -and ((Get-Item $outFile).Length -gt 0)
        $status[$step.Name] = $done
    }
    return $status
}

function Get-NextPendingStep {
    <#
    .SYNOPSIS
        Return the first step that has NOT completed (output file missing/empty).
    #>
    param(
        [Parameter(Mandatory)]
        [string]$TaskDir,

        [Parameter(Mandatory)]
        [array]$Steps
    )

    $status = Get-StepStatus -TaskDir $TaskDir -Steps $Steps
    foreach ($step in $Steps) {
        if (-not $status[$step.Name]) {
            return $step
        }
    }
    return $null   # all done
}

function Get-TaskProgress {
    <#
    .SYNOPSIS
        Returns a human-readable progress string plus numeric percentage.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$TaskDir,

        [Parameter(Mandatory)]
        [array]$Steps
    )

    $status = Get-StepStatus -TaskDir $TaskDir -Steps $Steps
    $done = ($status.Values | Where-Object { $_ }).Count
    $total = $Steps.Count
    $pct = if ($total -gt 0) { [math]::Round(($done / $total) * 100) } else { 0 }
    $next = Get-NextPendingStep -TaskDir $TaskDir -Steps $Steps

    return [PSCustomObject]@{
        CompletedCount = $done
        TotalCount     = $total
        Percent        = $pct
        NextStep       = if ($next) { $next.Name } else { $null }
        Status         = $status
    }
}

# ── Heartbeat helpers ───────────────────────────────────────────────────────

function Test-HeartbeatAlive {
    <#
    .SYNOPSIS
        Returns $true if the heartbeat was updated within the staleness window.
    #>
    param(
        [string]$TaskDir,
        [int]$StaleSec = 120
    )
    $hb = Join-Path $TaskDir '.heartbeat'
    if (-not (Test-Path $hb)) { return $false }
    $ts = Get-Content $hb -Raw
    try {
        $lastBeat = [datetime]::Parse($ts.Trim())
        return ((Get-Date) - $lastBeat).TotalSeconds -lt $StaleSec
    } catch { return $false }
}

# ── Log-capture helpers ──────────────────────────────────────────────────────

function Get-LatestLogFile {
    <#
    .SYNOPSIS
        Return the most recently written .log file in a task folder (or $null).
    #>
    param([Parameter(Mandatory)] [string]$TaskDir)

    Get-ChildItem -Path $TaskDir -Filter '*.log' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

function Test-LogAlive {
    <#
    .SYNOPSIS
        Returns $true if the latest log file was written to within $StaleSec seconds.
        This is a secondary liveness signal: if the log keeps growing, the CLI is
        still producing output even if the heartbeat thread is lagging.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$TaskDir,

        [int]$StaleSec = 90
    )
    $latest = Get-LatestLogFile -TaskDir $TaskDir
    if (-not $latest) { return $false }
    return ((Get-Date) - $latest.LastWriteTime).TotalSeconds -lt $StaleSec
}

function Get-TaskLogSummary {
    <#
    .SYNOPSIS
        Return a summary of log files in a task folder: count, latest path,
        latest size, and whether the latest log is still being written to.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$TaskDir,

        [int]$StaleSec = 90
    )
    $logs = Get-ChildItem -Path $TaskDir -Filter '*.log' -File -ErrorAction SilentlyContinue
    $latest = $logs | Sort-Object LastWriteTime -Descending | Select-Object -First 1

    return [PSCustomObject]@{
        LogCount      = ($logs | Measure-Object).Count
        LatestLog     = if ($latest) { $latest.Name } else { $null }
        LatestSizeKB  = if ($latest) { [math]::Round($latest.Length / 1024, 1) } else { 0 }
        LatestWritten = if ($latest) { $latest.LastWriteTime.ToString('o') } else { $null }
        LogAlive      = if ($latest) { ((Get-Date) - $latest.LastWriteTime).TotalSeconds -lt $StaleSec } else { $false }
    }
}

# ── Signal files ────────────────────────────────────────────────────────────

function Set-TaskFailed {
    param(
        [string]$TaskDir,
        [string]$ErrorText
    )
    $ErrorText | Set-Content (Join-Path $TaskDir '.failed') -Force
    Remove-Item (Join-Path $TaskDir '.heartbeat') -ErrorAction SilentlyContinue
}

function Set-TaskTimeout {
    param([string]$TaskDir)
    "Timeout at $(Get-Date -Format 'o')" | Set-Content (Join-Path $TaskDir '.timeout') -Force
    Remove-Item (Join-Path $TaskDir '.heartbeat') -ErrorAction SilentlyContinue
}

function Clear-TaskSignals {
    <#
    .SYNOPSIS
        Remove all signal files so the task can be retried.
    #>
    param([string]$TaskDir)
    @('.started', '.heartbeat', '.completed', '.failed', '.timeout') | ForEach-Object {
        Remove-Item (Join-Path $TaskDir $_) -ErrorAction SilentlyContinue
    }
}

# ── Batch execution engine ──────────────────────────────────────────────────

function Start-TaskBatch {
    <#
    .SYNOPSIS
        Execute a list of tasks in parallel with throttling, timeout, heartbeat
        liveness detection, and automatic retry.

    .PARAMETER Tasks
        Array of task descriptors.  Each must have:
            Id          — unique string identifying this task (used as subfolder name)
            ScriptBlock — the code to run, receives ($TaskDir, $TaskDescriptor)
            Label       — human-readable label for progress display

    .PARAMETER RunRoot
        Root directory for this batch run.  Each task gets <RunRoot>/<Id>/.

    .PARAMETER MaxConcurrent
        Maximum concurrent jobs.  Default: 5.

    .PARAMETER TimeoutMin
        Per-task wall-clock timeout in minutes.  Default: 10.

    .PARAMETER HeartbeatStaleSec
        Seconds without heartbeat before considering a task stuck.  Default: 120.

    .PARAMETER MaxRetryCount
        How many times to retry a failed/timed-out task.  Default: 2.

    .PARAMETER PollIntervalSec
        Supervisor polling interval.  Default: 5.

    .OUTPUTS
        PSCustomObject with Succeeded, Failed, TimedOut arrays.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$Tasks,

        [Parameter(Mandatory)]
        [string]$RunRoot,

        [int]$MaxConcurrent = 5,

        [int]$TimeoutMin = 10,

        [int]$HeartbeatStaleSec = 120,

        [int]$MaxRetryCount = 2,

        [int]$PollIntervalSec = 5
    )

    # The caller may have set $ErrorActionPreference = 'Stop' (common in robust
    # scripts).  Inside the batch engine, non-terminating errors from Start-Job
    # serialization, Receive-Job, or Remove-Job must NOT crash the supervisor
    # loop — they are handled explicitly via job state inspection.
    $ErrorActionPreference = 'Continue'

    # Diagnostic trace file — aids troubleshooting when the batch supervisor
    # exits unexpectedly (VS Code terminal kills, resource limits, etc.)
    $debugLog = Join-Path $RunRoot '_taskrunner-debug.log'

    if (-not (Test-Path $RunRoot)) {
        New-Item -ItemType Directory -Path $RunRoot -Force | Out-Null
    }

    # Build work queue (skip tasks already completed on disk)
    $queue = [System.Collections.Queue]::new()
    $alreadyDone = @()
    foreach ($t in $Tasks) {
        $taskDir = Join-Path $RunRoot $t.Id
        if (Test-Path (Join-Path $taskDir '.completed')) {
            $alreadyDone += $t.Id
        } else {
            # Clear any leftover failure/timeout signals so we can retry
            if (Test-Path $taskDir) { Clear-TaskSignals -TaskDir $taskDir }
            $queue.Enqueue(@{ Task = $t; Attempt = 0 })
        }
    }

    if ($alreadyDone.Count -gt 0) {
        Write-TROk "Resuming — $($alreadyDone.Count) task(s) already completed on disk"
    }

    # Result accumulators
    $succeeded = [System.Collections.ArrayList]::new()
    $failed    = [System.Collections.ArrayList]::new()
    $timedOut  = [System.Collections.ArrayList]::new()

    $alreadyDone | ForEach-Object { [void]$succeeded.Add($_) }

    $activeJobs = [System.Collections.ArrayList]::new()
    $totalTasks = $Tasks.Count
    $startTime  = Get-Date

    # Retry helper
    $retryQueue = [System.Collections.Queue]::new()

    $loopIter = 0
    while ($queue.Count -gt 0 -or $activeJobs.Count -gt 0 -or $retryQueue.Count -gt 0) {
        $loopIter++
        "[$(Get-Date -Format 'o')] LOOP iter=$loopIter q=$($queue.Count) active=$($activeJobs.Count) retry=$($retryQueue.Count)" | Out-File $debugLog -Append

        # Move retry items back when main queue empty
        if ($queue.Count -eq 0 -and $retryQueue.Count -gt 0 -and $activeJobs.Count -lt $MaxConcurrent) {
            $ri = $retryQueue.Dequeue()
            Write-TRWarn "Retrying $($ri.Task.Id) (attempt $($ri.Attempt + 1)/$($MaxRetryCount + 1))"
            Start-Sleep -Seconds 5
            $queue.Enqueue(@{ Task = $ri.Task; Attempt = $ri.Attempt + 1 })
        }

        # Launch new jobs up to throttle
        while ($activeJobs.Count -lt $MaxConcurrent -and $queue.Count -gt 0) {
            $item = $queue.Dequeue()
            $t = $item.Task
            $attempt = $item.Attempt
            $taskDir = Join-Path $RunRoot $t.Id

            $label = if ($t.Label) { $t.Label } else { $t.Id }
            $attemptTag = if ($attempt -gt 0) { " (retry $attempt)" } else { '' }
            Write-TRInfo "Starting: $label$attemptTag"

            # Generate a unique log file name for this attempt
            $logTs = (Get-Date).ToString('yyyyMMdd-HHmmss')
            if (-not (Test-Path $taskDir)) { New-Item -ItemType Directory -Path $taskDir -Force | Out-Null }
            $logFile = Join-Path $taskDir "run-$logTs.log"

            $job = Start-Job -Name "TR-$($t.Id)" -ScriptBlock {
                param($TaskDir, $TaskDescriptor, $ScriptBlockText, $LogFile)

                # Recreate the script block inside the job
                $sb = [scriptblock]::Create($ScriptBlockText)

                # Mark started + first heartbeat
                if (-not (Test-Path $TaskDir)) {
                    New-Item -ItemType Directory -Path $TaskDir -Force | Out-Null
                }
                (Get-Date).ToString('o') | Set-Content (Join-Path $TaskDir '.started') -Force
                $hbPath = Join-Path $TaskDir '.heartbeat'
                (Get-Date).ToString('o') | Set-Content $hbPath -Force

                # Start a background runspace that keeps the heartbeat alive
                # every 30s.  This is essential for tasks that invoke long-
                # running external processes which produce no stdout (e.g.
                # copilot.exe reviews).
                $hbPs = [powershell]::Create()
                [void]$hbPs.AddScript({
                    param($Path, $Sec)
                    while ($true) {
                        Start-Sleep -Seconds $Sec
                        try { (Get-Date).ToString('o') | Set-Content $Path -Force } catch {}
                    }
                }).AddArgument($hbPath).AddArgument(30)
                $hbHandle = $hbPs.BeginInvoke()

                # Write log header
                "[$(Get-Date -Format 'o')] Task started: $($TaskDescriptor.Id)" | Out-File $LogFile -Encoding utf8

                try {
                    # Run the script block, capturing all output streams to log
                    & $sb $TaskDir $TaskDescriptor 2>&1 | ForEach-Object {
                        $line = $_.ToString()
                        $line | Out-File $LogFile -Append -Encoding utf8
                        # Also emit to job output so Receive-Job still works
                        $_
                    }
                    "`n[$(Get-Date -Format 'o')] Task completed successfully" | Out-File $LogFile -Append -Encoding utf8
                    '0' | Set-Content (Join-Path $TaskDir '.completed') -Force
                } catch {
                    "`n[$(Get-Date -Format 'o')] Task FAILED: $($_.Exception.Message)" | Out-File $LogFile -Append -Encoding utf8
                    $_.Exception.Message | Set-Content (Join-Path $TaskDir '.failed') -Force
                    throw
                } finally {
                    # Stop the heartbeat runspace and clean up
                    try { $hbPs.Stop() } catch {}
                    try { $hbPs.Dispose() } catch {}
                    Remove-Item (Join-Path $TaskDir '.heartbeat') -ErrorAction SilentlyContinue
                }
            } -ArgumentList $taskDir, $t, $t.ScriptBlock.ToString(), $logFile

            [void]$activeJobs.Add(@{
                Job       = $job
                Task      = $t
                TaskDir   = $taskDir
                StartTime = Get-Date
                Attempt   = $attempt
            })
        }

        # Poll active jobs
        $justFinished = @()
        foreach ($aj in $activeJobs) {
            $job     = $aj.Job
            $t       = $aj.Task
            $taskDir = $aj.TaskDir
            $elapsed = (Get-Date) - $aj.StartTime

            if ($job.State -eq 'Completed') {
                try { Receive-Job -Job $job -ErrorAction SilentlyContinue | Out-Null } catch {}
                Remove-Job -Job $job -Force
                Write-TROk "✓ $($t.Id) completed ($([math]::Round($elapsed.TotalSeconds))s)"
                [void]$succeeded.Add($t.Id)
                $justFinished += $aj
            }
            elseif ($job.State -eq 'Failed') {
                $errMsg = try { $job.ChildJobs[0].JobStateInfo.Reason.Message } catch { 'Unknown error' }
                Remove-Job -Job $job -Force

                if ($aj.Attempt -lt $MaxRetryCount) {
                    Write-TRWarn "⚠ $($t.Id) failed — queueing retry: $errMsg"
                    $retryQueue.Enqueue(@{ Task = $t; Attempt = $aj.Attempt })
                } else {
                    Write-TRErr "✗ $($t.Id) failed after $($aj.Attempt + 1) attempt(s): $errMsg"
                    Set-TaskFailed -TaskDir $taskDir -ErrorText $errMsg
                    [void]$failed.Add($t.Id)
                }
                $justFinished += $aj
            }
            elseif ($elapsed.TotalMinutes -ge $TimeoutMin) {
                # Wall-clock timeout
                Stop-Job -Job $job -ErrorAction SilentlyContinue
                Remove-Job -Job $job -Force
                Set-TaskTimeout -TaskDir $taskDir

                if ($aj.Attempt -lt $MaxRetryCount) {
                    Write-TRWarn "⏱ $($t.Id) timed out — queueing retry"
                    $retryQueue.Enqueue(@{ Task = $t; Attempt = $aj.Attempt })
                } else {
                    Write-TRErr "⏱ $($t.Id) timed out after $($aj.Attempt + 1) attempt(s)"
                    [void]$timedOut.Add($t.Id)
                }
                $justFinished += $aj
            }
            elseif ($elapsed.TotalSeconds -gt 30 -and -not (Test-HeartbeatAlive -TaskDir $taskDir -StaleSec $HeartbeatStaleSec)) {
                # Heartbeat stale (grace period elapsed) — check if log is still growing
                # (CLI may be producing output without the script hitting a Beat call)
                if (Test-LogAlive -TaskDir $taskDir -StaleSec $HeartbeatStaleSec) {
                    # Log is still being written — CLI is alive, skip killing
                    Write-TRWarn "⚡ $($t.Id) heartbeat stale but log still growing — keeping alive"
                } else {
                    # Both heartbeat and log are stale — truly stuck
                    $hbFile = Join-Path $taskDir '.heartbeat'
                    if (Test-Path $hbFile) {
                        $lastBeat = Get-Content $hbFile -Raw
                        Write-TRWarn "💀 $($t.Id) heartbeat stale (last: $lastBeat), log also stale"
                    } else {
                        Write-TRWarn "💀 $($t.Id) no heartbeat file, log also stale"
                    }
                    Stop-Job -Job $job -ErrorAction SilentlyContinue
                    Remove-Job -Job $job -Force
                    Set-TaskTimeout -TaskDir $taskDir

                    if ($aj.Attempt -lt $MaxRetryCount) {
                        $retryQueue.Enqueue(@{ Task = $t; Attempt = $aj.Attempt })
                    } else {
                        Write-TRErr "💀 $($t.Id) stuck after $($aj.Attempt + 1) attempt(s)"
                        [void]$timedOut.Add($t.Id)
                    }
                    $justFinished += $aj
                }
            }
        }

        # Remove finished from active list
        foreach ($fin in $justFinished) {
            $activeJobs.Remove($fin) | Out-Null
        }

        # Progress line
        $doneCount = $succeeded.Count + $failed.Count + $timedOut.Count
        $elapsed = (Get-Date) - $startTime
        "[$(Get-Date -Format 'o')] PROGRESS done=$doneCount/$totalTasks active=$($activeJobs.Count) q=$($queue.Count) retry=$($retryQueue.Count) elapsed=$([math]::Round($elapsed.TotalSeconds))s" | Out-File $debugLog -Append
        $jobStates = $activeJobs | ForEach-Object { "$($_.Task.Id):$($_.Job.State)" }
        "[$(Get-Date -Format 'o')] JOB_STATES $($jobStates -join ', ')" | Out-File $debugLog -Append
        if ($activeJobs.Count -gt 0) {
            Write-Host "`r  [$doneCount/$totalTasks] active: $($activeJobs.Count) | elapsed: $([math]::Round($elapsed.TotalSeconds))s" `
                -ForegroundColor DarkGray -NoNewline
        }

        if ($activeJobs.Count -gt 0 -or $queue.Count -gt 0 -or $retryQueue.Count -gt 0) {
            "[$(Get-Date -Format 'o')] SLEEPING ${PollIntervalSec}s" | Out-File $debugLog -Append
            Start-Sleep -Seconds $PollIntervalSec
        }
    }

    "[$(Get-Date -Format 'o')] LOOP EXITED iter=$loopIter" | Out-File $debugLog -Append
    Write-Host ''  # clear progress line
    $totalElapsed = (Get-Date) - $startTime

    return [PSCustomObject]@{
        TotalTasks = $totalTasks
        Succeeded  = @($succeeded)
        Failed     = @($failed)
        TimedOut   = @($timedOut)
        ElapsedSec = [math]::Round($totalElapsed.TotalSeconds)
    }
}
