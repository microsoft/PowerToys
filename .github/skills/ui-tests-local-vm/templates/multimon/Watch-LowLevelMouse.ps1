# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
    Records what a WH_MOUSE_LL hook sees for a fixed period: position, flags, timestamps, extra info.

.DESCRIPTION
    Runs inside the guest in the standard user's interactive session, under PowerShell 7, launched by
    a limited interactive scheduled task. It shows how each input source reaches a low-level mouse
    hook, which Mouse Utilities modules depend on: whether VMConnect, RDP, and SendInput movement
    each set LLMHF_INJECTED, whether their coordinates arrive absolute, how far apart their
    timestamps are, and whether SetCursorPos reaches the hook at all.

    The hook runs in compiled C# and only queues events. A PowerShell callback inside a low-level
    hook risks exceeding LowLevelHooksTimeout, after which Windows silently removes the hook.
    The hook passes every event on unchanged, so the probe never alters input.

    The process needs no DPI awareness: MSLLHOOKSTRUCT reports the cursor in per-monitor-aware
    screen coordinates whatever the hooking process's awareness.

.PARAMETER DurationSeconds
    How long to record.

.PARAMETER OutputPath
    CSV file that receives one row per event.

.EXAMPLE
    pwsh -NoProfile -File C:\PowerToysUiTestRun\Multimon\Watch-LowLevelMouse.ps1 -DurationSeconds 20 -OutputPath C:\PowerToysUiTestRun\Multimon\out\hook.csv
#>
[CmdletBinding()]
param(
    [ValidateRange(1, 600)]
    [int]$DurationSeconds = 20,

    [Parameter(Mandatory)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

public static class MouseHookProbe
{
    [StructLayout(LayoutKind.Sequential)]
    struct MSLLHOOKSTRUCT { public int X, Y; public uint MouseData, Flags, Time; public UIntPtr ExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    struct MSG { public IntPtr Hwnd; public uint Message; public UIntPtr WParam; public IntPtr LParam; public uint Time; public int X, Y; }

    public sealed class Event
    {
        public long Tick; public int Message; public int X, Y; public uint Flags, Time; public ulong ExtraInfo;
    }

    delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern int GetMessage(out MSG msg, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] static extern UIntPtr SetTimer(IntPtr hwnd, UIntPtr id, uint elapse, IntPtr proc);
    [DllImport("user32.dll")] static extern bool KillTimer(IntPtr hwnd, UIntPtr id);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string name);

    const int WH_MOUSE_LL = 14;
    const uint WM_TIMER = 0x0113;

    static readonly ConcurrentQueue<Event> events = new ConcurrentQueue<Event>();
    static HookProc callback;
    static IntPtr hook;

    // The hook is called on this thread, so it must keep pumping messages for the whole recording.
    public static Event[] Record(uint milliseconds)
    {
        callback = OnEvent;
        hook = SetWindowsHookEx(WH_MOUSE_LL, callback, GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero) throw new InvalidOperationException("SetWindowsHookEx failed: " + Marshal.GetLastWin32Error());
        UIntPtr timer = SetTimer(IntPtr.Zero, UIntPtr.Zero, milliseconds, IntPtr.Zero);
        try
        {
            MSG msg;
            while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.Message == WM_TIMER && msg.WParam == timer) break;
            }
        }
        finally
        {
            KillTimer(IntPtr.Zero, timer);
            UnhookWindowsHookEx(hook);
        }
        return events.ToArray();
    }

    static IntPtr OnEvent(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            events.Enqueue(new Event
            {
                Tick = System.Diagnostics.Stopwatch.GetTimestamp(), Message = wParam.ToInt32(),
                X = data.X, Y = data.Y, Flags = data.Flags, Time = data.Time, ExtraInfo = data.ExtraInfo.ToUInt64(),
            });
        }
        return CallNextHookEx(hook, code, wParam, lParam);
    }
}
'@

$events = [MouseHookProbe]::Record([uint32]($DurationSeconds * 1000))
$ticksPerMs = [System.Diagnostics.Stopwatch]::Frequency / 1000.0

$rows = @(foreach ($hookEvent in $events) {
    [pscustomobject]@{
        ElapsedMs       = [math]::Round(($hookEvent.Tick - $events[0].Tick) / $ticksPerMs, 2)
        Message         = '0x{0:X4}' -f $hookEvent.Message
        X               = $hookEvent.X
        Y               = $hookEvent.Y
        Injected        = ($hookEvent.Flags -band 0x1) -ne 0
        LowerIlInjected = ($hookEvent.Flags -band 0x2) -ne 0
        HookTime        = $hookEvent.Time
        ExtraInfo       = '0x{0:X}' -f $hookEvent.ExtraInfo
    }
})
New-Item -ItemType Directory -Path (Split-Path -Path $OutputPath -Parent) -Force | Out-Null
$rows | Export-Csv -LiteralPath $OutputPath -NoTypeInformation -Encoding utf8

[pscustomobject]@{
    Events        = $rows.Count
    InjectedCount = @($rows | Where-Object -Property Injected).Count
    OutputPath    = $OutputPath
}
