# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Runs one script in the guest's logged-on standard-user desktop and copies its output folder back.

.DESCRIPTION
PowerShell Direct lands in session 0, which has no desktop, no display configuration, and no input
queue for SendInput. This helper copies the script in with Copy-VMFile, registers a one-shot limited
interactive scheduled task for the standard user under pwsh.exe, waits for it, and copies the guest
output folder back over the PowerShell Direct session. It registers and polls the task with
Start-InteractiveTask and Get-InteractiveTaskInfo from LocalVmGuest.psm1, as Invoke-LocalVmUiTest.ps1
does.

Before the script, the task runs Enable-PerMonitorDpiAwareness.ps1 on the same thread, so every
script reads and writes physical pixels, upstream Capture-MonitorLayout.ps1 included. The task fails
when Windows refuses per-monitor awareness, unless DpiReportOnly is set.

The task starts pwsh through 'conhost.exe --headless', which asks for a console with no window. A
Windows 11 guest hands every new console window to Windows Terminal, and -WindowStyle Hidden only
hides a window after it exists. The task still runs in the standard user's interactive
session and desktop, which display and input calls need. Because conhost's own exit code does not
carry the script's, the wrapped command writes the script's exit code to out\<script>.exitcode, and
that file decides success.

The guest output folder is emptied before each run, so only this run's files come back. Runs that
wait must therefore not overlap; a NoWait run keeps its files in diag and may run beside them. A
task that does not start within 30 seconds, disappears, overruns its limit, cannot be polled,
writes no exit code, or reports a non-zero one fails the call. Whatever the outcome, before the
call returns the task is stopped if it may still run, the run's processes that are still attached
to it are ended (Task Scheduler ends only the console host, and the pwsh under it would keep the
log open), the output is copied back, and the task is unregistered. A failure in one of those
steps is added to the error, never put in place of it. A process the run started and detached,
or one started while the cleanup reads the process list, can survive. An output folder that a
process still holds open is refused, not emptied.

.PARAMETER VmName
Name of the Hyper-V virtual machine.

.PARAMETER ScriptPath
Host path of the script to run in the guest.

.PARAMETER ScriptArguments
Arguments for the script, one element per token. An element of the form -Name passes that
parameter name. Every other element passes one value as written, as a single-quoted literal, so
spaces, quotes, and $ in a value need no escaping. Pass '-Preset', 'Offset', not '-Preset Offset'.

.PARAMETER HostOutputRoot
Host folder that receives the guest output folder's contents.

.PARAMETER CredentialPath
DPAPI credential file for the guest administrator, as Initialize-LocalVmHost.ps1 writes it.

.PARAMETER StandardUser
Interactive user that owns the desktop session.

.PARAMETER GuestRoot
Guest folder the script is copied to. Its out subfolder is the output folder. Defaults to the work
root Initialize-LocalVmMultimon.ps1 uses.

.PARAMETER TimeoutSeconds
Upper bound on the guest task.

.PARAMETER ShowConsole
Starts pwsh directly instead of through a headless console, so its window is visible in the guest.
For watching a script that hangs.

.PARAMETER RunName
Names the task, the log, and the exit-code file. Defaults to the script's base name, which must then
hold only letters, digits, and '-', because the task cmdlets read a task name as a wildcard pattern.
Two runs of one script at the same time need different names.

.PARAMETER DpiReportOnly
Runs the script even when Windows refuses per-monitor DPI awareness. For diagnostics such as
Get-SessionWindows.ps1, which record the awareness they got instead of refusing.

.PARAMETER NoWait
Registers and starts the task, then returns without waiting, copying output, or unregistering it.
Its log and exit code go to the diag folder beside out, which later runs do not empty, so a
long-running watcher survives the steps that follow it. The caller collects and unregisters it
through the returned Task, ExitCodePath, and LogPath.

