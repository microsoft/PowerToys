# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#requires -Version 7.0

<#
.SYNOPSIS
Captures two bounded, non-invasive native testhost stack traces after 120 seconds without diagnostic log progress.
.DESCRIPTION
Start before native VSTest and Stop in an always() step, using the same LogDirectory.
Start requires a local Microsoft-signed Windows SDK x64 CDB; it does not install anything.
Only native-tests.diag.host.*.log files created after Start can identify a target.
The watcher never terminates a testhost, writes a dump, or changes test results.
Artifacts include ownership, heartbeat/status, file versions/SHA256, and two CDB logs.
Watch and RunId are internal to the owned child. Dot-sourcing only defines functions for tests.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Start', 'Watch', 'Stop')]
    [string]$Action,
    [Parameter(Mandatory = $true)]
    [string]$LogDirectory,
    [string]$BinariesDirectory,
    [string]$DebuggerPath,
    [string]$RunId
)

function Get-NativeLocalPath {
    param([string]$Path)
    if ($Path -notmatch '^[a-zA-Z]:\\' -or $Path -match '[";\r\n]' -or
        ([System.IO.DriveInfo]::new($Path.Substring(0, 3))).DriveType -ne 'Fixed') {
        throw "A local fixed-drive path without quotes or symbol-path separators is required: $Path"
    }
    return [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
}

function Resolve-NativeDebugger {
    param([string]$Path)
    if (-not $Path) {
        foreach ($root in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
            $candidate = Join-Path $root 'Windows Kits\10\Debuggers\x64\cdb.exe'
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                $Path = $candidate
                break
            }
        }
    }
    if (-not $Path) { throw 'Native stack tracing requires an installed Windows SDK Debuggers\x64\cdb.exe; no debugger was found.' }
    $Path = Get-NativeLocalPath $Path
    if ([System.IO.Path]::GetFileName($Path) -ine 'cdb.exe' -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "CDB executable not found: $Path"
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(^|,\s*)O=Microsoft Corporation(,|$)') {
        throw "CDB must have a valid Microsoft Corporation signature: $Path ($($signature.Status))"
    }
    return $Path
}

function Get-NativeFileMetadata {
    param([string]$Path)
    $file = Get-Item -LiteralPath $Path -ErrorAction Stop
    [pscustomobject]@{
        Path = $file.FullName
        FileVersion = $file.VersionInfo.FileVersion
        ProductVersion = $file.VersionInfo.ProductVersion
        SHA256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256 -ErrorAction Stop).Hash
    }
}

function Write-NativeJson {
    param([string]$Path, $Value)
    $next = "$Path.next"
    $Value | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $next -Encoding utf8
    Move-Item -LiteralPath $next -Destination $Path -Force
}

function Open-NativeProcess {
    param([int]$ProcessId)
    $process = [System.Diagnostics.Process]::GetProcessById($ProcessId)
    try {
        # Keep the kernel handle open through validation, attachment, and cleanup; never reopen by name.
        $null = $process.Handle
        return $process
    }
    catch {
        $process.Dispose()
        throw
    }
}

function Get-NativeProcessInfo {
    param([int]$ProcessId)
    Get-CimInstance Win32_Process -Filter "ProcessId = $ProcessId" -Property ProcessId, ParentProcessId, ExecutablePath, CreationDate -ErrorAction Stop
}

function Test-NativeProcessIdentity {
    param($Process, [int]$ProcessId, [datetime]$CreatedUtc)
    return (-not $Process.HasExited -and $Process.Id -eq $ProcessId -and
        $Process.StartTime.ToUniversalTime().Ticks -eq $CreatedUtc.ToUniversalTime().Ticks)
}

