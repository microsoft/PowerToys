---
name: parallel-job-orchestrator
description: Generic parallel job orchestrator for running copilot, claude, or any CLI tool concurrently with queuing, monitoring, retry, and cleanup. Use when asked to run multiple jobs in parallel, batch process PRs or issues with copilot/claude, orchestrate concurrent CLI executions, run parallel reviews, run parallel triage, or execute any batch of shell commands concurrently. ALL skills that need parallel execution MUST use this orchestrator — do NOT use Start-Job, ForEach-Object -Parallel, or Start-Process directly.
license: Complete terms in LICENSE.txt
---

# Parallel Job Orchestrator

The **single, canonical way** to run multiple jobs concurrently in this repository. Every skill that needs to run copilot, claude, or any CLI tool in parallel **MUST** use this orchestrator. Do NOT use `Start-Job`, `ForEach-Object -Parallel`, or `Start-Process` directly — those approaches have known PowerShell 7 crash bugs that took 48 hours to diagnose and fix.

## When to Use This Skill

- Running copilot or claude CLI on multiple PRs/issues simultaneously
- Any batch processing that spawns multiple CLI processes
- Parallel review, triage, fix, or rework workflows
- Any skill that needs concurrent execution with retry and monitoring

## Why This Orchestrator Exists

PowerShell 7 has **silent host-process crash bugs** triggered by:

1. `[CmdletBinding()]`, `[Parameter(Mandatory)]`, `[ValidateSet()]` attributes propagating `ErrorActionPreference='Stop'` through child scopes
2. `Start-Job` called from within functions inside `while` loops — crashes after ~10-15 jobs
3. Accumulated completed `Job` objects consuming runspace resources
4. `ForEach-Object -Parallel` swallowing errors and losing context

This orchestrator avoids all of these by:

- **No advanced-function attributes** on the script itself
- **Inlined** all `Start-Job`/`Stop-Job`/`Remove-Job` calls (never in functions)
- **Immediately** `Receive-Job` + `Remove-Job` on completion
- **`$ErrorActionPreference = 'Continue'`** in the monitoring loop
- **Write-Host on every iteration** (PS7 kills the host if no output for ~8s in child-script loops)

## Quick Start

### Step 1: Build Job Definitions

Each job is a hashtable with this exact structure:

```powershell
$jobDef = @{
    Label               = 'copilot-pr-12345'          # unique human-readable label
    ExecutionParameters = @{
        JobName    = 'copilot-pr-12345'               # PS job name
        Command    = 'copilot'                        # executable to run
        Arguments  = @('-p', 'Review PR #12345', '--yolo')  # argument array
        WorkingDir = 'C:\repo'                        # working directory
        OutputDir  = 'C:\repo\output\copilot\12345'   # output directory (auto-created)
        LogPath    = 'C:\repo\output\copilot\12345\review.log'  # stdout+stderr log
    }
    MonitorFiles = @('C:\repo\output\copilot\12345\review.log')  # files to watch for activity
    CleanupTask  = $null   # optional scriptblock: { param($Tracker) ... }
}
```

### Step 2: Call the Orchestrator

```powershell
# CRITICAL: Set ErrorActionPreference to Continue before calling
$savedEAP = $ErrorActionPreference
$ErrorActionPreference = 'Continue'

$results = & '.github/skills/parallel-job-orchestrator/scripts/Invoke-SimpleJobOrchestrator.ps1' `
    -JobDefinitions $jobDefs `
    -MaxConcurrent 4 `
    -InactivityTimeoutSeconds 60 `
    -MaxRetryCount 3 `
    -PollIntervalSeconds 5 `
    -LogDir 'C:\repo\output'

$ErrorActionPreference = $savedEAP
```

### Step 3: Process Results

The orchestrator returns an array of result objects:

```powershell
$results | Format-Table Label, Status, JobState, ExitCode, RetryCount -AutoSize
```

| Property | Type | Description |
|----------|------|-------------|
| `Label` | string | Job label from definition |
| `JobId` | int | Last PowerShell job ID |
| `Status` | string | `Completed`, `Failed`, `Abandoned` |
| `JobState` | string | PowerShell job state |
| `ExitCode` | int | Process exit code |
| `RetryCount` | int | Number of retries performed |
| `OutputDir` | string | Output directory path |
| `LogPath` | string | Log file path |

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `-JobDefinitions` | hashtable[] | **(required)** | Array of job definition hashtables |
| `-MaxConcurrent` | int | 4 | Maximum simultaneous jobs |
| `-InactivityTimeoutSeconds` | int | 60 | Seconds of zero log-file growth before stale |
| `-MaxRetryCount` | int | 3 | Retry attempts before abandoning |
| `-PollIntervalSeconds` | int | 5 | Health-check interval |
| `-LogDir` | string | `$env:TEMP` | Directory for orchestrator's own log |

## Job Definition Schema

See [references/job-definition-schema.md](./references/job-definition-schema.md) for the complete schema, copilot/claude examples, and the CleanupTask API.

## Critical Rules for Callers

1. **Set `$ErrorActionPreference = 'Continue'`** before calling the orchestrator
2. **Do NOT** wrap the orchestrator call in a `try/catch` that re-throws
3. **Do NOT** use `[CmdletBinding()]` or `[Parameter(Mandatory)]` on your runner script
4. **Do NOT** use `Start-Job`, `ForEach-Object -Parallel`, or `Start-Process` for parallel work — use this orchestrator
5. **Do** use manual validation (`if (-not $param) { Write-Error ...; return }`) instead of parameter attributes

## Scripts

| Script | Purpose |
|--------|---------|
| [Invoke-SimpleJobOrchestrator.ps1](./scripts/Invoke-SimpleJobOrchestrator.ps1) | The orchestrator — the ONLY parallel execution engine |
| [Test-OrchestratorEdgeCases.ps1](./scripts/Test-OrchestratorEdgeCases.ps1) | 28-scenario stress test suite |

## Execution & Monitoring Rules

The orchestrator is a long-running poll loop. The agent calling it MUST:

1. **Never exit early** — monitor the orchestrator log until it prints "All N jobs finished."
2. **For VS Code terminal usage**, launch the parent script as a detached process (`Start-Process -WindowStyle Hidden`) with `Tee-Object` to a log file. VS Code kills idle background terminals after ~60s.
3. **Poll the log every 30–120 seconds** and report concise progress (done/total, running jobs, retries).
4. **On unexpected termination**, check the orchestrator log's last entries, diagnose the failure, and relaunch.
5. **Only report done** after the orchestrator returns results and all downstream processing is complete.

## Post-Execution Review

After using the orchestrator:

1. Check the orchestrator log in `$LogDir/orchestrator-*.log` for errors
2. Verify all expected jobs show `Completed` status in results
3. Check `RetryCount` — high retries may indicate CLI instability
4. Review `Abandoned` jobs — these hit `MaxRetryCount` and need manual attention

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| PS7 crashes silently | Advanced-function attributes on caller | Remove `[CmdletBinding()]`, `[Parameter()]` from runner script |
| PS7 crashes after ~10 jobs | `Start-Job` inside functions in while loops | Already fixed in orchestrator; don't re-introduce functions |
| Jobs stuck as "Running" | `InactivityTimeoutSeconds` too high | Lower timeout or check CLI isn't hanging |
| All jobs `Abandoned` | CLI tool not installed or auth expired | Test CLI manually: `copilot -p "hello" --yolo` |
| Orchestrator itself crashes at iter ~9 | Too many VS Code terminals open | Kill all terminals, restart VS Code, run in single terminal |
