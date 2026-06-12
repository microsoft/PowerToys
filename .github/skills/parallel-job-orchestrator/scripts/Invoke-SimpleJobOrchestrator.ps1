<#
.SYNOPSIS
	Generic job orchestrator: queues, starts, monitors, retries, and cleans up
	PowerShell background jobs with configurable concurrency.

.DESCRIPTION
	Accepts an array of job definitions (created via New-JobDefinition), queues
	them in memory, and runs up to MaxConcurrent at a time. Jobs are retried
	up to MaxRetryCount times when they:
	  - Exit with a non-zero exit code
	  - Finish with a Failed or NotFound job state
	  - Stall (log-file inactivity exceeds InactivityTimeoutSeconds)
	When a job finishes or is abandoned, its optional CleanupTask scriptblock
	runs.

	Returns an array of result objects with final state, exit code, retry count,
	and output directory for every definition.

	This is the CANONICAL parallel execution engine for this repository.
	ALL skills that need to run copilot, claude, or any CLI tool in parallel
	MUST use this orchestrator. Do NOT use Start-Job, ForEach-Object -Parallel,
	or Start-Process directly — those approaches have known PowerShell 7 crash
	bugs.

	Part of the parallel-job-orchestrator skill:
	<configRoot>/skills/parallel-job-orchestrator/SKILL.md

.PARAMETER JobDefinitions
	Array of job-definition hashtables created by New-JobDefinition.

.PARAMETER MaxConcurrent
	Maximum number of jobs running simultaneously. Default 4.

.PARAMETER InactivityTimeoutSeconds
	Seconds of zero log-file growth before a job is considered stale. Default 60.

.PARAMETER MaxRetryCount
	How many times to restart a stale job before giving up. Default 3.

.PARAMETER PollIntervalSeconds
	How often (seconds) to check job health. Default 5.

.PARAMETER LogDir
	Directory for the orchestrator's own progress log. Default: TEMP.
#>
# NOTE: Do NOT use [CmdletBinding()] here. When a caller sets
# $ErrorActionPreference='Stop', CmdletBinding propagates that as the implicit
# -ErrorAction common parameter, overriding any local assignment. A monitoring
# loop must be resilient, so we intentionally stay as a simple script.
# IMPORTANT: Do not use [Parameter()], [ValidateSet()] or any attribute on params
# either — those ALSO implicitly enable advanced-script behaviour.
param(
	[hashtable[]]$JobDefinitions,

	[int]$MaxConcurrent = 4,

	[int]$InactivityTimeoutSeconds = 60,

	[int]$MaxRetryCount = 3,

	[int]$PollIntervalSeconds = 5,

	[string]$LogDir
)

# Manual mandatory check (replacing [Parameter(Mandatory)] which makes this
# an advanced script and re-enables ErrorActionPreference propagation).
if (-not $JobDefinitions -or $JobDefinitions.Count -eq 0) {
	Write-Error 'Invoke-SimpleJobOrchestrator: -JobDefinitions is required and must not be empty.'
	return @()
}

# Orchestrator must be resilient — individual operations handle their own errors.
$ErrorActionPreference = 'Continue'

# ── logging ──────────────────────────────────────────────────────────────
# Verbose progress goes to a log file to avoid terminal-output issues that
# can silently terminate the script when run inside VS Code / IDE terminals.
# Only summary-level messages go to Write-Host (console).

if (-not $LogDir) { $LogDir = $env:TEMP }
New-Item -ItemType Directory -Path $LogDir -Force -ErrorAction SilentlyContinue | Out-Null
$script:_orchestratorLog = Join-Path $LogDir "orchestrator-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"

function Write-Log {
	param([string]$Message)
	$ts = Get-Date -Format 'HH:mm:ss'
	$line = "[$ts] $Message"
	try { Add-Content -Path $script:_orchestratorLog -Value $line -ErrorAction SilentlyContinue }
	catch { }
}

