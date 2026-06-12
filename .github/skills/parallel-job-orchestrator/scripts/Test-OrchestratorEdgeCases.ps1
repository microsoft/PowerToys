<#
.SYNOPSIS
	Stress-tests Invoke-SimpleJobOrchestrator.ps1 with edge-case scenarios.

.DESCRIPTION
	Creates job definitions that simulate various failure modes:
	  1. Happy-path jobs (should complete normally)
	  2. Jobs that throw exceptions (should be detected as Failed)
	  3. Jobs that hang with no log output (stale → retry → abandon)
	  4. Jobs that write to the log once then hang (stale after initial burst)
	  5. Jobs with a cleanup task (verify cleanup runs on completion)
	  6. Jobs with a cleanup task that itself throws
	  7. Concurrency pressure: many fast jobs queued beyond MaxConcurrent
	  8. Mixed bag: all of the above in one run

	Each scenario prints PASS / FAIL and the script exits with the total
	failure count so CI can gate on it.

.PARAMETER Scenario
	Which scenario to run. Default 'All' runs every scenario sequentially.

.PARAMETER OutputRoot
	Base directory for test artefacts. Cleaned before each scenario.
#>
# NOTE: Do NOT use [CmdletBinding()] or parameter attributes such as
# [ValidateSet()] / [Parameter()] here. Any of those make this an "advanced
# script", which propagates the caller's ErrorActionPreference via the implicit
# -ErrorAction common parameter — silently terminating the entire script when
# stray non-terminating errors bubble up from Stop-Job, Remove-Job, or file
# locks between scenarios.
param(
	[string]$Scenario = 'All',
	[string]$OutputRoot = 'Generated Files/orch-stress-test'
)

# Manual validation instead of [ValidateSet()] to keep this a simple script.
$validScenarios = @('All', 'HappyPath', 'ThrowException', 'StaleNoLog',
	'StaleThenHang', 'CleanupRuns', 'CleanupThrows', 'ConcurrencyPressure', 'MixedBag')
if ($Scenario -notin $validScenarios) {
	Write-Error "Invalid -Scenario '$Scenario'. Valid values: $($validScenarios -join ', ')"
	return
}

# Test scripts use 'Continue' globally. Individual assertions use try/catch.
# Using 'Stop' causes stray non-terminating errors from completed-job cleanup,
# file locks, Start-Job, etc. to silently terminate the whole script.
$ErrorActionPreference = 'Continue'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$orchPath = Join-Path $PSScriptRoot 'Invoke-SimpleJobOrchestrator.ps1'

if (-not [System.IO.Path]::IsPathRooted($OutputRoot)) {
	$OutputRoot = Join-Path $repoRoot $OutputRoot
}

# ── helper: build a single synthetic job definition ──────────────────────

function New-TestJob {
	param(
		[string]$Label,
		[string]$InlineScript,            # PowerShell code to run inside the job
		[string]$OutDir,
		[scriptblock]$CleanupTask = $null
	)

	$logPath = Join-Path $OutDir "$Label.log"

	return @{
		Label               = $Label
		ExecutionParameters = @{
			JobName    = $Label
			Command    = 'powershell'
			Arguments  = @('-NoProfile', '-Command', $InlineScript)
			WorkingDir = $repoRoot
			OutputDir  = $OutDir
			LogPath    = $logPath
		}
		MonitorFiles = @($logPath)
		CleanupTask  = $CleanupTask
	}
}

# ── helper: run one scenario ─────────────────────────────────────────────

$script:passCount = 0
$script:failCount = 0

