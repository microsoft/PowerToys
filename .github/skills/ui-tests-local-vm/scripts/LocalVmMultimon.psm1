# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Names and settings the multi-monitor layer shares between its host scripts.

.DESCRIPTION
Build-IddSampleDriver.ps1, Initialize-LocalVmMultimon.ps1, and Invoke-LocalVmInteractive.ps1 read
the Multimon configuration block, the driver package, the layout presets, the guest folders, and
the guest task names from here, so each has one definition.
#>

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$script:DefaultGuestRoot = 'C:\PowerToysUiTestRun\Multimon'
$script:TaskPrefix = 'PowerToysUiTest-Multimon-'
$script:DriverPackageName = 'IddSampleDriver'
$script:LayoutScript = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\templates\multimon\Set-MonitorLayout.ps1'))

function Get-MultimonSetting {
    <#
    .SYNOPSIS
    Returns a Multimon setting: the override when given, else the configuration value, else the default.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][hashtable]$Configuration,
        [Parameter(Mandatory)][string]$Name,
        [AllowEmptyString()][string]$Override,
        [AllowEmptyString()][string]$Default
    )

    if (-not [string]::IsNullOrWhiteSpace($Override)) {
        return $Override
    }
    $multimon = $Configuration.ContainsKey('Multimon') ? $Configuration.Multimon : @{}
    if ($multimon.ContainsKey($Name) -and -not [string]::IsNullOrWhiteSpace([string]$multimon[$Name])) {
        return [string]$multimon[$Name]
    }
    return $Default
}

function Get-MultimonPlatform {
    <#
    .SYNOPSIS
    Returns the build platform, x64 or ARM64, that matches the configured guest architecture.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][hashtable]$Configuration)

    if ([string]$Configuration.ProcessorArchitecture -eq 'arm64') {
        return 'ARM64'
    }
    return 'x64'
}

