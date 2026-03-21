// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using ManagedCommon;
using Microsoft.CmdPal.UI.Utilities;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;
using WinUIEx;

namespace Microsoft.CmdPal.UI.Taskbar;

public sealed partial class TaskbarWindow : WindowEx,
    IRecipient<QuitMessage>,
    IDisposable
{
    private readonly uint wMTASKBARRESTART;
    private readonly HWND _hwnd;
    private readonly TaskbarMetrics _taskbarMetrics;
    private readonly TaskbarBandControl _bandsControl;

    private readonly WNDPROC? _originalWndProc;
    private readonly WNDPROC? _customWndProc;

    private readonly DispatcherQueueTimer _updateLayoutDebouncer;

    private double _lastContentSpace;
    private bool _disposed;

    /// <summary>
    /// Creates a <see cref="TaskbarWindow"/> without blocking the UI thread.
    /// The first (expensive) UIA/COM initialization runs on a background
    /// thread; the window itself is created on the UI thread once ready.
    /// </summary>
    public static async Task<TaskbarWindow> CreateAsync()
    {
        // The very first TaskbarMetrics.Update() is expensive because it
        // initializes COM and enumerates the full UIA tree. Do that work
        // on a thread-pool thread so the UI stays responsive.
        var metrics = await Task.Run(() =>
        {
            var m = new TaskbarMetrics();
            m.Update();
            return m;
        });

        // Window creation must happen on the UI thread — we get here
        // because the caller is an async method on the UI thread.
        return new TaskbarWindow(metrics);
    }

    internal TaskbarWindow(TaskbarMetrics metrics)
    {
        var serviceProvider = App.Current.Services;
        var viewModel = serviceProvider.GetRequiredService<DockViewModel>();

        _bandsControl = new TaskbarBandControl(viewModel);

        InitializeComponent();

        MainContent.Content = _bandsControl;

        wMTASKBARRESTART = PInvoke.RegisterWindowMessage("TaskbarCreated");

        _hwnd = new HWND(WindowNative.GetWindowHandle(this));

        _updateLayoutDebouncer = DispatcherQueue.CreateTimer();

        MainContent.SizeChanged += MainContent_SizeChanged;

        WeakReferenceMessenger.Default.Register<QuitMessage>(this);

        _taskbarMetrics = metrics;

        // LOAD BEARING: The delegate must be stored in a member field.
        // A local variable would be collected, leaving a dangling function pointer.
        _customWndProc = CustomWndProc;
        var procPointer = Marshal.GetFunctionPointerForDelegate(_customWndProc);
        _originalWndProc = Marshal.GetDelegateForFunctionPointer<WNDPROC>(
            PInvoke.SetWindowLongPtr(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_WNDPROC, procPointer));

        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

        if (AppWindow.Presenter is OverlappedPresenter overlappedPresenter)
        {
            overlappedPresenter.SetBorderAndTitleBar(false, false);
            overlappedPresenter.IsResizable = false;
        }

        MoveToTaskbar();
    }

    private void MainContent_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ClipWindow().ConfigureAwait(false);
    }

    private LRESULT CustomWndProc(HWND hwnd, uint uMsg, WPARAM wParam, LPARAM lParam)
    {
        if (uMsg == PInvoke.WM_DISPLAYCHANGE)
        {
            DispatcherQueue.TryEnqueue(() => MoveToTaskbar());
        }
        else if (uMsg == PInvoke.WM_SETTINGCHANGE)
        {
            if (wParam == (uint)SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETWORKAREA)
            {
                DispatcherQueue.TryEnqueue(TriggerDebouncedLayoutUpdate);
            }
        }
        else if (uMsg == PInvoke.WM_DESTROY)
        {
            return (LRESULT)0;
        }
        else if (uMsg == wMTASKBARRESTART)
        {
            Logger.LogDebug("TaskbarWindow: WM_TASKBAR_RESTART");
            DispatcherQueue.TryEnqueue(() => this.Close());
        }

        return PInvoke.CallWindowProc(_originalWndProc, hwnd, uMsg, wParam, lParam);
    }

    private async Task UpdateLayoutForDPI()
    {
        MoveToTaskbar();
        await Task.Delay(200);
        MainContent.Padding = new Thickness(1);
        await Task.Delay(10);
        MainContent.Padding = new Thickness(0);
    }

    private void TriggerDebouncedLayoutUpdate()
    {
        _updateLayoutDebouncer.Debounce(
            async () => await UpdateLayoutForDPI(),
            interval: TimeSpan.FromMilliseconds(200),
            immediate: false);
    }

    private void MoveToTaskbar()
    {
        if (AppWindow is null)
        {
            Logger.LogDebug("TaskbarWindow: AppWindow was null");
            return;
        }

        var taskbarWindow = PInvoke.FindWindow("Shell_TrayWnd", null);
        var reBarWindow = PInvoke.FindWindowEx(taskbarWindow, HWND.Null, "ReBarWindow32", null);

        var oldStyle = (WINDOW_STYLE)PInvoke.GetWindowLong(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        var newStyle = (oldStyle & ~WINDOW_STYLE.WS_POPUP) | WINDOW_STYLE.WS_CHILD;
        _ = PInvoke.SetWindowLong(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, (int)newStyle);
        PInvoke.SetParent(_hwnd, taskbarWindow);

        PInvoke.GetWindowRect(taskbarWindow, out var taskbarRect);
        PInvoke.GetWindowRect(reBarWindow, out var reBarRect);

        RECT newWindowRect = default;
        newWindowRect.left = taskbarRect.left;
        newWindowRect.top = reBarRect.top - taskbarRect.top;
        newWindowRect.right = newWindowRect.left + (taskbarRect.right - taskbarRect.left);
        newWindowRect.bottom = newWindowRect.top + (reBarRect.bottom - reBarRect.top);

        // Don't clear the window region — leave the previous clip in
        // place until ClipWindow applies the updated one. Clearing it
        // would flash the window across the full taskbar width.
        PInvoke.SetWindowPos(
            _hwnd,
            HWND.Null,
            newWindowRect.left,
            newWindowRect.top,
            newWindowRect.Width,
            newWindowRect.Height,
            SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

        // Apply an immediate clip using pre-loaded metrics so the window
        // never flashes across the full taskbar width. The async ClipWindow
        // call will refine this once XAML layout has settled.
        if (_taskbarMetrics.ButtonsWidthInPixels > 0 || _taskbarMetrics.TrayWidthInPixels > 0)
        {
            var clipLeft = _taskbarMetrics.ButtonsWidthInPixels;
            var clipRight = newWindowRect.Width - _taskbarMetrics.TrayWidthInPixels;
            if (clipRight > clipLeft)
            {
                var hrgn = PInvoke.CreateRectRgn(clipLeft, 0, clipRight, newWindowRect.Height);
                _ = PInvoke.SetWindowRgn(_hwnd, hrgn, true);
            }
        }

        ClipWindow().ConfigureAwait(false);
    }

    private async Task<bool> UpdateTaskbarButtonsAsync()
    {
        // Run UIA enumeration on a background thread.
        var changed = await _taskbarMetrics.UpdateAsync();

        // After await, we may not be on the UI thread. All XAML updates
        // must go through the DispatcherQueue.
        if (!changed)
        {
            DispatcherQueue.TryEnqueue(() => _bandsControl.SetMaxAvailableWidth(_lastContentSpace));
            return false;
        }

        // Capture values from the background result.
        var buttonsPixels = _taskbarMetrics.ButtonsWidthInPixels;
        var trayPixels = _taskbarMetrics.TrayWidthInPixels;

        var tcs = new TaskCompletionSource<bool>();
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                var scaleFactor = PInvoke.GetDpiForWindow(_hwnd) / 96.0f;

                var buttonsInDips = buttonsPixels / scaleFactor;
                TaskbarButtons.Width = new GridLength(Math.Max(0, buttonsInDips - WindowsLogo.Width.Value));

                var trayInDips = trayPixels / scaleFactor;
                TrayIcons.Width = new GridLength(trayInDips);

                var available = this.Bounds.Width;
                var forContent = available - buttonsInDips - trayInDips;

                if (_lastContentSpace == forContent)
                {
                    _bandsControl.SetMaxAvailableWidth(forContent);
                    tcs.TrySetResult(false);
                    return;
                }

                if (forContent > 0)
                {
                    ContentColumn.MaxWidth = Root.ActualWidth == 0 ? double.MaxValue : forContent;
                    ContentColumn.Width = GridLength.Auto;
                    _bandsControl.SetMaxAvailableWidth(forContent);
                }
                else
                {
                    ContentColumn.MaxWidth = 0;
                    ContentColumn.Width = new GridLength(0);
                    _bandsControl.SetMaxAvailableWidth(0);
                }

                _lastContentSpace = forContent;
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return await tcs.Task;
    }

    private async Task ClipWindow(bool onlyIfButtonsChanged = false)
    {
        var taskbarChanged = await UpdateTaskbarButtonsAsync();
        if (onlyIfButtonsChanged && !taskbarChanged)
        {
            return;
        }

        // Wait for layout to settle, then clip on the UI thread.
        await Task.Delay(100);

        var tcs = new TaskCompletionSource();
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                var scaleFactor = PInvoke.GetDpiForWindow(_hwnd) / 96.0f;
                FrameworkElement clipToElement = MainContent;

                if (clipToElement.ActualWidth <= 0 || clipToElement.ActualHeight <= 0)
                {
                    tcs.TrySetResult();
                    return;
                }

                var position = clipToElement.TransformToVisual(this.Content).TransformPoint(default);
                RECT scaledBounds = new()
                {
                    left = (int)(position.X * scaleFactor),
                    top = (int)(position.Y * scaleFactor),
                    right = (int)((position.X + clipToElement.ActualWidth) * scaleFactor),
                    bottom = (int)((position.Y + clipToElement.ActualHeight) * scaleFactor),
                };

                if (scaledBounds.right <= scaledBounds.left || scaledBounds.bottom <= scaledBounds.top)
                {
                    tcs.TrySetResult();
                    return;
                }

                var hrgn = PInvoke.CreateRectRgn(
                    scaledBounds.left,
                    scaledBounds.top,
                    scaledBounds.right,
                    scaledBounds.bottom);
                _ = PInvoke.SetWindowRgn(_hwnd, hrgn, true);
            }
            catch
            {
                // Window may have been destroyed
            }

            tcs.TrySetResult();
        });

        await tcs.Task;
    }

    public void Receive(QuitMessage message)
    {
        _updateLayoutDebouncer?.Stop();
        DispatcherQueue.TryEnqueue(() => Close());
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _updateLayoutDebouncer?.Stop();
            _taskbarMetrics.Dispose();
            WeakReferenceMessenger.Default.UnregisterAll(this);
            _disposed = true;
        }
    }
}
