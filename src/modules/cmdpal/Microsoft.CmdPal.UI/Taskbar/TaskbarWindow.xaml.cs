// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using ManagedCommon;
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
    private readonly TaskbarBandControl _bandsControl;

    private readonly WNDPROC? _originalWndProc;
    private readonly WNDPROC? _customWndProc;

    private readonly DispatcherQueueTimer _updateLayoutDebouncer;
    private readonly DispatcherQueueTimer _updateTaskbarButtonsTimer;

    private double _lastContentSpace;
    private bool _disposed;

    public TaskbarWindow()
    {
        var serviceProvider = App.Current.Services;
        var viewModel = serviceProvider.GetRequiredService<DockViewModel>();

        _bandsControl = new TaskbarBandControl(viewModel);

        InitializeComponent();

        MainContent.Content = _bandsControl;

        wMTASKBARRESTART = PInvoke.RegisterWindowMessage("TaskbarCreated");

        _hwnd = new HWND(WindowNative.GetWindowHandle(this));

        _updateLayoutDebouncer = DispatcherQueue.CreateTimer();

        _updateTaskbarButtonsTimer = DispatcherQueue.CreateTimer();
        _updateTaskbarButtonsTimer.Tick += (s, e) => ClipWindow(onlyIfButtonsChanged: true).ConfigureAwait(false);
        _updateTaskbarButtonsTimer.Interval = TimeSpan.FromMilliseconds(500);
        _updateTaskbarButtonsTimer.Start();

        // MainContent.SizeChanged += MainContent_SizeChanged;
        WeakReferenceMessenger.Default.Register<QuitMessage>(this);

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

        _ = PInvoke.SetWindowRgn(_hwnd, HRGN.Null, true);

        PInvoke.SetWindowPos(
            _hwnd,
            HWND.Null,
            newWindowRect.left,
            newWindowRect.top,
            newWindowRect.Width,
            newWindowRect.Height,
            SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

        ClipWindow().ConfigureAwait(false);
    }

    private bool UpdateTaskbarButtons()
    {
        var scaleFactor = PInvoke.GetDpiForWindow(_hwnd) / 96.0f;

        var buttons = Microsoft.Terminal.UI.Tasklist.GetButtons();
        var maxRightInPixels = 0;
        foreach (var button in buttons)
        {
            var right = button.X + button.Width;
            if (right > maxRightInPixels)
            {
                maxRightInPixels = right;
            }
        }

        var maxRightDips = maxRightInPixels / scaleFactor;
        TaskbarButtons.Width = new GridLength(maxRightDips);

        // Measure notification/tray area
        var taskBarHwnd = PInvoke.FindWindow("Shell_TrayWnd", null);
        var notificationHwnd = PInvoke.FindWindowEx(taskBarHwnd, HWND.Null, "TrayNotifyWnd", null);
        PInvoke.GetWindowRect(notificationHwnd, out var trayRect);

        var notificationAreaInDips = trayRect.Width / scaleFactor;
        TrayIcons.Width = new GridLength(notificationAreaInDips);

        var available = this.Bounds.Width;
        var taskbarReservedInDips = WindowsLogo.Width.Value + maxRightDips;
        var forContent = available - taskbarReservedInDips - notificationAreaInDips;

        if (_lastContentSpace == forContent)
        {
            _bandsControl.SetMaxAvailableWidth(forContent);
            return false;
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
        return true;
    }

    private async Task ClipWindow(bool onlyIfButtonsChanged = false)
    {
        var taskbarChanged = UpdateTaskbarButtons();
        if (onlyIfButtonsChanged && !taskbarChanged)
        {
            return;
        }

        await Task.Delay(100);
        var scaleFactor = PInvoke.GetDpiForWindow(_hwnd) / 96.0f;
        FrameworkElement clipToElement = MainContent;
        var position = clipToElement.TransformToVisual(this.Content).TransformPoint(default);
        RECT scaledBounds = new()
        {
            left = (int)(position.X * scaleFactor),
            top = (int)(position.Y * scaleFactor),
            right = (int)((position.X + clipToElement.ActualWidth) * scaleFactor),
            bottom = (int)((position.Y + clipToElement.ActualHeight) * scaleFactor),
        };

        var hrgn = PInvoke.CreateRectRgn(
            scaledBounds.left,
            scaledBounds.top,
            scaledBounds.right,
            scaledBounds.bottom);
        _ = PInvoke.SetWindowRgn(_hwnd, hrgn, true);
    }

    public void Receive(QuitMessage message)
    {
        _updateLayoutDebouncer?.Stop();
        _updateTaskbarButtonsTimer?.Stop();
        DispatcherQueue.TryEnqueue(() => Close());
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _updateLayoutDebouncer?.Stop();
            _updateTaskbarButtonsTimer?.Stop();
            WeakReferenceMessenger.Default.UnregisterAll(this);
            _disposed = true;
        }
    }
}
