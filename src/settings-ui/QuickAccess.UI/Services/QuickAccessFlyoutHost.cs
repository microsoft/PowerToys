// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DesktopFlyouts;
using Microsoft.PowerToys.QuickAccess.Flyout;
using Microsoft.PowerToys.QuickAccess.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Microsoft.PowerToys.QuickAccess.Services;

internal sealed class QuickAccessFlyoutHost : IDisposable
{
    private const int ToggleGraceMilliseconds = 250;

    private readonly QuickAccessLaunchContext _launchContext;
    private readonly Action _exitApplication;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly QuickAccessCoordinator _coordinator;
    private readonly LauncherViewModel _launcherViewModel;
    private readonly QuickAccessFlyout _flyout;
    private readonly long _flyoutIsOpenChangedToken;
    private bool _disposed;
    private bool _explicitFlyoutHidePending;
    private long _lastImplicitFlyoutCloseTick;
    private EventWaitHandle? _showEvent;
    private EventWaitHandle? _exitEvent;
    private ManualResetEventSlim? _listenerShutdownEvent;
    private Thread? _showListenerThread;
    private Thread? _exitListenerThread;
    private CancellationTokenSource? _trimCts;

    public QuickAccessFlyoutHost(QuickAccessLaunchContext launchContext, Action exitApplication)
    {
        _launchContext = launchContext;
        _exitApplication = exitApplication;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _coordinator = new QuickAccessCoordinator(_launchContext, RequestHide);
        _launcherViewModel = new LauncherViewModel(_coordinator);
        var allAppsViewModel = new AllAppsViewModel(_coordinator);
        _flyout = new QuickAccessFlyout();
        _flyout.Initialize(_coordinator, _launcherViewModel, allAppsViewModel);
        _flyoutIsOpenChangedToken = _flyout.RegisterPropertyChangedCallback(DesktopFlyout.IsOpenProperty, OnFlyoutIsOpenChanged);

        InitializeEventListeners();
        ScheduleMemoryTrim();
    }

    private void RequestHide()
    {
        if (_disposed)
        {
            return;
        }

        if (_dispatcherQueue.HasThreadAccess)
        {
            HideFlyout();
        }
        else
        {
            _dispatcherQueue.TryEnqueue(HideFlyout);
        }
    }

    private void ToggleFlyout()
    {
        if (_disposed)
        {
            return;
        }

        if (_flyout.IsOpen)
        {
            HideFlyout();
            return;
        }

        var elapsedSinceImplicitClose = Environment.TickCount64 - _lastImplicitFlyoutCloseTick;
        if (_lastImplicitFlyoutCloseTick != 0 && elapsedSinceImplicitClose <= GetDoubleClickTime() + ToggleGraceMilliseconds)
        {
            _lastImplicitFlyoutCloseTick = 0;
            return;
        }

        CancelMemoryTrim();
        _flyout.Show();

        _flyout.RefreshIfAppsList();
    }

    private void HideFlyout()
    {
        if (!_flyout.IsOpen)
        {
            return;
        }

        _explicitFlyoutHidePending = true;
        _flyout.Hide();
    }

    private void OnFlyoutIsOpenChanged(DependencyObject sender, DependencyProperty property)
    {
        if (_flyout.IsOpen)
        {
            CancelMemoryTrim();
            _explicitFlyoutHidePending = false;
            return;
        }

        ScheduleMemoryTrim();

        if (_explicitFlyoutHidePending)
        {
            _explicitFlyoutHidePending = false;
            return;
        }

        _lastImplicitFlyoutCloseTick = Environment.TickCount64;
    }