function Invoke-Scenario {
	param(
		[string]$Name,
		[hashtable[]]$Defs,
		[int]$MaxConcurrent       = 10,
		[int]$InactivityTimeout   = 8,
		[int]$MaxRetry            = 1,
		[int]$PollInterval        = 2,
		[scriptblock]$Assertions        # receives $results array
	)

	Write-Host "`n╔══════════════════════════════════════════════════════╗" -ForegroundColor Yellow
	Write-Host   "║  Scenario: $Name" -ForegroundColor Yellow
	Write-Host   "╚══════════════════════════════════════════════════════╝" -ForegroundColor Yellow

	$scenarioDir = Join-Path $OutputRoot $Name

	# ── aggressive cleanup ───────────────────────────────────────────────
	# Previous stale-job scenarios may leave background processes with file
	# locks. Stop ALL jobs (not just the current scenario's) and wait a
	# moment for handles to release before wiping the directory.
	Get-Job | Stop-Job  -ErrorAction SilentlyContinue
	Get-Job | Remove-Job -Force -ErrorAction SilentlyContinue

	if (Test-Path $scenarioDir) {
		Start-Sleep -Milliseconds 500
		Remove-Item $scenarioDir -Recurse -Force -ErrorAction SilentlyContinue
		# Retry once if first attempt failed (file lock race)
		if (Test-Path $scenarioDir) {
			Start-Sleep -Seconds 1
			Remove-Item $scenarioDir -Recurse -Force -ErrorAction SilentlyContinue
		}
	}

	$results = & $orchPath `
		-JobDefinitions $Defs `
		-MaxConcurrent $MaxConcurrent `
		-InactivityTimeoutSeconds $InactivityTimeout `
		-MaxRetryCount $MaxRetry `
		-PollIntervalSeconds $PollInterval `
		-LogDir $scenarioDir

	# run caller assertions
	try {
		& $Assertions $results
	}
	catch {
		Write-Host "  FAIL (assertion error): $_" -ForegroundColor Red
		$script:failCount++
	}
}

function Assert-True {
	param([bool]$Condition, [string]$Message)
	if ($Condition) {
		Write-Host "  PASS: $Message" -ForegroundColor Green
		$script:passCount++
	}
	else {
		Write-Host "  FAIL: $Message" -ForegroundColor Red
		$script:failCount++
	}
}

# ── scenario definitions ─────────────────────────────────────────────────

$scenarios = @{}

# 1. Happy path — 3 jobs that complete quickly
$scenarios['HappyPath'] = {
	$dir = Join-Path $OutputRoot 'HappyPath'
	$defs = @(1..3 | ForEach-Object {
		New-TestJob -Label "happy-$_" -OutDir $dir `
			-InlineScript "Write-Output 'hello from $_'; Start-Sleep -Milliseconds 500; Write-Output 'done $_'"
	})

	Invoke-Scenario -Name 'HappyPath' -Defs $defs -Assertions {
		param($r)
		Assert-True ($r.Count -eq 3)          'Got 3 results'
		Assert-True (($r | Where-Object Status -eq 'Completed').Count -eq 3) 'All 3 completed'
		Assert-True (($r | Where-Object RetryCount -eq 0).Count -eq 3)      'Zero retries'
	}
}

# 2. Throw exception — the command errors out immediately
$scenarios['ThrowException'] = {
	$dir = Join-Path $OutputRoot 'ThrowException'
	$defs = @(
		(New-TestJob -Label 'throw-1' -OutDir $dir `
			-InlineScript "throw 'Simulated fatal error'"),
		(New-TestJob -Label 'good-1' -OutDir $dir `
			-InlineScript "Write-Output 'I am fine'; Start-Sleep -Milliseconds 300")
	)

	Invoke-Scenario -Name 'ThrowException' -Defs $defs -Assertions {
		param($r)
		Assert-True ($r.Count -eq 2)                                  'Got 2 results'
		Assert-True (($r | Where-Object Label -eq 'throw-1').Status -in 'Completed','Failed') 'Throw job detected as finished'
		Assert-True (($r | Where-Object Label -eq 'good-1').Status -eq 'Completed')            'Good job completed'
	}
}

# 3. Stale — no log output, job sleeps forever (beyond timeout)
$scenarios['StaleNoLog'] = {
	$dir = Join-Path $OutputRoot 'StaleNoLog'
	$defs = @(
		(New-TestJob -Label 'stale-nolog' -OutDir $dir `
			-InlineScript "Start-Sleep -Seconds 120")
	)

	# Timeout 8 s, poll 2 s, max retry 1 → should retry once then abandon
	Invoke-Scenario -Name 'StaleNoLog' -Defs $defs `
		-InactivityTimeout 8 -MaxRetry 1 -PollInterval 2 `
		-Assertions {
		param($r)
		Assert-True ($r.Count -eq 1)                           'Got 1 result'
		Assert-True ($r[0].Status -eq 'Abandoned')             'Marked as Abandoned'
		Assert-True ($r[0].RetryCount -eq 1)                   'Retried once before giving up'
	}
}

