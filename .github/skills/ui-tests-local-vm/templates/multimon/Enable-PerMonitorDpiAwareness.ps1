# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Makes the calling thread per-monitor DPI aware and returns the thread's awareness.

.DESCRIPTION
Runs inside the guest. Invoke-LocalVmInteractive.ps1 runs it before every guest script, and the
scripts that read or write positions call it again, so a direct run is covered too. DPI awareness
belongs to the thread, not to a PowerShell scope, so a call with & reaches the caller's thread.

The script requests per-monitor awareness (v2) for the process and the thread. A process whose
awareness is already fixed, by a manifest, an earlier call, or pwsh's startup profile, refuses the
process request with ERROR_ACCESS_DENIED, which is not an error here: the thread context decides
the positions, sizes, and screen pixels the thread reads. See references\multimon.md.

It returns the thread's awareness: 0 unaware, 1 system aware, 2 per-monitor aware. Unless
ReportOnly is set, it fails when the result is not 2, because every position would then be scaled
rather than physical.

.PARAMETER ReportOnly
Returns the awareness without failing when it is not per-monitor.

.EXAMPLE
$awareness = & (Join-Path $PSScriptRoot 'Enable-PerMonitorDpiAwareness.ps1')
#>

[CmdletBinding()]
param(
    [switch]$ReportOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($null -eq ('PowerToysUiTestVm.DpiAwareness' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PowerToysUiTestVm
{
    public static class DpiAwareness
    {
        private const int ERROR_ACCESS_DENIED = 5;
        private static readonly IntPtr PerMonitorAwareV2 = new IntPtr(-4);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern IntPtr GetThreadDpiAwarenessContext();

        [DllImport("user32.dll")]
        private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr value);

        public static int EnsurePerMonitor()
        {
            if (!SetProcessDpiAwarenessContext(PerMonitorAwareV2))
            {
                int error = Marshal.GetLastWin32Error();
                if (error != ERROR_ACCESS_DENIED)
                {
                    throw new Win32Exception(error, "SetProcessDpiAwarenessContext failed");
                }
            }

            SetThreadDpiAwarenessContext(PerMonitorAwareV2);
            return GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext());
        }
    }
}
'@
}

$awareness = [PowerToysUiTestVm.DpiAwareness]::EnsurePerMonitor()
if (-not $ReportOnly -and $awareness -ne 2) {
    throw "The thread DPI awareness is $awareness, not per-monitor (2): Windows refused the per-monitor thread context, so positions and sizes would be scaled rather than physical."
}
$awareness
