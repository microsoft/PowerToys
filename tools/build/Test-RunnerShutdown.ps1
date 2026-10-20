# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#requires -Version 7.0

<#
.SYNOPSIS
Checks that a locally built Runner exits naturally after a graceful close.

.DESCRIPTION
Use a complete development layout in an interactive test session, with no other
Runner in that session. The current PowerToys profile is used; snapshot/restore it
and the user's PowerToys autorun task, or use a disposable test account. Runner
startup can update that task to the test executable path. No installation files
or settings are directly edited by this script. WM_CLOSE follows the tray Quit
shutdown path, but does not replace a separate UI check of the tray menu.

A timeout is a failure, never a pass. Only the process started by the current
iteration is force-stopped on failure, and that cleanup is recorded in results.json.

.EXAMPLE
.\tools\build\Test-RunnerShutdown.ps1 -RunnerPath D:\test-payload\PowerToys.exe -Iterations 5
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$RunnerPath,
    [ValidateRange(1, 50)]
    [int]$Iterations = 5,
    [ValidateRange(5, 120)]
    [int]$ExitTimeoutSeconds = 30,
    [ValidateRange(0, 10)]
    [int]$SettleSeconds = 2,
    [string]$ResultsDirectory = (Join-Path $env:TEMP ("RunnerShutdown-{0:yyyyMMdd-HHmmss}" -f (Get-Date)))
)

$ErrorActionPreference = 'Stop'
$runner = (Get-Item -LiteralPath $RunnerPath -ErrorAction Stop).FullName
if ([IO.Path]::GetFileName($runner) -ine 'PowerToys.exe') {
    throw 'RunnerPath must identify PowerToys.exe in a complete test payload.'
}
$sessionId = [Diagnostics.Process]::GetCurrentProcess().SessionId
$existing = @(Get-Process -Name PowerToys -ErrorAction SilentlyContinue | Where-Object SessionId -eq $sessionId)
if ($existing.Count) {
    throw "Close existing Runner instances in this session before testing. PIDs: $($existing.Id -join ', ')"
}
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null

if (-not ('PowerToys.RunnerShutdownProbe.NativeMethods' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PowerToys.RunnerShutdownProbe
{
    public static class NativeMethods
    {
        private delegate bool EnumWindowProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr window, uint message, UIntPtr wParam, IntPtr lParam,
            uint flags, uint timeout, out UIntPtr result);

        public static IntPtr FindWindow(int processId, string className)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((window, parameter) =>
            {
                GetWindowThreadProcessId(window, out uint owner);
                if (owner == processId)
                {
                    var name = new StringBuilder(256);
                    GetClassName(window, name, name.Capacity);
                    if (name.ToString() == className)
                    {
                        found = window;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        public static bool IsResponsive(IntPtr window)
        {
            return SendMessageTimeout(window, 0, UIntPtr.Zero, IntPtr.Zero, 0x23, 250, out _) != IntPtr.Zero;
        }
    }
}
'@
}

$results = [Collections.Generic.List[object]]::new()
for ($iteration = 1; $iteration -le $Iterations; $iteration++) {
    $process = $null
    $record = [ordered]@{
        Iteration = $iteration
        RunnerPath = $runner
        Pid = $null
        Status = 'FAIL'
        ReadyMilliseconds = $null
        ExitMilliseconds = $null
        ExitCode = $null
        NaturalExit = $false
        ForcedCleanup = $false
        Failure = $null
    }
    try {
        $process = Start-Process -FilePath $runner -ArgumentList '--dont-elevate' `
            -WorkingDirectory (Split-Path $runner -Parent) -PassThru
        $record.Pid = $process.Id
        $startup = [Diagnostics.Stopwatch]::StartNew()
        $window = [IntPtr]::Zero
        while ($startup.Elapsed.TotalSeconds -lt 60) {
            if ($process.HasExited) {
                throw "Runner exited during startup (code $($process.ExitCode))."
            }
            if ([PowerToys.RunnerShutdownProbe.NativeMethods]::FindWindow($process.Id, '#32770') -ne [IntPtr]::Zero) {
                throw 'Runner opened a modal startup dialog. Check the test payload and startup logs.'
            }
            $window = [PowerToys.RunnerShutdownProbe.NativeMethods]::FindWindow($process.Id, 'PToyTrayIconWindow')
            if ($window -ne [IntPtr]::Zero -and [PowerToys.RunnerShutdownProbe.NativeMethods]::IsResponsive($window)) {
                break
            }
            Start-Sleep -Milliseconds 100
        }
        if ($window -eq [IntPtr]::Zero -or -not [PowerToys.RunnerShutdownProbe.NativeMethods]::IsResponsive($window)) {
            throw 'Runner did not reach a responsive tray window within 60 seconds.'
        }
        $record.ReadyMilliseconds = $startup.ElapsedMilliseconds
        if ($SettleSeconds) { Start-Sleep -Seconds $SettleSeconds }
        $shutdown = [Diagnostics.Stopwatch]::StartNew()
        if (-not [PowerToys.RunnerShutdownProbe.NativeMethods]::PostMessage($window, 0x10, [UIntPtr]::Zero, [IntPtr]::Zero)) {
            throw [ComponentModel.Win32Exception]::new([Runtime.InteropServices.Marshal]::GetLastWin32Error())
        }
        if (-not $process.WaitForExit($ExitTimeoutSeconds * 1000)) {
            throw "Runner PID $($process.Id) remained alive after graceful close for $ExitTimeoutSeconds seconds."
        }
        $record.ExitMilliseconds = $shutdown.ElapsedMilliseconds
        $record.ExitCode = $process.ExitCode
        $record.NaturalExit = $true
        if ($process.ExitCode -ne 0) {
            throw "Runner exited with code $($process.ExitCode)."
        }
        $record.Status = 'PASS'
        Write-Host "PASS: iteration $iteration, PID $($process.Id), natural exit in $($record.ExitMilliseconds) ms."
    }
    catch {
        $record.Failure = $_.Exception.Message
        throw
    }
    finally {
        if ($process -and -not $process.HasExited) {
            Write-Warning "Stopping only failed test process PID $($process.Id); this is not a successful shutdown."
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
            $record.ForcedCleanup = $true
        }
        $results.Add([pscustomobject]$record)
        $results | ConvertTo-Json -Depth 5 |
            Set-Content -LiteralPath (Join-Path $ResultsDirectory 'results.json') -Encoding utf8
        if ($process) { $process.Dispose() }
    }
}
