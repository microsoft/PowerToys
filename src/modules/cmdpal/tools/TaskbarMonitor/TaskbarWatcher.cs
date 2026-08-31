// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace TaskbarMonitor;

/// <summary>
/// Watches for taskbar changes using SetWinEventHook (EVENT_OBJECT_REORDER,
/// EVENT_OBJECT_CREATE, EVENT_OBJECT_DESTROY, EVENT_OBJECT_NAMECHANGE).
/// Fires <see cref="Changed"/> when a change is detected.
/// <para>
/// Must be created and disposed on the thread that runs the message pump.
/// </para>
/// </summary>
public sealed unsafe class TaskbarWatcher : IDisposable
{
    private readonly List<HWINEVENTHOOK> _hooks = new();
    private bool _disposed;

    /// <summary>Raised when any taskbar-related accessibility event fires.</summary>
    public event Action? Changed;

    // Must be stored in a field to prevent GC from collecting the pointer target.
    // (The static method reference is fine, but the delegate instance is not.)
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void WinEventProc(
        HWINEVENTHOOK hWinEventHook,
        uint @event,
        HWND hwnd,
        int idObject,
        int idChild,
        uint idEventThread,
        uint dwmsEventTime)
    {
        _instance?.Changed?.Invoke();
    }

    // Static instance reference so the static callback can reach our event.
    // Only one TaskbarWatcher is expected per process.
    private static TaskbarWatcher? _instance;

    public TaskbarWatcher()
    {
        _instance = this;

        // Hook events that indicate taskbar button changes:
        //   EVENT_OBJECT_REORDER  (0x8004) — children reordered
        //   EVENT_OBJECT_CREATE   (0x8000) — new element created
        //   EVENT_OBJECT_DESTROY  (0x8001) — element removed
        //   EVENT_OBJECT_NAMECHANGE (0x800C) — element renamed (e.g. window title change)
        uint[] events =
        [
            PInvoke.EVENT_OBJECT_REORDER,
            PInvoke.EVENT_OBJECT_CREATE,
            PInvoke.EVENT_OBJECT_DESTROY,
            PInvoke.EVENT_OBJECT_NAMECHANGE,
        ];

        foreach (var evt in events)
        {
            var hook = PInvoke.SetWinEventHook(
                evt,
                evt,
                HMODULE.Null,
                &WinEventProc,
                0, // all processes
                0, // all threads
                PInvoke.WINEVENT_OUTOFCONTEXT);

            if (!hook.IsNull)
            {
                _hooks.Add(hook);
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var hook in _hooks)
            {
                PInvoke.UnhookWinEvent(hook);
            }

            _hooks.Clear();
            _instance = null;
            _disposed = true;
        }
    }
}