function Write-ProgressMessage {
	<# Write to both console and log file. Use sparingly. #>
	param([string]$Message)
	Write-Log $Message
	Write-Host $Message
}

# ── helpers ──────────────────────────────────────────────────────────────

# IMPORTANT: Start-TrackedJob is deliberately NOT a function. PowerShell 7
# silently crashes the host process when Start-Job is called from within a
# function that is invoked inside a while loop in a .ps1 script file (~10-15
# jobs triggers it). Inline the Start-Job call at every call site instead.

# Shared scriptblock for all tracked jobs (defined once, reused).
$_jobScriptBlock = {
	param($Cmd, $ArgList, $WorkDir, $LogFile)
	Set-Location $WorkDir
	if (Test-Path $LogFile) { Remove-Item $LogFile -Force }
	& $Cmd @ArgList *> $LogFile
	[PSCustomObject]@{
		Command  = $Cmd
		ExitCode = $LASTEXITCODE
		LogPath  = $LogFile
	}
}

# NOTE: Test-MonitorFilesActive, Stop-TrackedJob, and Invoke-CleanupTask
# are deliberately NOT functions. PowerShell 7 silently crashes the host when
# certain cmdlets (Stop-Job, Remove-Job, Get-Job, Get-Item) are called from
# within a function in a while loop inside a .ps1 script. Their logic is
# inlined at every call site below.

function Get-TrackerResult {
	param([hashtable]$Tracker)

	# Job output was collected and stored in _ReceivedOutput / _FinalJobState
	# at completion time (before Remove-Job). Fall back to live query only if
	# the tracker somehow missed the collection step.
	$received = $Tracker._ReceivedOutput
	$state    = $Tracker._FinalJobState

	if (-not $state) {
		$jobObj = Get-Job -Id $Tracker.JobId -ErrorAction SilentlyContinue
		$received = if ($jobObj) {
			Receive-Job -Id $Tracker.JobId -Keep -ErrorAction SilentlyContinue
		}
		else { $null }
		$state = if ($jobObj) { $jobObj.State } else { 'Removed' }
	}

	[PSCustomObject]@{
		Label      = $Tracker.Label
		JobId      = $Tracker.JobId
		Status     = $Tracker.Status
		JobState   = $state
		ExitCode   = if ($received) { $received.ExitCode } else { $null }
		RetryCount = $Tracker.RetryCount
		OutputDir  = $Tracker.ExecutionParameters.OutputDir
		LogPath    = $Tracker.ExecutionParameters.LogPath
	}
}

# ── build tracker list ───────────────────────────────────────────────────

$queue    = [System.Collections.Generic.Queue[hashtable]]::new()
$running  = [System.Collections.Generic.List[hashtable]]::new()
$finished = [System.Collections.Generic.List[hashtable]]::new()

foreach ($def in $JobDefinitions) {
	$tracker = @{
		Label               = $def.Label
		ExecutionParameters = $def.ExecutionParameters
		MonitorFiles        = $def.MonitorFiles
		CleanupTask         = $def.CleanupTask
		Status              = 'Queued'
		JobId               = $null
		RetryCount          = 0
		LastFileSizes       = @{}
		LastChangeTime      = [DateTime]::UtcNow
	}
	$queue.Enqueue($tracker)
}

Write-ProgressMessage "Orchestrator: $($queue.Count) jobs queued, max $MaxConcurrent concurrent. Log: $script:_orchestratorLog"

# ── main loop ────────────────────────────────────────────────────────────

$loopIteration = 0