.EXAMPLE
& ./Invoke-LocalVmInteractive.ps1 -VmName PowerToysUiTest-Win11 -ScriptPath ..\templates\multimon\Save-VirtualScreenshot.ps1 -ScriptArguments '-OutputPath', 'C:\PowerToysUiTestRun\Multimon\out\desktop.png' -HostOutputRoot X:\PowerToysUiTestVm\evidence\desktop
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$VmName,
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$ScriptPath,
    [string[]]$ScriptArguments = @(),
    [Parameter(Mandatory)]
    [string]$HostOutputRoot,
    [ValidateNotNullOrEmpty()]
    [string]$CredentialPath = (Join-Path $env:LOCALAPPDATA 'PowerToysUiTestVm\admin.credential.xml'),
    [ValidateNotNullOrEmpty()]
    [string]$StandardUser = 'PTUser',
    [string]$GuestRoot,
    [ValidateRange(5, 3600)]
    [int]$TimeoutSeconds = 300,
    [switch]$ShowConsole,
    [ValidatePattern('^[A-Za-z0-9-]*$')]
    [string]$RunName = '',
    [switch]$NoWait,
    [switch]$DpiReportOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module -Name (Join-Path $PSScriptRoot 'LocalVmGuest.psm1') -Force
Import-Module -Name (Join-Path $PSScriptRoot 'LocalVmMultimon.psm1') -Force
if (-not (Test-HyperVAccess)) {
    throw (Get-HyperVAccessMessage)
}

$scriptName = Split-Path -Path $ScriptPath -Leaf
$scriptBaseName = [string]::IsNullOrEmpty($RunName) ? [System.IO.Path]::GetFileNameWithoutExtension($scriptName) : $RunName
# The task cmdlets read -TaskName as a wildcard pattern, so a name holding [ or ] registers a task
# that none of them finds again.
if ($scriptBaseName -notmatch '^[A-Za-z0-9-]+$') {
    throw "The run name '$scriptBaseName' from $scriptName may hold only letters, digits, and '-'. Pass -RunName with such a name."
}

# Guest paths come from the module as text: Join-Path would need each drive to exist on the host.
$guestPaths = Get-MultimonGuestPath -Root $GuestRoot
$GuestRoot = $guestPaths.Root
$guestOutput = $guestPaths.Output
$run = Get-MultimonRun -Root $GuestRoot -RunName $scriptBaseName -NoWait:$NoWait
$taskName = $run.Task
$guestExitCodePath = $run.ExitCodePath
$guestLogPath = $run.LogPath
$dpiScript = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\templates\multimon\Enable-PerMonitorDpiAwareness.ps1'))
$guestDpiScript = "$GuestRoot\$(Split-Path -Path $dpiScript -Leaf)"

# Values for -ArgumentList are bound to variables first: in argument mode a cast such as
# [bool]$ShowConsole is not an expression, so it would arrive as the string '[bool]False'.
$visibleConsole = $ShowConsole.IsPresent
$clearOutput = -not $NoWait.IsPresent
$dpiReport = $DpiReportOnly.IsPresent

# The arguments become PowerShell source inside the guest command, so every value is a single-quoted
# literal and only bare parameter names stay unquoted.
$argumentText = @($ScriptArguments | ForEach-Object {
        if ($_ -match '^-[A-Za-z][A-Za-z0-9]*\z') {
            $_
        }
        elseif ($_ -match '^-[A-Za-z]' -and $_ -match '\s') {
            throw "ScriptArguments element '$_' holds a parameter name and a value. Pass each as its own element, for example '-Preset', 'Offset'."
        }
        else {
            "'$([System.Management.Automation.Language.CodeGeneration]::EscapeSingleQuotedStringContent($_))'"
        }
    }) -join ' '

