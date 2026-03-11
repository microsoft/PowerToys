# Job Definition Schema

This document defines the exact hashtable structure required by the
`Invoke-SimpleJobOrchestrator.ps1` script. Every skill that needs parallel
execution builds an array of these hashtables and passes them to the
orchestrator.

## Schema

```powershell
@{
    Label               = [string]   # REQUIRED: unique human-readable label (e.g. 'copilot-pr-12345')
    ExecutionParameters = @{
        JobName    = [string]        # REQUIRED: PowerShell background job name
        Command    = [string]        # REQUIRED: executable to run (e.g. 'copilot', 'claude', 'gh')
        Arguments  = [string[]]      # REQUIRED: argument array splatted to Command
        WorkingDir = [string]        # REQUIRED: working directory for the job
        OutputDir  = [string]        # REQUIRED: output directory (auto-created by orchestrator)
        LogPath    = [string]        # REQUIRED: path for stdout+stderr capture
    }
    MonitorFiles = [string[]]        # REQUIRED: files to watch for activity (typically LogPath or a debug log)
    CleanupTask  = [scriptblock]     # OPTIONAL: runs after job finishes or is abandoned
}
```

## Field Details

### Label
A unique string identifying the job in logs and results. Convention:
`{cli-type}-{skill}-{id}` — e.g. `copilot-pr-45601`, `claude-issue-1234`.

### ExecutionParameters

| Field | Description |
|-------|-------------|
| `JobName` | Name for `Start-Job -Name`. Should match `Label`. |
| `Command` | The executable. Must be in `$PATH` or an absolute path. |
| `Arguments` | Array of arguments. Splatted via `@ArgList`. |
| `WorkingDir` | The job sets `Set-Location` to this before running. |
| `OutputDir` | The orchestrator creates this directory automatically. |
| `LogPath` | All stdout+stderr is redirected here via `*> $LogFile`. |

### MonitorFiles
Array of file paths the orchestrator watches for growth. If none of these
files grow for `InactivityTimeoutSeconds`, the job is considered stale and
retried.

**For copilot CLI**: Monitor the `LogPath` (stdout/stderr).
**For claude CLI**: Monitor the debug log (`--debug-file` path) — claude
writes progress there more frequently than to stdout.

### CleanupTask
Optional scriptblock that receives the tracker hashtable as its single
parameter. Runs after the job completes, fails, or is abandoned. Use for
cleaning up large temporary files.

```powershell
CleanupTask = {
    param($Tracker)
    $debugLog = Join-Path $Tracker.ExecutionParameters.OutputDir '_debug.log'
    if (Test-Path $debugLog) { Remove-Item $debugLog -Force }
}
```

## Examples

### Copilot CLI Job

```powershell
@{
    Label               = 'copilot-pr-45601'
    ExecutionParameters = @{
        JobName    = 'copilot-pr-45601'
        Command    = 'copilot'
        Arguments  = @('-p', 'Review PR #45601 in microsoft/PowerToys...', '--yolo')
        WorkingDir = 'C:\s\PowerToys'
        OutputDir  = 'C:\s\PowerToys\output\copilot\45601'
        LogPath    = 'C:\s\PowerToys\output\copilot\45601\_copilot-review.log'
    }
    MonitorFiles = @('C:\s\PowerToys\output\copilot\45601\_copilot-review.log')
    CleanupTask  = $null
}
```

### Claude CLI Job

```powershell
@{
    Label               = 'claude-pr-45601'
    ExecutionParameters = @{
        JobName    = 'claude-pr-45601'
        Command    = 'claude'
        Arguments  = @('-p', 'Review PR #45601 in microsoft/PowerToys...',
                       '--dangerously-skip-permissions',
                       '--debug', 'all', '--debug-file', 'C:\output\claude\45601\_claude-debug.log')
        WorkingDir = 'C:\s\PowerToys'
        OutputDir  = 'C:\s\PowerToys\output\claude\45601'
        LogPath    = 'C:\s\PowerToys\output\claude\45601\_claude-review.log'
    }
    MonitorFiles = @('C:\s\PowerToys\output\claude\45601\_claude-debug.log')
    CleanupTask  = {
        param($Tracker)
        $dbg = Join-Path $Tracker.ExecutionParameters.OutputDir '_claude-debug.log'
        if (Test-Path $dbg) {
            $fi = [System.IO.FileInfo]::new($dbg)
            if ($fi.Length -gt 0) {
                $sizeMB = [math]::Round($fi.Length / 1MB, 1)
                Remove-Item $dbg -Force
                Write-Host "[$($Tracker.Label)] Cleaned debug log (${sizeMB} MB)"
            }
        }
    }
}
```

### Generic Shell Command Job

```powershell
@{
    Label               = 'lint-module-fancyzones'
    ExecutionParameters = @{
        JobName    = 'lint-fancyzones'
        Command    = 'dotnet'
        Arguments  = @('build', '--no-restore', '-warnaserror')
        WorkingDir = 'C:\s\PowerToys\src\modules\fancyzones'
        OutputDir  = 'C:\s\PowerToys\output\lint\fancyzones'
        LogPath    = 'C:\s\PowerToys\output\lint\fancyzones\build.log'
    }
    MonitorFiles = @('C:\s\PowerToys\output\lint\fancyzones\build.log')
    CleanupTask  = $null
}
```

## Caller Template

Every skill that builds job definitions and calls the orchestrator should
follow this pattern:

```powershell
# Build definitions
$jobDefs = @(foreach ($item in $items) {
    @{
        Label               = "myskill-$($item.Id)"
        ExecutionParameters = @{ ... }
        MonitorFiles        = @(...)
        CleanupTask         = $null
    }
})

# Resolve orchestrator path
$orchestratorPath = Join-Path $PSScriptRoot '..\..\parallel-job-orchestrator\scripts\Invoke-SimpleJobOrchestrator.ps1'

# CRITICAL: Lower ErrorActionPreference before calling
$savedEAP = $ErrorActionPreference
$ErrorActionPreference = 'Continue'

$results = & $orchestratorPath `
    -JobDefinitions $jobDefs `
    -MaxConcurrent 4 `
    -LogDir $outputPath

$ErrorActionPreference = $savedEAP

# Process results
$results | Format-Table Label, Status, ExitCode, RetryCount -AutoSize
```
