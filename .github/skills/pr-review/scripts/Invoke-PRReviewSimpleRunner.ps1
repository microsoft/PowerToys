<#
.SYNOPSIS
	Kick off copilot/claude PR-review jobs via the generic job orchestrator.

.DESCRIPTION
	Builds one job definition per CLI type, then delegates to
	Invoke-SimpleJobOrchestrator.ps1 for queuing, monitoring, retry, and cleanup.
#>
# NOTE: Do NOT use [CmdletBinding()], [Parameter()], [ValidateSet()] or any
# attribute here. These make the script "advanced" which propagates
# ErrorActionPreference through PS7's plumbing and can silently kill the
# orchestrator's monitoring loop in a child scope.
param(
	[int[]]$PRNumbers,

	[string[]]$CLITypes = @('copilot', 'claude'),

	[string]$PromptText,

	[string]$OutputRoot = 'Generated Files/simple-runner',

	[int]$MaxConcurrent = 20,

	[int]$InactivityTimeoutSeconds = 120,

	[int]$MaxRetryCount = 3,

	[int]$PollIntervalSeconds = 5,

	[switch]$Wait
)

$ErrorActionPreference = 'Stop'

# Manual validation (replacing [Parameter(Mandatory)] and [ValidateSet()])
if (-not $PRNumbers -or $PRNumbers.Count -eq 0) {
	Write-Error 'Invoke-PRReviewSimpleRunner: -PRNumbers is required.'
	return
}
foreach ($_cli in $CLITypes) {
	if ($_cli -notin 'copilot', 'claude') {
		Write-Error "Invoke-PRReviewSimpleRunner: Invalid CLIType '$_cli'. Must be 'copilot' or 'claude'."
		return
	}
}

# ── resolve paths ────────────────────────────────────────────────────────

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path

# Resolve config directory name (.github or .claude) from script location
$_cfgDir = if ($PSScriptRoot -match '[\\/](\.github|\.claude)[\\/]') { $Matches[1] } else { '.github' }
$outputRootPath = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
	$OutputRoot
}
else {
	Join-Path $repoRoot $OutputRoot
}
New-Item -ItemType Directory -Path $outputRootPath -Force | Out-Null

# ── prompt builder ───────────────────────────────────────────────────────

function New-ReviewPrompt {
	param(
		[int]$PR,
		[string]$ReviewDir,
		[string]$Override
	)

	if (-not [string]::IsNullOrWhiteSpace($Override)) {
		return $Override
	}

	return @"
Review PR #$PR in the microsoft/PowerToys repo. Read/analyze only. Never modify repo code.

Phase 1 - Fetch PR data:
  gh pr view $PR --json number,title,baseRefName,headRefName,baseRefOid,headRefOid,changedFiles,files
  gh api repos/:owner/:repo/pulls/$PR/files?per_page=250

Phase 2 - Execute review steps 01 through 13 IN ORDER. For each step:
  1. Read the step prompt file from $_cfgDir/skills/pr-review/references/ (e.g. 01-functionality.prompt.md)
  2. Analyze the PR changes against that prompt's checklist
  3. Write the output to $ReviewDir/<NN>-<name>.md (e.g. $ReviewDir/01-functionality.md)
  Do each step sequentially. Write the file immediately after completing the step.

  Steps: 01-functionality, 02-compatibility, 03-performance, 04-accessibility, 05-security,
         06-localization, 07-globalization, 08-extensibility, 09-solid-design, 10-repo-patterns,
         11-docs-automation, 12-code-comments, 13-copilot-guidance

Phase 3 - Write overview:
  Generate $ReviewDir/00-OVERVIEW.md summarizing all step results.

IMPORTANT: Do NOT invoke any other agent, skill, or nested copilot/claude process.
           Do all analysis yourself directly.
"@
}

# ── job definition builder ───────────────────────────────────────────────