function Get-NativeHostIdentity {
    param([string]$Text, [string]$DiagnosticPath, [datetime]$StartedUtc)
    $firstLine = ($Text -split '\r?\n', 2)[0]
    $match = [regex]::Match($firstLine, '^TpTrace \w+: \d+ : (?<pid>\d+), \d+, (?<time>\d{4}/\d{2}/\d{2}, \d{2}:\d{2}:\d{2}\.\d+), \d+, (?<image>testhost(?:\.x86)?\.exe), .*Testhost process started with args.*\[--parentprocessid,\s*(?<parent>\d+)\].*\[--diag,\s*(?<diag>[^\]]+)\]')
    if (-not $match.Success) { return $null }
    $logTime = [datetime]::ParseExact($match.Groups['time'].Value, 'yyyy/MM/dd, HH:mm:ss.FFFFFFF', [cultureinfo]::InvariantCulture).ToUniversalTime()
    if ($logTime -lt $StartedUtc -or
        (Get-NativeLocalPath $match.Groups['diag'].Value) -ine (Get-NativeLocalPath $DiagnosticPath)) { return $null }
    $adapter = [regex]::Match($Text, "Register extension[^\r\n]*'executor://CppUnitTestExecutor/v1'[^\r\n]*inside file '(?<path>[^']+CppUnitTestExtension\.dll)'")
    $sources = [regex]::Matches($Text, "assemblyType:'Native' for source: '(?<path>[^']+\.dll)'")
    if (-not $adapter.Success -or $sources.Count -eq 0) { return $null }
    [pscustomobject]@{
        ProcessId = [int]$match.Groups['pid'].Value
        ParentId = [int]$match.Groups['parent'].Value
        Image = $match.Groups['image'].Value
        LogTimeUtc = $logTime
        Adapter = $adapter.Groups['path'].Value
        Sources = @($sources | ForEach-Object { $_.Groups['path'].Value } | Sort-Object -Unique)
    }
}

function Test-NativeLogIdle {
    param($File, [hashtable]$Samples, [datetime]$StartedUtc, [datetime]$NowUtc)
    if ($File.CreationTimeUtc -lt $StartedUtc -or $File.LastWriteTimeUtc -lt $StartedUtc) { return $false }
    $previous = $Samples[$File.FullName]
    if (-not $previous -or $previous.Length -ne $File.Length -or $previous.Modified -ne $File.LastWriteTimeUtc) {
        $Samples[$File.FullName] = @{ Length = $File.Length; Modified = $File.LastWriteTimeUtc; UnchangedSince = $NowUtc }
        return $false
    }
    return ($NowUtc - $previous.UnchangedSince).TotalSeconds -ge 120
}

