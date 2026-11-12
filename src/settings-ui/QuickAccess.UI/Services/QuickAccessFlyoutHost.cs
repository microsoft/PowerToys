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
    private readonly QuickAccessLaunchContext _launchContext;
    private readonly Action _exitApplication;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly QuickAccessCoordinator _coordinator;
    private readonly LauncherViewModel _launcherViewModel;
    private readonly QuickAccessFlyout _flyout;
    private readonly long _flyoutIsOpenChangedToken;
    private readonly QuickAccessFlyoutState _visibilityState = new();
    private bool _disposed;
    private EventWaitHandle? _visibilityStateEvent;
    private EventWaitHandle? _visibilityChangedEvent;
    private EventWaitHandle? _exitEvent;
    private ManualResetEventSlim? _listenerShutdownEvent;
    private Thread? _visibilityListenerThread;
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

    private void ShowFlyout()
    {
        if (_disposed)
        {
            return;
        }

        _flyout.ApplyTemplate();
        ExecuteFlyoutAction(_visibilityState.RequestShow());
    }

    private void ExecuteFlyoutAction(QuickAccessFlyoutAction action)
    {
        if (_disposed)
        {
            return;
        }

        if (action == QuickAccessFlyoutAction.Show)
        {
            CancelMemoryTrim();
            _flyout.Show(GetFlyoutPosition());

            _flyout.RefreshIfAppsList();
        }
        else if (action == QuickAccessFlyoutAction.Hide)
        {
            _flyout.Hide();
        }
    }

    private void HideFlyout()
    {
        ExecuteFlyoutAction(_visibilityState.RequestHide());
    }

    private static DesktopFlyoutPosition GetFlyoutPosition()
    {
        if (TryGetRunnerTrayIconBounds(out var bounds))
        {
            return DesktopFlyoutPosition.ForTaskbarAnchor(bounds);
        }

        return DesktopFlyoutPosition.InWorkArea(
            DesktopFlyoutWorkAreaPosition.BottomRight,
            DesktopFlyoutMonitorSelection.Cursor);
    }

    private static bool TryGetRunnerTrayIconBounds(out System.Drawing.Rectangle bounds)
    {
        bounds = System.Drawing.Rectangle.Empty;

        // Match the tray icon registered by Runner in tray_icon.cpp.
        var trayIconWindow = FindWindowW("PToyTrayIconWindow", null);
        if (trayIconWindow == IntPtr.Zero)
        {
            return false;
        }

        var identifier = new NotifyIconIdentifier
        {
            CbSize = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
            HWnd = trayIconWindow,
            UId = 1,
        };

        if (Shell_NotifyIconGetRect(ref identifier, out var iconRect) < 0 ||
            iconRect.Right <= iconRect.Left ||
            iconRect.Bottom <= iconRect.Top)
        {
            return false;
        }

        bounds = System.Drawing.Rectangle.FromLTRB(iconRect.Left, iconRect.Top, iconRect.Right, iconRect.Bottom);
        return true;
    }

    private void OnFlyoutIsOpenChanged(DependencyObject sender, DependencyProperty property)
    {
        if (_flyout.IsOpen)
        {
            CancelMemoryTrim();
            ExecuteFlyoutAction(_visibilityState.OnOpened());
            return;
        }

        ScheduleMemoryTrim();

        var action = _visibilityState.OnClosed();

        // CompleteClose hides the native window after raising IsOpen's notification.
        // Reopen on the next dispatcher turn so that hide cannot overwrite our show.
        _dispatcherQueue.TryEnqueue(() => ExecuteFlyoutAction(action));
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
        if (!string.IsNullOrEmpty(_launchContext.VisibilityStateEventName))
        {
            try
            {
                _visibilityStateEvent = EventWaitHandle.OpenExisting(_launchContext.VisibilityStateEventName);
            }
            catch (WaitHandleCannotBeOpenedException)
            {
            }
        }

        if (!string.IsNullOrEmpty(_launchContext.VisibilityChangedEventName))
        {
            try
            {
                _visibilityChangedEvent = EventWaitHandle.OpenExisting(_launchContext.VisibilityChangedEventName);
            }
            catch (WaitHandleCannotBeOpenedException)
            {
            }
        }

        if (_visibilityStateEvent is not null && _visibilityChangedEvent is not null)
        {
            EnsureListenerInfrastructure();
            StartVisibilityListenerThread();
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

    private void StartVisibilityListenerThread()
    {
        if (_visibilityStateEvent is null || _visibilityChangedEvent is null || _listenerShutdownEvent == null || _visibilityListenerThread != null)
        {
            return;
        }

        _visibilityListenerThread = new Thread(ListenForVisibilityChanges)
        {
            IsBackground = true,
            Name = "QuickAccess-VisibilityListener",
        };
        _visibilityListenerThread.Start();
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

    private void ListenForVisibilityChanges()
    {
        if (_visibilityStateEvent is null || _visibilityChangedEvent is null || _listenerShutdownEvent == null)
        {
            return;
        }

        var handles = new WaitHandle[] { _visibilityChangedEvent, _listenerShutdownEvent.WaitHandle };
        try
        {
            while (true)
            {
                var index = WaitHandle.WaitAny(handles);
                if (index == 1)
                {
                    break;
                }

                // Read the manual-reset state when the UI thread handles the wake-up so
                // coalesced change signals always apply the latest requested visibility.
                _dispatcherQueue.TryEnqueue(ApplyRequestedVisibility);
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (ThreadInterruptedException)
        {
        }
    }

    private void ApplyRequestedVisibility()
    {
        if (_disposed || _visibilityStateEvent is null)
        {
            return;
        }

        if (_visibilityStateEvent.WaitOne(0))
        {
            ShowFlyout();
        }
        else
        {
            HideFlyout();
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
        JoinListenerThread(ref _visibilityListenerThread);
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
        _visibilityStateEvent?.Dispose();
        _visibilityChangedEvent?.Dispose();
        _exitEvent?.Dispose();
        _flyout.UnregisterPropertyChangedCallback(DesktopFlyout.IsOpenProperty, _flyoutIsOpenChangedToken);
        _flyout.Dispose();
        _launcherViewModel.Dispose();
        _coordinator.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, nint dwMinimumWorkingSetSize, nint dwMaximumWorkingSetSize);

    [DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr FindWindowW(string? className, string? windowName);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int Shell_NotifyIconGetRect(ref NotifyIconIdentifier identifier, out NativeRect iconLocation);

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyIconIdentifier
    {
        public uint CbSize;
        public IntPtr HWnd;
        public uint UId;
        public Guid GuidItem;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