function Get-MultimonDriverPackage {
    <#
    .SYNOPSIS
    Describes the IndirectDisplay sample's driver package: its name, its files, and its host folder.

    .DESCRIPTION
    The folder is the override when given, else Multimon.DriverPath, else multimon\IddSampleDriver
    under the VM root. Build-IddSampleDriver.ps1 writes there and Initialize-LocalVmMultimon.ps1
    reads from there.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][hashtable]$Configuration,
        [Parameter(Mandatory)][string]$VmRoot,
        [AllowEmptyString()][string]$Override
    )

    $name = $script:DriverPackageName
    [pscustomobject]@{
        Name  = $name
        Files = @('inf', 'cat', 'dll' | ForEach-Object { "$name.$_" })
        Path  = Get-MultimonSetting -Configuration $Configuration -Name 'DriverPath' -Override $Override `
            -Default (Join-Path $VmRoot "multimon\$name")
    }
}

function Get-MultimonPreset {
    <#
    .SYNOPSIS
    Returns the layout presets Set-MonitorLayout.ps1 accepts, read from its Preset parameter.
    #>
    [CmdletBinding()]
    param()

    $parameter = (Get-Command -Name $script:LayoutScript).Parameters['Preset']
    $validateSet = $parameter.Attributes | Where-Object { $_ -is [System.Management.Automation.ValidateSetAttribute] }
    return @($validateSet.ValidValues)
}

function Get-MultimonGuestPath {
    <#
    .SYNOPSIS
    Returns the guest work root and its out and diag folders.

    .DESCRIPTION
    Out receives each interactive run's output and is emptied before the run. Diag receives the
    log and exit code of a run started without waiting, and only its owner empties it.
    #>
    [CmdletBinding()]
    param([AllowEmptyString()][string]$Root)

    if ([string]::IsNullOrWhiteSpace($Root)) {
        $Root = $script:DefaultGuestRoot
    }
    # Guest paths, so they are joined as text: Join-Path would need the drive to exist on the host.
    $Root = $Root.TrimEnd('\')
    [pscustomobject]@{
        Root   = $Root
        Output = "$Root\out"
        Diag   = "$Root\diag"
    }
}

function Get-MultimonTaskName {
    <#
    .SYNOPSIS
    Returns the guest scheduled-task name for a run name. A RunName of '*' gives the pattern for all of them.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$RunName)

    return "$script:TaskPrefix$RunName"
}

function Get-MultimonRun {
    <#
    .SYNOPSIS
    Returns an interactive run's task name and the guest paths of its exit code and log.

    .DESCRIPTION
    A run started with NoWait keeps its files in the diag folder; any other run keeps them in out.
    Invoke-LocalVmInteractive.ps1 returns this object for a NoWait run, and a caller that never got
    that object rebuilds it here.
    #>
    [CmdletBinding()]
    param(
        [AllowEmptyString()][string]$Root,
        [Parameter(Mandatory)][string]$RunName,
        [switch]$NoWait
    )

    $paths = Get-MultimonGuestPath -Root $Root
    $folder = $NoWait ? $paths.Diag : $paths.Output
    [pscustomobject]@{
        Task         = Get-MultimonTaskName -RunName $RunName
        ExitCodePath = "$folder\$RunName.exitcode"
        LogPath      = "$folder\$RunName.log"
    }
}

function Stop-MultimonRunProcess {
    <#
    .SYNOPSIS
    Ends the guest processes of one interactive run and reports whether its log is still open for
    writing.

    .DESCRIPTION
    Task Scheduler ends only a task's own process when it stops the task or its time limit ends it.
    For a run started through conhost --headless that is the console host, and the pwsh under it
    keeps running with the run's log open. This function ends the processes that still belong to
    the run, so the log can be copied and the next run can empty its folder.

    A process belongs to the run when its -EncodedCommand decodes to a command naming LogPath as a
    single-quoted literal, as Invoke-LocalVmInteractive.ps1 writes it, or when it descends from such
    a process. That covers the console host and pwsh. A descendant whose parent already exited, or
    one started after the process list was read, is not found.

    Each process is opened by ID and its start time compared with the listed one before it is ended,
    so an ID that a new process reused in between is left alone. The session's own process never
    matches.

    LogLocked means some process still holds the log with write access. A reader such as an
    antivirus scan does not count. LogError carries any other failure to open the log.

    .PARAMETER Session
    PowerShell Direct session to the guest, as the administrator.

    .PARAMETER LogPath
    Guest path of the run's log, as Get-MultimonRun returns it.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Management.Automation.Runspaces.PSSession]$Session,
        [Parameter(Mandatory)][string]$LogPath
    )

    return Invoke-Command -Session $Session -ArgumentList $LogPath -ErrorAction Stop -ScriptBlock {
        param($Log)
        $ErrorActionPreference = 'Stop'
        # A short or partial path would match other runs' commands, so the guest refuses one
        # whatever the host sent.
        if ([string]::IsNullOrEmpty($Log) -or $Log.Length -lt 8 -or -not [System.IO.Path]::IsPathRooted($Log) -or
            -not $Log.EndsWith('.log', [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to end processes for the log path '$Log'. It must be a full path ending in .log."
        }
        $literal = "'" + [System.Management.Automation.Language.CodeGeneration]::EscapeSingleQuotedStringContent($Log) + "'"

        $processes = @(Get-CimInstance -ClassName Win32_Process)
        $pending = New-Object 'System.Collections.Generic.Queue[object]'
        foreach ($process in $processes) {
            if ([int]$process.ProcessId -eq $PID -or $null -eq $process.CommandLine) {
                continue
            }
            $encoded = [regex]::Match([string]$process.CommandLine, '-EncodedCommand\s+([A-Za-z0-9+/=]+)')
            if (-not $encoded.Success) {
                continue
            }
            try {
                $decoded = [System.Text.Encoding]::Unicode.GetString([System.Convert]::FromBase64String($encoded.Groups[1].Value))
            }
            catch {
                continue
            }
            if ($decoded.Contains($literal)) {
                $pending.Enqueue($process)
            }
        }
        $selected = New-Object 'System.Collections.Generic.List[object]'
        $seen = New-Object 'System.Collections.Generic.HashSet[int]'
        while ($pending.Count -gt 0) {
            $parent = $pending.Dequeue()
            if (-not $seen.Add([int]$parent.ProcessId)) {
                continue
            }
            $selected.Add($parent)
            # A process older than the parent it names holds a reused ID, so it is not a child.
            foreach ($child in $processes) {
                if ($child.ParentProcessId -eq $parent.ProcessId -and [int]$child.ProcessId -ne $PID -and
                    $child.CreationDate -ge $parent.CreationDate) {
                    $pending.Enqueue($child)
                }
            }
        }

        $ended = New-Object 'System.Collections.Generic.List[object]'
        $failures = New-Object 'System.Collections.Generic.List[string]'
        foreach ($listed in $selected) {
            $process = Get-Process -Id ([int]$listed.ProcessId) -ErrorAction SilentlyContinue
            if ($null -eq $process) {
                continue
            }
            try {
                # Reading Handle makes the object keep its handle, which pins the ID: the start-time
                # check and the kill then reach the same process.
                $null = $process.Handle
                if ($null -eq $listed.CreationDate -or [Math]::Abs(($process.StartTime - $listed.CreationDate).TotalSeconds) -ge 1) {
                    continue
                }
                $process.Kill()
                $ended.Add($process)
            }
            catch {
                $exited = $true
                try {
                    $exited = $process.HasExited
                }
                catch {
                    $exited = $true
                }
                if (-not $exited) {
                    $failures.Add("PID $($listed.ProcessId): $($_.Exception.Message)")
                }
            }
        }
        $deadline = (Get-Date).AddSeconds(15)
        foreach ($process in $ended) {
            $remaining = [int][Math]::Max(0, ($deadline - (Get-Date)).TotalMilliseconds)
            [void]$process.WaitForExit($remaining)
        }

        # Only a writer blocks the copy, so the check opens for reading and shares reading. A handle
        # can trail the process exit, so a sharing violation is retried for 10 s.
        $locked = $false
        $logError = ''
        if (Test-Path -LiteralPath $Log -PathType Leaf) {
            $retryUntil = (Get-Date).AddSeconds(10)
            do {
                $locked = $false
                try {
                    [System.IO.File]::Open($Log, 'Open', 'Read', 'Read').Dispose()
                }
                catch {
                    $exception = $_.Exception
                    if ($exception -is [System.Management.Automation.MethodInvocationException] -and $null -ne $exception.InnerException) {
                        $exception = $exception.InnerException
                    }
                    # 0x80070020 is ERROR_SHARING_VIOLATION.
                    if ($exception -is [System.IO.IOException] -and $exception.HResult -eq -2147024864) {
                        $locked = $true
                        Start-Sleep -Milliseconds 500
                    }
                    else {
                        $logError = $exception.Message
                    }
                }
            } while ($locked -and (Get-Date) -lt $retryUntil)
        }
        [pscustomobject]@{
            Matched   = $selected.Count
            Ended     = $ended.Count
            Failures  = @($failures)
            LogLocked = $locked
            LogError  = $logError
        }
    }
}

Export-ModuleMember -Function `
    Get-MultimonSetting, Get-MultimonPlatform, Get-MultimonDriverPackage, Get-MultimonPreset, `
    Get-MultimonGuestPath, Get-MultimonTaskName, Get-MultimonRun, Stop-MultimonRunProcess
