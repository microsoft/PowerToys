// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Records every time a top-level window of a given class is shown, using a <c>WinEvent</c> hook.
/// </summary>
/// <remarks>
/// Use this instead of polling <see cref="WindowControl.IsAnyWindowOfClassVisible"/> when the window
/// under observation is transient. Polling can only see a window that stays visible longer than the
/// sample interval, so it cannot distinguish "never shown" from "shown and hidden again immediately" —
/// and the interval cannot be lowered without the probe itself contending for the window manager.
/// <c>EVENT_OBJECT_SHOW</c> is delivered for every show regardless of how briefly the window survives.
/// </remarks>
public sealed class WindowShowWatcher : IDisposable
{
    private const uint EVENT_OBJECT_SHOW = 0x8002;
    private const uint EVENT_OBJECT_HIDE = 0x8003;
    private const int OBJID_WINDOW = 0;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    private const uint PM_REMOVE = 1;

    private readonly string className;
    private readonly ManualResetEventSlim shown = new(false);
    private readonly ManualResetEventSlim ready = new(false);
    private readonly Thread pump;
    private readonly List<string> events = new();
    private readonly Lock sync = new();
    private readonly WinEventProc callback; // keep the delegate alive for the hook's lifetime

    private volatile bool stop;

    public WindowShowWatcher(string className)
    {
        this.className = className;
        callback = OnWinEvent;

        pump = new Thread(Pump) { IsBackground = true };
        pump.Start();
        ready.Wait(5_000);
    }

    private delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

    /// <summary>Show/hide events seen so far, for diagnostics.</summary>
    public IReadOnlyList<string> Events
    {
        get
        {
            lock (sync)
            {
                return events.ToArray();
            }
        }
    }

    /// <summary>Wait until a window of the watched class is shown.</summary>
    public bool Wait(int timeoutMs) => shown.Wait(timeoutMs);

    public void Dispose()
    {
        stop = true;
        pump.Join(2_000);
        shown.Dispose();
        ready.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr hWnd, [Out] char[] lpClassName, int nMaxCount);

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        if (idObject != OBJID_WINDOW || hwnd == IntPtr.Zero)
        {
            return;
        }

        var buffer = new char[256];
        var length = GetClassNameW(hwnd, buffer, buffer.Length);
        if (length <= 0 || !new string(buffer, 0, length).Equals(className, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // dwmsEventTime is when the OS raised the event; a timestamp taken here would instead measure
        // how promptly this thread pumped its queue, which would understate a very short-lived window.
        lock (sync)
        {
            events.Add($"{(eventType == EVENT_OBJECT_SHOW ? "SHOW" : "HIDE")} 0x{hwnd.ToInt64():X} @{dwmsEventTime}ms");
        }

        if (eventType == EVENT_OBJECT_SHOW)
        {
            shown.Set();
        }
    }

    private void Pump()
    {
        var hook = SetWinEventHook(
            EVENT_OBJECT_SHOW,
            EVENT_OBJECT_HIDE,
            IntPtr.Zero,
            callback,
            0,
            0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        ready.Set();
        if (hook == IntPtr.Zero)
        {
            return;
        }

        try
        {
            while (!stop)
            {
                while (PeekMessageW(out var msg, IntPtr.Zero, 0, 0, PM_REMOVE))
                {
                    DispatchMessageW(ref msg);
                }

                Thread.Sleep(5);
            }
        }
        finally
        {
            UnhookWinEvent(hook);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public POINT Point;
    }
}
