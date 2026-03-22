// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace Microsoft.CmdPal.UI.Utilities;

/// <summary>
/// Watches for taskbar changes using SetWinEventHook and fires
/// <see cref="Changed"/> when a change is detected.
/// Uses WINEVENT_OUTOFCONTEXT — the callback is delivered via the
/// message pump on the thread that created this _instance.
/// </summary>
public sealed unsafe class TaskbarWatcher : IDisposable
{
    private readonly List<HWINEVENTHOOK> _hooks = new();
    private bool _disposed;

    /// <summary>Raised when any taskbar-related accessibility event fires.</summary>
    public event Action? Changed;

    private static TaskbarWatcher? _instance;

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

    public TaskbarWatcher()
    {
        _instance = this;

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
                0,
                0,
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

            if (_instance == this)
            {
                _instance = null;
            }

            _disposed = true;
        }
    }
}