# 4. Writes once then hangs — log grows initially then stops
$scenarios['StaleThenHang'] = {
	$dir = Join-Path $OutputRoot 'StaleThenHang'
	$defs = @(
		(New-TestJob -Label 'burst-hang' -OutDir $dir `
			-InlineScript "Write-Output 'initial burst'; Start-Sleep -Seconds 120")
	)

	Invoke-Scenario -Name 'StaleThenHang' -Defs $defs `
		-InactivityTimeout 8 -MaxRetry 1 -PollInterval 2 `
		-Assertions {
		param($r)
		Assert-True ($r.Count -eq 1)                           'Got 1 result'
		Assert-True ($r[0].Status -eq 'Abandoned')             'Marked as Abandoned'
		Assert-True ($r[0].RetryCount -ge 1)                   'Retried at least once'
	}
}

# 5. Cleanup task runs on completion
$scenarios['CleanupRuns'] = {
	$dir = Join-Path $OutputRoot 'CleanupRuns'
	$marker = Join-Path $dir 'cleanup-ran.marker'

	$cleanupBlock = [scriptblock]::Create(
		"param(`$Tracker); New-Item -ItemType File -Path '$($marker -replace "'","''")' -Force | Out-Null"
	)

	$defs = @(
		(New-TestJob -Label 'cleanup-ok' -OutDir $dir `
			-InlineScript "Write-Output 'will be cleaned'" `
			-CleanupTask $cleanupBlock)
	)

	Invoke-Scenario -Name 'CleanupRuns' -Defs $defs -Assertions {
		param($r)
		Assert-True ($r.Count -eq 1)                           'Got 1 result'
		Assert-True ($r[0].Status -eq 'Completed')             'Job completed'
		Assert-True (Test-Path $marker)                        'Cleanup marker file exists'
	}
}

# 6. Cleanup task that itself throws — should not crash the orchestrator
$scenarios['CleanupThrows'] = {
	$dir = Join-Path $OutputRoot 'CleanupThrows'

	$badCleanup = { param($Tracker); throw 'Cleanup explosion!' }

	$defs = @(
		(New-TestJob -Label 'cleanup-boom' -OutDir $dir `
			-InlineScript "Write-Output 'boom prep'" `
			-CleanupTask $badCleanup),
		(New-TestJob -Label 'after-boom' -OutDir $dir `
			-InlineScript "Write-Output 'I should still finish'")
	)

	Invoke-Scenario -Name 'CleanupThrows' -Defs $defs -Assertions {
		param($r)
		Assert-True ($r.Count -eq 2)                                         'Got 2 results'
		Assert-True (($r | Where-Object Label -eq 'cleanup-boom').Status -eq 'Completed') 'Boom job completed despite bad cleanup'
		Assert-True (($r | Where-Object Label -eq 'after-boom').Status -eq 'Completed')   'Next job also completed'
	}
}

# 7. Concurrency pressure — 20 fast jobs, MaxConcurrent=5
$scenarios['ConcurrencyPressure'] = {
	$dir = Join-Path $OutputRoot 'ConcurrencyPressure'
	$defs = @(1..20 | ForEach-Object {
		New-TestJob -Label "conc-$_" -OutDir $dir `
			-InlineScript "Write-Output 'job $_ at $(Get-Date -f s)'; Start-Sleep -Milliseconds $(Get-Random -Min 200 -Max 1500)"
	})

	Invoke-Scenario -Name 'ConcurrencyPressure' -Defs $defs `
		-MaxConcurrent 5 -InactivityTimeout 15 -PollInterval 2 `
		-Assertions {
		param($r)
		Assert-True ($r.Count -eq 20)                                       'Got 20 results'
		Assert-True (($r | Where-Object Status -eq 'Completed').Count -eq 20) 'All 20 completed'
		# Verify logs have content
		$withContent = ($r | Where-Object {
			(Test-Path $_.LogPath) -and (Get-Item $_.LogPath).Length -gt 0
		}).Count
		Assert-True ($withContent -eq 20)                                    'All 20 logs have content'
	}
}