function New-CliJobDefinition {
	param(
		[Parameter(Mandatory)] [string]$CLIType,
		[Parameter(Mandatory)] [int]$PR,
		[Parameter(Mandatory)] [string]$RootPath,
		[Parameter(Mandatory)] [string]$RepoRoot,
		[string]$PromptOverride
	)

	$prDir      = Join-Path $RootPath "$CLIType/$PR"
	$logPath    = Join-Path $prDir "_$CLIType-review.log"
	$debugPath  = Join-Path $prDir "_$CLIType-debug.log"
	$prompt     = New-ReviewPrompt -PR $PR -ReviewDir ($prDir -replace '\\', '/') -Override $PromptOverride
	$flatPrompt = ($prompt -replace "[\r\n]+", ' ').Trim()

	if ($CLIType -eq 'copilot') {
		$cmd  = 'copilot'
		$cliArgs = @('-p', $flatPrompt, '--yolo', '--agent', 'ReviewPR')
		$monitorFiles = @($logPath)
		$cleanupTask  = $null
	}
	else {
		$cmd  = 'claude'
		$cliArgs = @('-p', $flatPrompt, '--dangerously-skip-permissions', '--agent', 'ReviewPR',
			'--debug', 'all', '--debug-file', $debugPath)
		$monitorFiles = @($debugPath)
		$cleanupTask  = {
			param($Tracker)
			$outDir = $Tracker.ExecutionParameters.OutputDir
			$dbg = Join-Path $outDir '_claude-debug.log'
			if (Test-Path $dbg) {
				$fi = [System.IO.FileInfo]::new($dbg)
				if ($fi.Length -gt 0) {
					$sizeMB = [math]::Round($fi.Length / 1MB, 1)
					Remove-Item $dbg -Force
					Write-Host "[$($Tracker.Label)] Cleaned debug log (${sizeMB} MB)"
				}
			}
			# Claude CLI auto-creates a 0-byte 'latest' marker file — remove it.
			$latest = Join-Path $outDir 'latest'
			if (Test-Path $latest) { Remove-Item $latest -Force }
		}
	}

	return @{
		Label               = "$CLIType-pr-$PR"
		ExecutionParameters = @{
			JobName    = "$CLIType-pr-$PR"
			Command    = $cmd
			Arguments  = $cliArgs
			WorkingDir = $RepoRoot
			OutputDir  = $prDir
			LogPath    = $logPath
		}
		MonitorFiles = $monitorFiles
		CleanupTask  = $cleanupTask
	}
}

# ── build definitions ────────────────────────────────────────────────────

$jobDefs = @(foreach ($pr in $PRNumbers) {
	foreach ($cli in ($CLITypes | Select-Object -Unique)) {
		New-CliJobDefinition -CLIType $cli -PR $pr `
			-RootPath $outputRootPath -RepoRoot $repoRoot `
			-PromptOverride $PromptText
	}
})

Write-Host "Built $($jobDefs.Count) job definition(s):"
$jobDefs | ForEach-Object { Write-Host "  $($_.Label)" }

if (-not $Wait) {
	Write-Host "`nDefinitions ready. Use -Wait to run them."
	$jobDefs | ForEach-Object {
		[PSCustomObject]@{
			Label       = $_.Label
			Command     = $_.ExecutionParameters.Command
			OutputDir   = $_.ExecutionParameters.WorkingDir
			LogPath     = $_.ExecutionParameters.LogPath
			MonitorFiles = $_.MonitorFiles -join '; '
		}
	} | Format-Table -AutoSize
	return
}

# ── run orchestrator ─────────────────────────────────────────────────────

$orchestratorPath = Join-Path $PSScriptRoot '..\..\parallel-job-orchestrator\scripts\Invoke-SimpleJobOrchestrator.ps1'

# The orchestrator must run under 'Continue' so its monitoring loop survives
# transient errors. Temporarily lower the preference for the child scope.
# CRITICAL: The 2>&1 redirect prevents error-stream items from propagating
# to this scope (where EAP might be 'Stop'), which would silently kill the
# orchestrator's long-running monitoring loop.
$savedEAP = $ErrorActionPreference
$ErrorActionPreference = 'Continue'

$results = & $orchestratorPath `
	-JobDefinitions $jobDefs `
	-MaxConcurrent $MaxConcurrent `
	-InactivityTimeoutSeconds $InactivityTimeoutSeconds `
	-MaxRetryCount $MaxRetryCount `
	-PollIntervalSeconds $PollIntervalSeconds `
	-LogDir $outputRootPath

$ErrorActionPreference = $savedEAP

# ── display results ──────────────────────────────────────────────────────

""
"Job results:"
$results | Format-Table Label, JobId, Status, JobState, ExitCode, RetryCount, LogPath -AutoSize

""
"Output files:"
foreach ($r in $results) {
	""
	"[$($r.Label)] $($r.OutputDir)"
	if (Test-Path $r.OutputDir) {
		Get-ChildItem $r.OutputDir -File |
			Select-Object Name, Length, LastWriteTime |
			Sort-Object Name |
			Format-Table -AutoSize
	}
	else {
		'  (missing directory)'
	}
}
