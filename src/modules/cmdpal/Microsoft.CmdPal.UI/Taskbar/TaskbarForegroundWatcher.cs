// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using CommunityToolkit.WinUI;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace Microsoft.CmdPal.UI.Taskbar;

internal sealed class TaskbarForegroundWatcher : IAsyncDisposable
{
    private readonly DispatcherQueueController _controller;
    private readonly WINEVENTPROC _callback;
    private readonly Task _startTask;
    private HWINEVENTHOOK _hook;
    private int _stopped;

    public TaskbarForegroundWatcher(HWND window, uint message)
    {
        ArgumentOutOfRangeException.ThrowIfZero(message);
        _controller = DispatcherQueueController.CreateOnDedicatedThread();
        _callback = (_, _, foreground, _, _, _, _) =>
        {
            // Only post a notification. In particular, never parent a window or
            // synchronously wait for XAML from inside the WinEvent callback.
            if (Volatile.Read(ref _stopped) == 0 &&
                !foreground.IsNull &&
                !PInvoke.PostMessage(window, message, new WPARAM((nuint)(nint)foreground), default) &&
                Interlocked.Exchange(ref _stopped, 1) == 0)
            {
                Logger.LogError("Failed to post a taskbar foreground notification.", new Win32Exception(Marshal.GetLastWin32Error()));
            }
        };
        _startTask = StartAsync();
    }

    private async Task StartAsync()
    {
        try
        {
            await _controller.DispatcherQueue.EnqueueAsync(() =>
            {
                if (Volatile.Read(ref _stopped) != 0)
                {
                    return;
                }

                _hook = PInvoke.SetWinEventHook(
                    PInvoke.EVENT_SYSTEM_FOREGROUND,
                    PInvoke.EVENT_SYSTEM_FOREGROUND,
                    HMODULE.Null,
                    _callback,
                    0,
                    0,
                    PInvoke.WINEVENT_OUTOFCONTEXT);
                if (_hook.IsNull)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to watch shell foreground changes.");
                }
            });
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _stopped, 1);
            Logger.LogError("Failed to start the taskbar foreground watcher.", ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _stopped, 1);
        await _startTask;
        try
        {
            await _controller.DispatcherQueue.EnqueueAsync(() =>
            {
                if (!_hook.IsNull)
                {
                    if (!PInvoke.UnhookWinEvent(_hook))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to stop watching shell foreground changes.");
                    }

                    _hook = default;
                }
            });
        }
        finally
        {
            await _controller.ShutdownQueueAsync();
        }
    }
}