function Open-NativeHost {
    param($Identity, [datetime]$StartedUtc)
    $target = $null
    $parent = $null
    try {
        $target = Open-NativeProcess $Identity.ProcessId
        $parent = Open-NativeProcess $Identity.ParentId
        $os = Get-NativeProcessInfo $Identity.ProcessId
        $targetStart = $target.StartTime.ToUniversalTime()
        $parentStart = $parent.StartTime.ToUniversalTime()
        $targetPath = $target.MainModule.FileName
        $parentPath = $parent.MainModule.FileName
        if ($target.HasExited -or $parent.HasExited -or
            $targetStart -lt $StartedUtc -or $parentStart -lt $StartedUtc -or $parentStart -gt $targetStart -or
            $targetStart -gt $Identity.LogTimeUtc -or ($Identity.LogTimeUtc - $targetStart).TotalSeconds -gt 30 -or
            -not $os -or $os.ParentProcessId -ne $Identity.ParentId -or
            [math]::Abs(($os.CreationDate.ToUniversalTime() - $targetStart).TotalMilliseconds) -gt 10 -or
            $os.ExecutablePath -ine $targetPath -or
            [System.IO.Path]::GetFileName($targetPath) -ine $Identity.Image -or
            [System.IO.Path]::GetFileName($parentPath) -ine 'vstest.console.exe') {
            throw 'Diagnostic PID, executable, creation time, or OS parent does not match a current native VSTest host.'
        }
        $installation = Get-NativeLocalPath (Split-Path $parentPath)
        $adapter = Get-NativeLocalPath $Identity.Adapter
        if (-not $adapter.StartsWith("$installation\", [StringComparison]::OrdinalIgnoreCase) -or
            -not $targetPath.StartsWith("$installation\", [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Host and registered CppUnitTestExtension must belong to the actual VSTest installation.'
        }
        $result = [pscustomobject]@{
            Target = $target; Parent = $parent; TargetStartUtc = $targetStart; ParentStartUtc = $parentStart
            Identity = $Identity; Installation = $installation
        }
        $target = $null
        $parent = $null
        return $result
    }
    finally {
        if ($target) { $target.Dispose() }
        if ($parent) { $parent.Dispose() }
    }
}

function Test-NativeHostAlive {
    param($HostRecord)
    return ((Test-NativeProcessIdentity $HostRecord.Target $HostRecord.Identity.ProcessId $HostRecord.TargetStartUtc) -and
        (Test-NativeProcessIdentity $HostRecord.Parent $HostRecord.Identity.ParentId $HostRecord.ParentStartUtc))
}

function Get-NativeCaptureInputs {
    param($HostRecord, [string]$Binaries)
    $modules = @($HostRecord.Target.Modules | ForEach-Object { $_.FileName } | Where-Object {
        $_.StartsWith("$Binaries\", [StringComparison]::OrdinalIgnoreCase) -or
        [System.IO.Path]::GetFileName($_) -match 'CppUnitTest|^(clr|coreclr|vcruntime.*|msvcp.*|ucrtbase|ntdll|kernelbase)\.dll$'
    } | Sort-Object -Unique)
    $paths = @($HostRecord.Target.MainModule.FileName, $HostRecord.Parent.MainModule.FileName, $HostRecord.Identity.Adapter) + $modules
    $metadata = @($paths | Sort-Object -Unique | ForEach-Object { Get-NativeFileMetadata $_ })
    $symbols = @($Binaries, $HostRecord.Installation, (Split-Path $HostRecord.Identity.Adapter)) +
        @($modules | ForEach-Object { Split-Path $_ })
    [pscustomobject]@{
        Files = $metadata
        LoadedRelevantModules = $modules
        NativeSources = $HostRecord.Identity.Sources
        SymbolPath = (($symbols | ForEach-Object { Get-NativeLocalPath $_ } | Sort-Object -Unique) -join ';')
    }
}

function Test-NativeStackOutput {
    param([string]$Text)
    # Exit code zero also occurs on failed attaches. Require actual frames AND the lm module table.
    return ($Text -match '(?m)^\s*(?:[0-9a-f]{2}\s+)?[0-9a-f`]{8,}\s+[0-9a-f`]{8,}\s+\S+[!+]\S+' -and
        $Text -match '(?m)^\s*start\s+end\s+module name\s*$' -and
        $Text -match '(?m)^\s*[0-9a-f`]{8,}\s+[0-9a-f`]{8,}\s+\S+\s+\(' -and
        $Text -notmatch '(?i)Cannot debug pid|Could not attach|Unable to attach|Access is denied|Debuggee initialization failed')
}

function Start-NativeCdbProcess {
    param($StartInfo)
    [System.Diagnostics.Process]::Start($StartInfo)
}

function Invoke-NativeStackCapture {
    param($Target, [string]$Cdb, [string]$SymbolPath, [string]$OutputPath, [string]$StopPath)
    $debugger = $null
    $started = [datetime]::UtcNow
    $status = 'Failed'
    $exitCode = $null
    $debuggerId = $null
    $debuggerStart = $null
    $errorMessage = $null
    $deadline = (Get-Date).ToUniversalTime().AddSeconds(60)
    # SDK CDB 10.0.19041 rejects -pd with non-invasive attach; qd exits without a debug port.
    # Its help misspells -netsyms:no as -netsym:no.
    $arguments = @('-pv', '-pvr', '-p', [string]$Target.Id, '-sins', '-noshell', '-noio', '-netsyms:no',
        '-y', $SymbolPath, '-logo', $OutputPath, '-c', '~* k 24; !locks; lm; qd')
    try {
        if ($Target.HasExited) { $status = 'TargetExited'; return $status }
        $info = [System.Diagnostics.ProcessStartInfo]::new($Cdb)
        $info.UseShellExecute = $false
        $info.CreateNoWindow = $true
        $info.WorkingDirectory = Split-Path $Cdb
        foreach ($argument in $arguments) { $info.ArgumentList.Add($argument) }
        foreach ($name in @('_NT_SYMBOL_PATH', '_NT_ALT_SYMBOL_PATH', '_NT_SOURCE_PATH', '_NT_EXECUTABLE_IMAGE_PATH')) {
            $null = $info.Environment.Remove($name)
        }
        $info.Environment['_NT_DEBUGGER_EXTENSION_PATH'] = Join-Path (Split-Path $Cdb) 'winext'
        # -pvr additionally disables suspension: even a CDB timeout cannot strand suspended test threads.
        $debugger = Start-NativeCdbProcess $info
        $null = $debugger.Handle
        $debuggerId = $debugger.Id
        $debuggerStart = $debugger.StartTime.ToUniversalTime().ToString('o')
        while (-not $debugger.WaitForExit(250)) {
            if (Test-Path -LiteralPath $StopPath) { $status = 'Stopped'; break }
            if ((Get-Date).ToUniversalTime() -ge $deadline) { $status = 'TimedOut'; break }
        }
        if ($debugger.HasExited) {
            $exitCode = $debugger.ExitCode
            $text = if (Test-Path -LiteralPath $OutputPath) { Get-Content -LiteralPath $OutputPath -Raw } else { '' }
            if ($exitCode -eq 0 -and (Test-NativeStackOutput $text)) { $status = 'Captured' }
            elseif ($Target.HasExited) { $status = 'TargetExited' }
        }
    }
    catch {
        $errorMessage = $_.Exception.Message
        throw
    }
    finally {
        if ($debugger) {
            if (-not $debugger.HasExited) {
                $debugger.Kill()
                if (-not $debugger.WaitForExit(5000)) { throw "Owned CDB PID $($debugger.Id) did not exit." }
            }
            $debugger.Dispose()
        }
        Write-NativeJson "$OutputPath.result.json" ([pscustomobject]@{
            StartedUtc = $started.ToString('o'); FinishedUtc = [datetime]::UtcNow.ToString('o')
            TargetId = $Target.Id; Status = $status; ExitCode = $exitCode; Arguments = $arguments
            DebuggerId = $debuggerId; DebuggerStartUtc = $debuggerStart
            TargetStillRunning = -not $Target.HasExited; Error = $errorMessage
        })
    }
    if ($status -eq 'Failed' -or $status -eq 'TimedOut') { throw "CDB capture $status; inspect $OutputPath and its result JSON." }
    return $status
}

function Write-NativeWatchState {
    param([string]$Directory, $Owner, [string]$Status, [string]$Message, [int]$Captures = 0)
    Write-NativeJson (Join-Path $Directory 'state.json') ([pscustomobject]@{
        RunId = $Owner.RunId; WatcherId = $Owner.WatcherId; Status = $Status
        HeartbeatUtc = [datetime]::UtcNow.ToString('o'); Captures = $Captures; Message = $Message
    })
}

function Watch-NativeTestHost {
    param($Owner, [string]$Directory)
    $stop = Join-Path $Directory 'stop.requested'
    $samples = @{}
    $rejected = @{}
    $hostRecord = $null
    $captures = 0
    $started = ([datetime]$Owner.StartedUtc).ToUniversalTime()
    $deadline = [datetime]::UtcNow.AddMinutes(60)
    try {
        $cdb = Resolve-NativeDebugger $Owner.DebuggerPath
        # Verify CIM access before reporting ready, not two minutes into a stalled test.
        $null = Get-NativeProcessInfo $PID
        Write-NativeWatchState $Directory $Owner 'Ready' 'Waiting for current-run native host diagnostics.'
        while ([datetime]::UtcNow -lt $deadline -and -not (Test-Path -LiteralPath $stop)) {
            foreach ($file in @(Get-ChildItem -LiteralPath $Owner.LogDirectory -Filter 'native-tests.diag.host.*.log' -File)) {
                if ($rejected.ContainsKey($file.FullName) -or -not (Test-NativeLogIdle $file $samples $started ([datetime]::UtcNow))) { continue }
                try {
                    if ($file.Length -gt 16MB) { throw 'Diagnostic exceeds the bounded 16 MiB parser limit.' }
                    $identity = Get-NativeHostIdentity (Get-Content -LiteralPath $file.FullName -Raw) $file.FullName $started
                    if (-not $identity) { continue }
                    $hostRecord = Open-NativeHost $identity $started
                }
                catch {
                    $rejected[$file.FullName] = $true
                    Add-Content -LiteralPath (Join-Path $Directory 'rejected-hosts.log') -Value "$([datetime]::UtcNow.ToString('o')) $($file.Name): $($_.Exception.Message)"
                    continue
                }
                $inputs = Get-NativeCaptureInputs $hostRecord $Owner.BinariesDirectory
                Write-NativeJson (Join-Path $Directory 'target.json') ([pscustomobject]@{
                    Diagnostic = $file.FullName; ProcessId = $identity.ProcessId; ParentId = $identity.ParentId
                    TargetStartUtc = $hostRecord.TargetStartUtc.ToString('o'); ParentStartUtc = $hostRecord.ParentStartUtc.ToString('o')
                    Inputs = $inputs
                })
                for ($snapshot = 1; $snapshot -le 2; $snapshot++) {
                    if ((Test-Path -LiteralPath $stop) -or -not (Test-NativeHostAlive $hostRecord)) { break }
                    Write-NativeWatchState $Directory $Owner 'Capturing' "Snapshot $snapshot of PID $($identity.ProcessId)." $captures
                    $output = Join-Path $Directory "snapshot-$snapshot.log"
                    $result = Invoke-NativeStackCapture $hostRecord.Target $cdb $inputs.SymbolPath $output $stop
                    if ($result -in @('Stopped', 'TargetExited')) { break }
                    $captures++
                    Write-NativeWatchState $Directory $Owner 'Waiting' 'Non-invasive capture detached.' $captures
                    if ($snapshot -eq 1) {
                        for ($second = 0; $second -lt 10 -and -not (Test-Path -LiteralPath $stop); $second++) { Start-Sleep -Seconds 1 }
                    }
                }
                Write-NativeWatchState $Directory $Owner 'Completed' 'Capture budget finished, stop requested, or target exited; test result unchanged.' $captures
                return
            }
            Write-NativeWatchState $Directory $Owner 'Ready' 'Waiting for 120 seconds without native host log progress.' $captures
            Start-Sleep -Seconds 5
        }
        $status = if (Test-Path -LiteralPath $stop) { 'Stopped' } else { 'Expired' }
        Write-NativeWatchState $Directory $Owner $status 'Monitor finished without changing test processes or results.' $captures
    }
    catch {
        Write-NativeWatchState $Directory $Owner 'Failed' $_.Exception.Message $captures
        throw
    }
    finally {
        if ($hostRecord) {
            $hostRecord.Target.Dispose()
            $hostRecord.Parent.Dispose()
        }
    }
}

function Start-NativeTestHostTrace {
    param([string]$Logs, [string]$Binaries, [string]$Debugger)
    $started = [datetime]::UtcNow
    $Logs = Get-NativeLocalPath $Logs
    $Binaries = Get-NativeLocalPath $Binaries
    if (-not (Test-Path -LiteralPath $Binaries -PathType Container)) { throw "Binaries directory not found: $Binaries" }
    $cdb = Resolve-NativeDebugger $Debugger
    $pwsh = (Get-Command pwsh.exe -ErrorAction Stop).Source
    $null = New-Item -ItemType Directory -Path $Logs -Force
    $ownerPath = Join-Path $Logs 'native-host-trace.owner.json'
    if (Test-Path -LiteralPath $ownerPath) { throw "Native trace ownership already exists; use a fresh log directory: $ownerPath" }
    $id = [guid]::NewGuid().ToString('N')
    $directory = Join-Path $Logs "native-host-trace-$id"
    $null = New-Item -ItemType Directory -Path $directory
    $owner = [pscustomobject]@{
        RunId = $id; StartedUtc = $started.ToString('o'); WatcherId = 0; WatcherStartUtc = ''
        LogDirectory = $Logs; BinariesDirectory = $Binaries; DebuggerPath = $cdb
    }
    $reservation = [System.IO.File]::Open($ownerPath, [System.IO.FileMode]::CreateNew)
    $reservation.Dispose()
    Write-NativeJson (Join-Path $directory 'debugger.json') (Get-NativeFileMetadata $cdb)
    $escapedScript = $PSCommandPath.Replace("'", "''")
    $escapedLogs = $Logs.Replace("'", "''")
    $command = "& '$escapedScript' -Action Watch -LogDirectory '$escapedLogs' -RunId '$id'"
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    $watcher = $null
    $ready = $false
    try {
        $watcher = Start-Process -FilePath $pwsh -ArgumentList '-NoLogo', '-NoProfile', '-NonInteractive', '-EncodedCommand', $encoded `
            -WindowStyle Hidden -RedirectStandardOutput (Join-Path $directory 'watcher.stdout.log') `
            -RedirectStandardError (Join-Path $directory 'watcher.stderr.log') -PassThru
        $null = $watcher.Handle
        $owner.WatcherId = $watcher.Id
        $owner.WatcherStartUtc = $watcher.StartTime.ToUniversalTime().ToString('o')
        Write-NativeJson $ownerPath $owner
        $deadline = [datetime]::UtcNow.AddSeconds(25)
        do {
            if ($watcher.HasExited) { throw "Native trace watcher exited during startup ($($watcher.ExitCode)); inspect $directory." }
            $statePath = Join-Path $directory 'state.json'
            if (Test-Path -LiteralPath $statePath) {
                $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
                if ($state.Status -eq 'Ready' -and $state.RunId -eq $id -and $state.WatcherId -eq $watcher.Id -and
                    ([datetime]::UtcNow - ([datetime]$state.HeartbeatUtc).ToUniversalTime()).TotalSeconds -lt 10) {
                    $ready = $true
                    Write-Output "Native host trace ready: PID $($watcher.Id); artifacts $directory"
                    return
                }
                if ($state.Status -eq 'Failed') { throw $state.Message }
            }
            Start-Sleep -Milliseconds 250
        } while ([datetime]::UtcNow -lt $deadline)
        throw "Native trace watcher did not become ready within 25 seconds; inspect $directory."
    }
    finally {
        if ($watcher) {
            if (-not $ready -and -not $watcher.HasExited) {
                Set-Content -LiteralPath (Join-Path $directory 'stop.requested') -Value $id
                $null = $watcher.WaitForExit(75000)
            }
            $watcher.Dispose()
        }
    }
}

function Stop-NativeTestHostTrace {
    param([string]$Logs)
    $Logs = Get-NativeLocalPath $Logs
    $ownerPath = Join-Path $Logs 'native-host-trace.owner.json'
    if (-not (Test-Path -LiteralPath $ownerPath)) { Write-Output 'No native trace watcher was started.'; return }
    $owner = Get-Content -LiteralPath $ownerPath -Raw | ConvertFrom-Json
    if ($owner.RunId -notmatch '^[0-9a-f]{32}$' -or $owner.LogDirectory -ine $Logs -or $owner.WatcherId -le 0) {
        throw 'Invalid native trace ownership record; no process was signaled.'
    }
    $directory = Join-Path $Logs "native-host-trace-$($owner.RunId)"
    $watcher = $null
    try {
        try { $watcher = Open-NativeProcess $owner.WatcherId }
        catch [System.ArgumentException] { }
        if ($watcher -and -not $watcher.HasExited) {
            if (-not (Test-NativeProcessIdentity $watcher $owner.WatcherId ([datetime]$owner.WatcherStartUtc))) {
                throw 'Native trace watcher PID was reused; no process was signaled.'
            }
            if ([System.IO.Path]::GetFileName($watcher.MainModule.FileName) -ine 'pwsh.exe') {
                throw 'Owned watcher is not pwsh.exe; no process was signaled.'
            }
            Set-Content -LiteralPath (Join-Path $directory 'stop.requested') -Value $owner.RunId
            if (-not $watcher.WaitForExit(75000)) {
                throw 'Watcher did not stop within 75 seconds; refusing to terminate it or any test/debugger process by PID lookup.'
            }
        }
        $statePath = Join-Path $directory 'state.json'
        if (-not (Test-Path -LiteralPath $statePath)) { throw "Native trace watcher has no final state; inspect $directory." }
        $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
        Write-Output "Native host trace: $($state.Status), captures=$($state.Captures); $($state.Message); artifacts $directory"
        if ($state.Status -notin @('Completed', 'Stopped', 'Expired')) { throw 'Native host trace failed or exited without cleanup; inspect watcher logs.' }
    }
    finally {
        if ($watcher) { $watcher.Dispose() }
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    $ErrorActionPreference = 'Stop'
    switch ($Action) {
        Start { Start-NativeTestHostTrace $LogDirectory $BinariesDirectory $DebuggerPath }
        Stop { Stop-NativeTestHostTrace $LogDirectory }
        Watch {
            if ($RunId -notmatch '^[0-9a-f]{32}$') { throw 'Watch requires the Start-generated ownership token.' }
            $LogDirectory = Get-NativeLocalPath $LogDirectory
            $ownerPath = Join-Path $LogDirectory 'native-host-trace.owner.json'
            $owner = $null
            for ($attempt = 0; $attempt -lt 40; $attempt++) {
                try { $owner = Get-Content -LiteralPath $ownerPath -Raw | ConvertFrom-Json } catch { }
                if ($owner -and $owner.WatcherId -eq $PID) { break }
                Start-Sleep -Milliseconds 250
            }
            $self = Open-NativeProcess $PID
            try {
                if (-not $owner -or $owner.RunId -ne $RunId -or $owner.LogDirectory -ine $LogDirectory -or
                    -not (Test-NativeProcessIdentity $self $owner.WatcherId ([datetime]$owner.WatcherStartUtc))) {
                    throw 'Watch ownership does not match this process.'
                }
                Watch-NativeTestHost $owner (Join-Path $LogDirectory "native-host-trace-$RunId")
            }
            finally { $self.Dispose() }
        }
    }
}