# 8. Mixed bag — happy + throw + stale + cleanup in one run
$scenarios['MixedBag'] = {
	$dir = Join-Path $OutputRoot 'MixedBag'
	$marker = Join-Path $dir 'mixed-cleanup.marker'

	$cleanupOk = [scriptblock]::Create(
		"param(`$Tracker); New-Item -ItemType File -Path '$($marker -replace "'","''")' -Force | Out-Null"
	)

	$defs = @(
		(New-TestJob -Label 'mix-happy'   -OutDir $dir -InlineScript "Write-Output 'happy'; Start-Sleep -Milliseconds 500"),
		(New-TestJob -Label 'mix-throw'   -OutDir $dir -InlineScript "throw 'kaboom'"),
		(New-TestJob -Label 'mix-stale'   -OutDir $dir -InlineScript "Start-Sleep -Seconds 120"),
		(New-TestJob -Label 'mix-cleanup' -OutDir $dir -InlineScript "Write-Output 'with cleanup'" -CleanupTask $cleanupOk)
	)

	Invoke-Scenario -Name 'MixedBag' -Defs $defs `
		-MaxConcurrent 10 -InactivityTimeout 8 -MaxRetry 1 -PollInterval 2 `
		-Assertions {
		param($r)
		Assert-True ($r.Count -eq 4)                                                         'Got 4 results'
		Assert-True (($r | Where-Object Label -eq 'mix-happy').Status -eq 'Completed')       'Happy completed'
		Assert-True (($r | Where-Object Label -eq 'mix-throw').Status -in 'Completed','Failed') 'Throw detected'
		Assert-True (($r | Where-Object Label -eq 'mix-stale').Status -eq 'Abandoned')       'Stale abandoned'
		Assert-True (($r | Where-Object Label -eq 'mix-stale').RetryCount -ge 1)             'Stale retried'
		Assert-True (($r | Where-Object Label -eq 'mix-cleanup').Status -eq 'Completed')     'Cleanup job completed'
		Assert-True (Test-Path $marker)                                                      'Mixed cleanup marker exists'
	}
}

# ── run selected scenarios ───────────────────────────────────────────────

$toRun = if ($Scenario -eq 'All') { $scenarios.Keys | Sort-Object } else { @($Scenario) }

$sw = [System.Diagnostics.Stopwatch]::StartNew()

foreach ($name in $toRun) {
	& $scenarios[$name]

	# ── inter-scenario cleanup ─────────────────────────────────
	# Kill any leftover jobs (especially long-running stale-sim sleeps),
	# force garbage collection, and pause briefly so handles release.
	Get-Job | Stop-Job  -ErrorAction SilentlyContinue
	Get-Job | Remove-Job -Force -ErrorAction SilentlyContinue
	[System.GC]::Collect()
	Start-Sleep -Seconds 2
}

$sw.Stop()

# ── summary ──────────────────────────────────────────────────────────────

Write-Host "`n════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  RESULTS:  $($script:passCount) passed,  $($script:failCount) failed   ($([math]::Round($sw.Elapsed.TotalSeconds, 1))s)" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════════════════════" -ForegroundColor Cyan

# clean up jobs
Get-Job | Remove-Job -Force -ErrorAction SilentlyContinue

exit $script:failCount