while ($queue.Count -gt 0 -or $running.Count -gt 0) {
	$loopIteration++

	try {
		$ErrorActionPreference = 'Continue'

		# fill slots from queue
		while ($running.Count -lt $MaxConcurrent -and $queue.Count -gt 0) {
			$t = $queue.Dequeue()
			Write-Log "Dequeued $($t.Label); about to start job (running=$($running.Count), queue=$($queue.Count))"
			try {
				# ── inline Start-TrackedJob (see note above about PS7 crash) ──
				$ep = $t.ExecutionParameters
				New-Item -ItemType Directory -Path $ep.OutputDir -Force | Out-Null
				$job = Start-Job -Name $ep.JobName -ScriptBlock $_jobScriptBlock `
					-ArgumentList $ep.Command, $ep.Arguments, $ep.WorkingDir, $ep.LogPath
				$t.JobId          = $job.Id
				$t.Status         = 'Running'
				$t.LastFileSizes  = @{}
				$t.LastChangeTime = [DateTime]::UtcNow
				foreach ($f in $t.MonitorFiles) { $t.LastFileSizes[$f] = 0L }
				Write-Log "[$($t.Label)] Started job $($job.Id)"
			}
			catch {
				Write-Log "Start job FAILED for $($t.Label): $_"
				$t.Status = 'Failed'
				$finished.Add($t)
				continue
			}
			$running.Add($t)
		}

		Write-Log "Sleeping ${PollIntervalSeconds}s..."

		Start-Sleep -Seconds $PollIntervalSeconds

		# evaluate every running tracker
		$toRemove = [System.Collections.Generic.List[hashtable]]::new()

		foreach ($t in $running) {
			$jobObj   = Get-Job -Id $t.JobId -ErrorAction SilentlyContinue
			$jobState = if ($jobObj) { $jobObj.State } else { 'NotFound' }

			# ── finished naturally ────────────────────────────────────
			if ($jobState -in 'Completed', 'Failed', 'NotFound') {
				# Collect job output before deciding whether to retry.
				$received = $null
				if ($jobObj) {
					$received = Receive-Job -Id $t.JobId -ErrorAction SilentlyContinue
				}
				Remove-Job -Id $t.JobId -Force -ErrorAction SilentlyContinue

				$exitCode = if ($received) { $received.ExitCode } else { $null }
				$isFailedExit = ($jobState -in 'Failed', 'NotFound') -or
				                ($null -ne $exitCode -and $exitCode -ne 0)

				# ── retry if the process exited with failure ──────────
				if ($isFailedExit -and $t.RetryCount -lt $MaxRetryCount) {
					$t.RetryCount++
					Write-Log "[$($t.Label)] Exited with failure (state=$jobState, exit=$exitCode) — retry $($t.RetryCount)/$MaxRetryCount"
					# ── inline cleanup before retry (no function — see PS7 crash note) ──
					if ($t.CleanupTask) {
						try { & $t.CleanupTask $t }
						catch { Write-Log "[$($t.Label)] Cleanup failed: $_" }
					}
					# ── inline Start-TrackedJob for retry (see note about PS7 crash) ──
					$ep = $t.ExecutionParameters
					New-Item -ItemType Directory -Path $ep.OutputDir -Force | Out-Null
					$job = Start-Job -Name $ep.JobName -ScriptBlock $_jobScriptBlock `
						-ArgumentList $ep.Command, $ep.Arguments, $ep.WorkingDir, $ep.LogPath
					$t.JobId          = $job.Id
					$t.Status         = 'Running'
					$t.LastFileSizes  = @{}
					$t.LastChangeTime = [DateTime]::UtcNow
					foreach ($f in $t.MonitorFiles) { $t.LastFileSizes[$f] = 0L }
					Write-Log "[$($t.Label)] Retry started job $($job.Id)"
					continue
				}

				$t.Status = if ($isFailedExit) { 'Failed' } else { $jobState }
				Write-Log "[$($t.Label)] Finished (state=$jobState, exit=$exitCode) after $($t.RetryCount) retries."

				$t._ReceivedOutput = $received
				$t._FinalJobState  = $jobState

				# ── inline cleanup (no function — see PS7 crash note) ──
				if ($t.CleanupTask) {
					try { & $t.CleanupTask $t }
					catch { Write-Log "[$($t.Label)] Cleanup failed: $_" }
				}
				$toRemove.Add($t)
				continue
			}

			# ── still running — check monitor files ──────────────────
			$active = $false
			try {
				# ── inline file-activity check (no function — see PS7 crash note) ──
				$_anyGrew = $false
				foreach ($_f in $t.MonitorFiles) {
					$_sz = 0L
					if (Test-Path $_f) { $_sz = ([System.IO.FileInfo]::new($_f)).Length }
					if ($_sz -ne $t.LastFileSizes[$_f]) {
						$t.LastFileSizes[$_f] = $_sz
						$_anyGrew = $true
					}
				}
				$active = $_anyGrew
			}
			catch { $active = $true }

			if ($active) {
				$t.LastChangeTime = [DateTime]::UtcNow
				continue
			}

			$staleSecs = [math]::Round(([DateTime]::UtcNow - $t.LastChangeTime).TotalSeconds)
			if ($staleSecs -lt $InactivityTimeoutSeconds) { continue }

			# ── stale — retry or give up ─────────────────────────────
			if ($t.RetryCount -ge $MaxRetryCount) {
				Write-Log "[$($t.Label)] Max retries ($MaxRetryCount) after ${staleSecs}s stale. Giving up."
				$t.Status = 'Abandoned'
				# ── inline stop + cleanup (no function — see PS7 crash note) ──
				Stop-Job  -Id $t.JobId -ErrorAction SilentlyContinue
				Remove-Job -Id $t.JobId -Force -ErrorAction SilentlyContinue
				if ($t.CleanupTask) {
					try { & $t.CleanupTask $t }
					catch { Write-Log "[$($t.Label)] Cleanup failed: $_" }
				}
				$toRemove.Add($t)
				continue
			}

			$t.RetryCount++
			Write-Log "[$($t.Label)] Stale ${staleSecs}s — retry $($t.RetryCount)/$MaxRetryCount"
			# ── inline stop (no function — see PS7 crash note) ──
			Stop-Job  -Id $t.JobId -ErrorAction SilentlyContinue
			Remove-Job -Id $t.JobId -Force -ErrorAction SilentlyContinue
			# ── inline Start-TrackedJob for retry (see note about PS7 crash) ──
			$ep = $t.ExecutionParameters
			New-Item -ItemType Directory -Path $ep.OutputDir -Force | Out-Null
			$job = Start-Job -Name $ep.JobName -ScriptBlock $_jobScriptBlock `
				-ArgumentList $ep.Command, $ep.Arguments, $ep.WorkingDir, $ep.LogPath
			$t.JobId          = $job.Id
			$t.Status         = 'Running'
			$t.LastFileSizes  = @{}
			$t.LastChangeTime = [DateTime]::UtcNow
			foreach ($f in $t.MonitorFiles) { $t.LastFileSizes[$f] = 0L }
			Write-Log "[$($t.Label)] Retry started job $($job.Id)"
		}

		foreach ($r in $toRemove) {
			$running.Remove($r) | Out-Null
			$finished.Add($r)
		}
	}
	catch {
		Write-Log "Loop error (iter $loopIteration): $_ | $($_.Exception.GetType().FullName)"
	}

	# log every iteration; console progress every iteration (REQUIRED:
	# PowerShell 7 silently kills the host process when a child-script
	# while loop produces no Write-Host output for ~8+ seconds).
	$qc = $queue.Count; $rc = $running.Count; $fc = $finished.Count
	Write-Log "queue=$qc running=$rc done=$fc (iter=$loopIteration)"
	$runLabels = ($running | ForEach-Object { $_.Label }) -join ', '
	Write-Host "  [$((Get-Date).ToString('HH:mm:ss'))] queue=$qc  running=$rc  done=$fc  (iter=$loopIteration)  [$runLabels]"
}

# ── results ──────────────────────────────────────────────────────────────

Write-ProgressMessage "All $($finished.Count) jobs finished. Log: $script:_orchestratorLog"

$results = foreach ($t in $finished) { Get-TrackerResult $t }
return $results
