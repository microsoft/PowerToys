// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace Microsoft.CmdPal.UI.Utilities;

/// <summary>
/// Watches for taskbar changes using SetWinEventHook.
/// The UI consumes pending changes outside the native callback.
/// Scoped to explorer.exe's process to avoid an event storm from
/// other windows (flyouts, tooltips, etc.).
/// </summary>
public sealed unsafe partial class TaskbarWatcher : IDisposable
{
    private readonly List<HWINEVENTHOOK> _hooks = new();
    private readonly HWND _taskbarHwnd;
    private int _changed;
    private bool _disposed;

    public bool ConsumeChanges() => Interlocked.Exchange(ref _changed, 0) != 0;

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
        var instance = _instance;
        if (instance is not null &&
            !instance._disposed &&
            !hwnd.IsNull &&
            (hwnd == instance._taskbarHwnd || PInvoke.IsChild(instance._taskbarHwnd, hwnd)))
        {
            Interlocked.Exchange(ref instance._changed, 1);
        }
    }

    public TaskbarWatcher()
    {
        // Find explorer.exe's process ID from Shell_TrayWnd so we
        // only receive events from the taskbar, not from our own
        // flyouts/tooltips/teaching tips.
        _taskbarHwnd = PInvoke.FindWindow("Shell_TrayWnd", null);
        uint explorerPid = 0;
        if (!_taskbarHwnd.IsNull)
        {
            _ = PInvoke.GetWindowThreadProcessId(_taskbarHwnd, &explorerPid);
        }

        if (explorerPid == 0)
        {
            throw new InvalidOperationException("Cannot watch taskbar changes because the taskbar process is unavailable.");
        }

        uint[] events =
        [
            PInvoke.EVENT_OBJECT_REORDER,
            PInvoke.EVENT_OBJECT_CREATE,
            PInvoke.EVENT_OBJECT_DESTROY,
            PInvoke.EVENT_OBJECT_NAMECHANGE,
            PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
        ];
        try
        {
            foreach (var evt in events)
            {
                var hook = PInvoke.SetWinEventHook(
                    evt,
                    evt,
                    HMODULE.Null,
                    &WinEventProc,
                    explorerPid,
                    0,
                    PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS);

                if (hook.IsNull)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to watch taskbar changes.");
                }

                _hooks.Add(hook);
            }

            _instance = this;
        }
        catch
        {
            Dispose();
            throw;
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