    private void ScheduleMemoryTrim()
    {
        CancelMemoryTrim();
        _trimCts = new CancellationTokenSource();
        var token = _trimCts.Token;

        // Preserve the legacy launcher's idle-memory policy without trimming during quick toggles.
        _ = Task.Delay(2000, token).ContinueWith(
            _ =>
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                if (!token.IsCancellationRequested)
                {
                    using var process = Process.GetCurrentProcess();
                    SetProcessWorkingSetSize(process.Handle, -1, -1);
                }
            },
            token,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default);
    }

    private void CancelMemoryTrim()
    {
        _trimCts?.Cancel();
        _trimCts?.Dispose();
        _trimCts = null;
    }

    private void InitializeEventListeners()
    {
        if (!string.IsNullOrEmpty(_launchContext.ShowEventName))
        {
            try
            {
                _showEvent = EventWaitHandle.OpenExisting(_launchContext.ShowEventName);
                EnsureListenerInfrastructure();
                StartShowListenerThread();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
            }
        }

        if (!string.IsNullOrEmpty(_launchContext.ExitEventName))
        {
            try
            {
                _exitEvent = EventWaitHandle.OpenExisting(_launchContext.ExitEventName);
                EnsureListenerInfrastructure();
                StartExitListenerThread();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
            }
        }
    }

    private void EnsureListenerInfrastructure()
    {
        _listenerShutdownEvent ??= new ManualResetEventSlim(false);
    }

    private void StartShowListenerThread()
    {
        if (_showEvent == null || _listenerShutdownEvent == null || _showListenerThread != null)
        {
            return;
        }

        _showListenerThread = new Thread(ListenForShowEvents)
        {
            IsBackground = true,
            Name = "QuickAccess-ShowEventListener",
        };
        _showListenerThread.Start();
    }

    private void StartExitListenerThread()
    {
        if (_exitEvent == null || _listenerShutdownEvent == null || _exitListenerThread != null)
        {
            return;
        }

        _exitListenerThread = new Thread(ListenForExitEvents)
        {
            IsBackground = true,
            Name = "QuickAccess-ExitEventListener",
        };
        _exitListenerThread.Start();
    }

    private void ListenForShowEvents()
    {
        if (_showEvent == null || _listenerShutdownEvent == null)
        {
            return;
        }

        var handles = new WaitHandle[] { _showEvent, _listenerShutdownEvent.WaitHandle };
        try
        {
            while (true)
            {
                var index = WaitHandle.WaitAny(handles);
                if (index != 0)
                {
                    break;
                }

                _dispatcherQueue.TryEnqueue(ToggleFlyout);
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (ThreadInterruptedException)
        {
        }
    }

    private void ListenForExitEvents()
    {
        if (_exitEvent == null || _listenerShutdownEvent == null)
        {
            return;
        }

        var handles = new WaitHandle[] { _exitEvent, _listenerShutdownEvent.WaitHandle };
        try
        {
            if (WaitHandle.WaitAny(handles) == 0)
            {
                _dispatcherQueue.TryEnqueue(() => _exitApplication());
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (ThreadInterruptedException)
        {
        }
    }

    private void StopEventListeners()
    {
        if (_listenerShutdownEvent == null)
        {
            return;
        }

        _listenerShutdownEvent.Set();
        JoinListenerThread(ref _showListenerThread);
        JoinListenerThread(ref _exitListenerThread);
        _listenerShutdownEvent.Dispose();
        _listenerShutdownEvent = null;
    }

    private static void JoinListenerThread(ref Thread? thread)
    {
        if (thread == null)
        {
            return;
        }

        try
        {
            if (!thread.Join(TimeSpan.FromMilliseconds(250)))
            {
                thread.Interrupt();
                thread.Join(TimeSpan.FromMilliseconds(250));
            }
        }
        catch (ThreadInterruptedException)
        {
        }
        catch (ThreadStateException)
        {
        }

        thread = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CancelMemoryTrim();
        StopEventListeners();
        _showEvent?.Dispose();
        _exitEvent?.Dispose();
        _flyout.UnregisterPropertyChangedCallback(DesktopFlyout.IsOpenProperty, _flyoutIsOpenChangedToken);
        _flyout.Dispose();
        _launcherViewModel.Dispose();
        _coordinator.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, nint dwMinimumWorkingSetSize, nint dwMaximumWorkingSetSize);
}