$credential = Import-Clixml -LiteralPath $CredentialPath
# The awareness script goes in with every run, so a guest the initialization never reached still runs it.
foreach ($file in $ScriptPath, $dpiScript) {
    Copy-VMFile -Name $VmName -SourcePath (Resolve-Path -LiteralPath $file).ProviderPath `
        -DestinationPath "$GuestRoot\$(Split-Path -Path $file -Leaf)" -FileSource Host -CreateFullPath -Force
}

$session = New-PSSession -VMName $VmName -Credential $credential
try {
    $launchArguments = @(
        $GuestRoot, $guestOutput, $scriptName, $argumentText, $guestDpiScript, $guestExitCodePath, $guestLogPath,
        $visibleConsole, $clearOutput, $dpiReport, (Get-GuestPowerShellPath)
    )
    # Runs under the guest's inbox Windows PowerShell 5.1.
    $launch = Invoke-Command -Session $session -ArgumentList $launchArguments -ScriptBlock {
        param($Root, $Output, $Script, $Arguments, $DpiScript, $ExitCodePath, $LogPath, $Visible, $ClearOutput, $DpiReport, $Pwsh)
        $ErrorActionPreference = 'Stop'
        # A string here means a caller passed a cast in argument mode; any non-empty string counts as true
        # and would silently pick the visible launch.
        if ($Visible -isnot [bool] -or $ClearOutput -isnot [bool] -or $DpiReport -isnot [bool]) {
            throw "Visible, ClearOutput, and DpiReport must arrive as Boolean values; they arrived as '$Visible', '$ClearOutput', and '$DpiReport'."
        }
        $dpiSwitch = ''
        if ($DpiReport) {
            $dpiSwitch = ' -ReportOnly'
        }
        New-Item -ItemType Directory -Path $Output, (Split-Path -Path $LogPath -Parent) -Force | Out-Null
        # A file still open here belongs to a process this run cannot tell apart from a concurrent run's,
        # so the folder is refused rather than its holder ended.
        if ($ClearOutput) {
            try {
                Get-ChildItem -LiteralPath $Output -Force | Remove-Item -Recurse -Force
            }
            catch {
                throw "The guest output folder $Output could not be emptied: $($_.Exception.Message) A process still holds a file there: either a run still in progress, since runs that wait share this folder and must not overlap, or a guest script from an earlier run that outlived its task. Wait for the other run to finish; otherwise end the guest pwsh that holds the file, or restart the guest with Stop-LocalVm.ps1 and Start-LocalVm.ps1 -Wait. Then rerun."
            }
        }
        Remove-Item -LiteralPath $LogPath, $ExitCodePath -Force -ErrorAction SilentlyContinue
        # Every stream goes to <run>.log, which is copied back with the evidence, so a failing script is
        # diagnosable from the host. The exit code goes to <run>.exitcode, because the headless console
        # host's own exit code does not carry it. The command is encoded because the arguments carry
        # quotes. Every path enters it as a single-quoted literal, so a quote in a path cannot end it.
        # DPI awareness belongs to the thread, so the awareness script runs first in the same command,
        # on the thread the script runs on, before anything caches display metrics.
        $codeGeneration = [System.Management.Automation.Language.CodeGeneration]
        $scriptLiteral = "'" + $codeGeneration::EscapeSingleQuotedStringContent("$Root\$Script") + "'"
        $dpiLiteral = "'" + $codeGeneration::EscapeSingleQuotedStringContent($DpiScript) + "'"
        $log = "'" + $codeGeneration::EscapeSingleQuotedStringContent($LogPath) + "'"
        $exitCodeLiteral = "'" + $codeGeneration::EscapeSingleQuotedStringContent($ExitCodePath) + "'"
        $command = @"
`$ErrorActionPreference = 'Stop'
`$code = 1
try {
    `$global:LASTEXITCODE = 0
    `$null = & $dpiLiteral$dpiSwitch
    & $scriptLiteral $Arguments *> $log
    `$code = if (`$LASTEXITCODE) { `$LASTEXITCODE } else { 0 }
}
catch {
    `$_ | Out-String | Add-Content -LiteralPath $log
    `$_.ScriptStackTrace | Add-Content -LiteralPath $log
}
finally {
    Set-Content -LiteralPath $exitCodeLiteral -Value `$code -Encoding ascii
}
exit `$code
"@
        $encoded = [System.Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($command))
        $pwshArguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand $encoded"
        if ($Visible) {
            [pscustomobject]@{ Executable = $Pwsh; Arguments = $pwshArguments }
        }
        else {
            [pscustomobject]@{
                Executable = Join-Path $env:SystemRoot 'System32\conhost.exe'
                Arguments  = "--headless `"$Pwsh`" $pwshArguments"
            }
        }
    }
    # From here a registered task may exist, so every outcome but a successful NoWait start reaches the
    # cleanup in finally: stop the task if it may still run, end the run's processes, copy the output
    # back, and unregister the task.
    # Each step runs on its own and adds its failure to the call's error rather than replacing it.
    $failure = $null
    $taskState = $null
    # The exit-code file is written last, so its presence also ends the wait if the console host
    # outlives the script.
    $exitCode = $null
    $cleanupFailures = [System.Collections.Generic.List[string]]::new()
    $cleanUp = $true
    try {
        try {
            $null = Start-InteractiveTask -Session $session -TaskName $taskName -Executable $launch.Executable `
                -Arguments $launch.Arguments -UserName $StandardUser -ExecutionTimeLimitSeconds $TimeoutSeconds
        }
        catch {
            $failure = "Starting guest task $taskName failed: $($_.Exception.Message)"
        }

        if ($null -eq $failure -and $NoWait) {
            $cleanUp = $false
            return $run
        }

        if ($null -eq $failure) {
            # The task is registered fresh each run, so a recorded last run means this run started. A
            # task that never starts (no interactive session for the user) reads Ready, the same as a
            # finished one.
            $launchDeadline = (Get-Date).AddSeconds(30)
            $deadline = (Get-Date).AddSeconds($TimeoutSeconds + 15)
            $started = $false
            try {
                do {
                    Start-Sleep -Seconds 2
                    $taskState = Get-InteractiveTaskInfo -Session $session -TaskName $taskName -ExitCodePath $guestExitCodePath
                    $exitCode = $taskState.ExitCode
                    if ($taskState.State -eq 'Missing') {
                        $failure = "Guest task $taskName disappeared while it was polled. A run with the same run name replaces the task; runs that wait must not overlap, because they also share the guest output folder."
                        break
                    }
                    $started = $started -or ($taskState.State -in 'Running', 'Queued') -or -not [string]::IsNullOrEmpty($taskState.LastRunTimeUtc)
                    if (-not $started -and (Get-Date) -ge $launchDeadline) {
                        $failure = "Guest task $taskName did not start within 30 s (State=$($taskState.State)). Check that $StandardUser is logged on at the console."
                        break
                    }
                    $running = -not $started -or $taskState.State -in 'Running', 'Queued'
                } while ([string]::IsNullOrEmpty($exitCode) -and $running -and (Get-Date) -lt $deadline)
            }
            catch {
                $failure = "Reading the state of guest task $taskName failed: $($_.Exception.Message)"
            }
            if ($null -eq $failure -and $taskState.State -in 'Running', 'Queued' -and [string]::IsNullOrEmpty($exitCode)) {
                $failure = "Guest task $taskName exceeded $TimeoutSeconds s."
            }
        }
    }
    finally {
        if ($cleanUp) {
            # Stopping is skipped only for a task known to have ended; stopping a task that is not running
            # is harmless. Unregistering alone leaves a running instance alive.
            if ($null -ne $failure -or $null -eq $taskState -or $taskState.State -in 'Running', 'Queued') {
                try {
                    Invoke-Command -Session $session -ArgumentList $taskName -ScriptBlock {
                        param($Name)
                        Stop-ScheduledTask -TaskName $Name -ErrorAction SilentlyContinue
                    }
                }
                catch {
                    $cleanupFailures.Add("stopping the task failed: $($_.Exception.Message)")
                }
            }
            # Task Scheduler ends only the console host when it stops the task or the time limit ends
            # it, and the pwsh under it keeps the log open, which blocks the copy and the next run.
            try {
                $tree = Stop-MultimonRunProcess -Session $session -LogPath $guestLogPath
                foreach ($killFailure in @($tree.Failures)) {
                    $cleanupFailures.Add("ending a process of the run failed: $killFailure")
                }
                if ($tree.LogLocked) {
                    $holder = ($tree.Matched -gt 0) ? "after the run's $($tree.Ended) of $($tree.Matched) attached processes were ended" : 'by a process the run did not start'
                    $cleanupFailures.Add("$guestLogPath is still open for writing $holder")
                }
                if (-not [string]::IsNullOrEmpty($tree.LogError)) {
                    $cleanupFailures.Add("checking $guestLogPath failed: $($tree.LogError)")
                }
            }
            catch {
                $cleanupFailures.Add("ending the run's processes failed: $($_.Exception.Message)")
            }
            if (-not $NoWait) {
                try {
                    New-Item -ItemType Directory -Path $HostOutputRoot -Force | Out-Null
                    $hasOutput = Invoke-Command -Session $session -ArgumentList $guestOutput -ScriptBlock {
                        param($Output)
                        @(Get-ChildItem -LiteralPath $Output -Force).Count -gt 0
                    }
                    if ($hasOutput) {
                        Copy-Item -Path "$guestOutput\*" -Destination $HostOutputRoot -FromSession $session -Recurse -Force
                    }
                }
                catch {
                    $cleanupFailures.Add("copying the guest output back failed: $($_.Exception.Message)")
                }
            }
            try {
                # A task that is already gone is not an error here; one that is still registered is.
                $removed = Invoke-Command -Session $session -ArgumentList $taskName -ScriptBlock {
                    param($Name)
                    Unregister-ScheduledTask -TaskName $Name -Confirm:$false -ErrorAction SilentlyContinue
                    $null -eq (Get-ScheduledTask -TaskName $Name -ErrorAction SilentlyContinue)
                }
                if ($removed -ne $true) {
                    $cleanupFailures.Add("the task is still registered; remove it in the guest with Unregister-ScheduledTask -TaskName $taskName")
                }
            }
            catch {
                $cleanupFailures.Add("unregistering the task failed: $($_.Exception.Message)")
            }
        }
    }

    # Exception messages usually end with a period, which the joined sentence supplies once.
    $cleanupText = ($cleanupFailures | ForEach-Object { $_.TrimEnd('.') }) -join '; '
    $cleanupNote = ($cleanupFailures.Count -gt 0) ? " Cleanup also failed: $cleanupText." : ''
    if ($null -ne $failure) {
        # A NoWait run copies nothing back.
        $evidenceNote = $NoWait ? '' : " Evidence, if any, is in $HostOutputRoot."
        throw "$failure$cleanupNote$evidenceNote"
    }
    if ([string]::IsNullOrEmpty($exitCode)) {
        throw "Guest task $taskName ended without writing $guestExitCodePath (task result $($taskState.LastTaskResult)).$cleanupNote Evidence, if any, is in $HostOutputRoot."
    }
    if ($exitCode -ne '0') {
        throw "Guest script $scriptName exited with $exitCode.$cleanupNote Evidence, including $scriptBaseName.log, is in $HostOutputRoot."
    }
    if ($cleanupFailures.Count -gt 0) {
        throw "Guest script $scriptName exited with 0, but cleanup failed: $cleanupText. Evidence, if any, is in $HostOutputRoot."
    }

    [pscustomobject]@{
        Task           = $taskName
        ExitCode       = [int]$exitCode
        LastTaskResult = $taskState.LastTaskResult
        HostOutputRoot = $HostOutputRoot
    }
}
finally {
    Remove-PSSession -Session $session
}
