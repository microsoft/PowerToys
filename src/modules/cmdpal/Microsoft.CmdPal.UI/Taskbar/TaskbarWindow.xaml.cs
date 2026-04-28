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
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;
using WinUIEx;

namespace Microsoft.CmdPal.UI.Taskbar;

public sealed partial class TaskbarWindow : WindowEx,
    IRecipient<QuitMessage>,
    IRecipient<EnterEditModeMessage>,
    IRecipient<ExitEditModeMessage>,
    IDisposable
{
    private readonly uint wMTASKBARRESTART;
    private readonly HWND _hwnd;
    private readonly TaskbarMetrics _taskbarMetrics;
    private readonly TaskbarWatcher _taskbarWatcher;
    private readonly TaskbarBandControl _bandsControl;

    private readonly WNDPROC? _originalWndProc;
    private readonly WNDPROC? _customWndProc;
    private readonly IThemeService _themeService;

    private readonly DispatcherQueueTimer _updateLayoutDebouncer;

    private double _lastContentSpace;
    private int _clipVersion;
    private bool _clipSuspended;
    private bool _disposed;
    private TaskbarEdge _lastKnownEdge;

    internal TaskbarWindow(TaskbarMetrics metrics)
    {
        var serviceProvider = App.Current.Services;
        var viewModel = serviceProvider.GetRequiredService<DockViewModel>();
        _themeService = serviceProvider.GetRequiredService<IThemeService>();

        _bandsControl = new TaskbarBandControl(viewModel);

        InitializeComponent();

        ApplySystemTheme();
        _themeService.ThemeChanged += ThemeService_ThemeChanged;

        Activated += TaskbarWindow_Activated;
        UpdateFrame();

        MainContent.Content = _bandsControl;

        wMTASKBARRESTART = PInvoke.RegisterWindowMessage("TaskbarCreated");

        _hwnd = new HWND(WindowNative.GetWindowHandle(this));

        // Hide from alt-tab. Must be set early, before WinUI manages
        // the window, to avoid fighting with the framework.
        var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        _ = PInvoke.SetWindowLong(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)(exStyle | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW));

        _updateLayoutDebouncer = DispatcherQueue.CreateTimer();

        MainContent.SizeChanged += MainContent_SizeChanged;

        WeakReferenceMessenger.Default.Register<QuitMessage>(this);
        WeakReferenceMessenger.Default.Register<EnterEditModeMessage>(this);
        WeakReferenceMessenger.Default.Register<ExitEditModeMessage>(this);

        _taskbarMetrics = metrics;

        // Event-driven: re-measure when taskbar buttons change instead
        // of polling. The WinEventHook callback is delivered via the
        // WinUI message pump on this thread.
        _taskbarWatcher = new TaskbarWatcher();
        _taskbarWatcher.Changed += OnTaskbarChanged;

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

    private void TaskbarWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        UpdateFrame();
    }

    private void UpdateFrame()
    {
        // These are used for removing the very subtle shadow/border that we get from Windows 11
        HwndExtensions.ToggleWindowStyle(_hwnd, false, WindowStyle.TiledWindow);
        unsafe
        {
            BOOL value = false;
            PInvoke.DwmSetWindowAttribute(_hwnd, DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, &value, (uint)sizeof(BOOL));
        }
    }

    private void MainContent_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_bandsControl.IsEditMode || _clipSuspended)
        {
            return;
        }

        _updateLayoutDebouncer.Debounce(
            () => ClipWindow().ConfigureAwait(false),
            interval: TimeSpan.FromMilliseconds(100),
            immediate: false);
    }

    private void OnTaskbarChanged()
    {
        if (_bandsControl.IsEditMode || _clipSuspended)
        {
            return;
        }

        var shellTray = PInvoke.FindWindow("Shell_TrayWnd", null);
        if (!shellTray.IsNull)
        {
            PInvoke.GetWindowRect(shellTray, out var taskbarRect);

            // Detect the current edge cheaply (no UIA/COM) by comparing
            // the taskbar rect to the monitor rect.
            var currentEdge = DetectEdgeFromRect(shellTray, taskbarRect);
            var isHorizontal = currentEdge is TaskbarEdge.Bottom or TaskbarEdge.Top;

            var isTaskbarVisible = isHorizontal
                ? taskbarRect.Height > 2
                : taskbarRect.Width > 2;

            if (!isTaskbarVisible)
            {
                PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_HIDE);
                return;
            }
            else if (!PInvoke.IsWindowVisible(_hwnd))
            {
                PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
                MoveToTaskbar();
                return;
            }

            // If the taskbar moved to a different edge, reposition now.
            if (currentEdge != _lastKnownEdge)
            {
                _lastKnownEdge = currentEdge;
                _lastContentSpace = 0; // force layout recalculation
                MoveToTaskbar();
                return;
            }

            // Re-assert topmost so we stay above the taskbar even after
            // the user clicks on it (both windows are HWND_TOPMOST;
            // whichever is activated last goes on top within that band).
            PInvoke.SetWindowPos(
                _hwnd,
                HWND.HWND_TOPMOST,
                0,
                0,
                0,
                0,
                SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        }

        _updateLayoutDebouncer.Debounce(
            () => ClipWindow().ConfigureAwait(false),
            interval: TimeSpan.FromMilliseconds(250),
            immediate: false);
    }

    /// <summary>
    /// Lightweight edge detection using only Win32 rect comparisons.
    /// No UIA/COM — safe to call on the UI thread in hot paths.
    /// </summary>
    private static TaskbarEdge DetectEdgeFromRect(HWND taskbarHwnd, RECT taskbarRect)
    {
        var monitor = PInvoke.MonitorFromWindow(taskbarHwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        PInvoke.GetMonitorInfo(monitor, ref monitorInfo);
        var screen = monitorInfo.rcMonitor;

        if (taskbarRect.Width >= screen.Width)
        {
            return taskbarRect.top <= screen.top ? TaskbarEdge.Top : TaskbarEdge.Bottom;
        }
        else
        {
            return taskbarRect.left <= screen.left ? TaskbarEdge.Left : TaskbarEdge.Right;
        }
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
                // Work area changed — taskbar moved, resized, or changed
                // edge. Call MoveToTaskbar directly (not debounced) so
                // the window repositions immediately.
                DispatcherQueue.TryEnqueue(() => MoveToTaskbar());
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

        // No parent/child/owner relationship — fully independent window.
        // IsAlwaysOnTop (WS_EX_TOPMOST) keeps us above the taskbar.
        // Auto-hide sync via TaskbarWatcher + EVENT_OBJECT_LOCATIONCHANGE.
        PInvoke.GetWindowRect(taskbarWindow, out var taskbarRect);
        PInvoke.GetWindowRect(reBarWindow, out var reBarRect);

        RECT newWindowRect;
        if (_taskbarMetrics.IsHorizontal)
        {
            // Horizontal: span full taskbar width, use rebar height.
            newWindowRect.left = taskbarRect.left;
            newWindowRect.top = reBarRect.top;
            newWindowRect.right = taskbarRect.right;
            newWindowRect.bottom = reBarRect.bottom;
        }
        else
        {
            // Vertical: span full taskbar height, use rebar width.
            newWindowRect.left = reBarRect.left;
            newWindowRect.top = taskbarRect.top;
            newWindowRect.right = reBarRect.right;
            newWindowRect.bottom = taskbarRect.bottom;
        }

        // Don't clear the window region — leave the previous clip in
        // place until ClipWindow applies the updated one. Clearing it
        // would flash the window across the full taskbar width.
        PInvoke.SetWindowPos(
            _hwnd,
            HWND.HWND_TOPMOST,
            newWindowRect.left,
            newWindowRect.top,
            newWindowRect.Width,
            newWindowRect.Height,
            SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

        // Detect which screen edge the taskbar is on and set the
        // teaching tip placement to the opposite direction.
        var monitorHandle = PInvoke.MonitorFromWindow(_hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        PInvoke.GetMonitorInfo(monitorHandle, ref monitorInfo);
        var screen = monitorInfo.rcMonitor;

        if (taskbarRect.top <= screen.top)
        {
            _bandsControl.SetTeachingTipPlacement(Microsoft.UI.Xaml.Controls.TeachingTipPlacementMode.Bottom);
        }
        else if (taskbarRect.bottom >= screen.bottom)
        {
            _bandsControl.SetTeachingTipPlacement(Microsoft.UI.Xaml.Controls.TeachingTipPlacementMode.Top);
        }
        else if (taskbarRect.left <= screen.left)
        {
            _bandsControl.SetTeachingTipPlacement(Microsoft.UI.Xaml.Controls.TeachingTipPlacementMode.Right);
        }
        else
        {
            _bandsControl.SetTeachingTipPlacement(Microsoft.UI.Xaml.Controls.TeachingTipPlacementMode.Left);
        }

        // Switch band layout orientation to match the taskbar edge.
        _bandsControl.SetOrientation(_taskbarMetrics.IsHorizontal
            ? Microsoft.UI.Xaml.Controls.Orientation.Horizontal
            : Microsoft.UI.Xaml.Controls.Orientation.Vertical);

        // Reset MainContent layout immediately so the XAML state matches
        // the new orientation before ClipWindow runs asynchronously.
        {
            var scaleFactor = PInvoke.GetDpiForWindow(_hwnd) / 96.0f;
            var trayInDips = _taskbarMetrics.TrayWidthInPixels / scaleFactor;

            if (_taskbarMetrics.IsHorizontal)
            {
                Microsoft.UI.Xaml.Controls.Grid.SetColumn(MainContent, 3);
                Microsoft.UI.Xaml.Controls.Grid.SetColumnSpan(MainContent, 1);
                MainContent.HorizontalAlignment = HorizontalAlignment.Right;
                MainContent.VerticalAlignment = VerticalAlignment.Center;
                MainContent.Margin = new Thickness(0);
                MainContent.MaxHeight = double.PositiveInfinity;
            }
            else
            {
                Microsoft.UI.Xaml.Controls.Grid.SetColumn(MainContent, 0);
                Microsoft.UI.Xaml.Controls.Grid.SetColumnSpan(MainContent, 5);
                MainContent.HorizontalAlignment = HorizontalAlignment.Center;
                MainContent.VerticalAlignment = VerticalAlignment.Bottom;
                MainContent.Margin = new Thickness(0, 0, 0, trayInDips);
                MainContent.MaxHeight = double.PositiveInfinity;
            }
        }

        // Apply an immediate clip using pre-loaded metrics so the window
        // never flashes across the full taskbar width. The async ClipWindow
        // call will refine this once XAML layout has settled.
        if (_taskbarMetrics.ButtonsWidthInPixels > 0 || _taskbarMetrics.TrayWidthInPixels > 0)
        {
            if (_taskbarMetrics.IsHorizontal)
            {
                var clipLeft = _taskbarMetrics.ButtonsWidthInPixels;
                var clipRight = newWindowRect.Width - _taskbarMetrics.TrayWidthInPixels;

                if (clipLeft > clipRight)
                {
                    clipLeft = Math.Max(0, clipRight);
                }

                if (clipRight > clipLeft)
                {
                    var hrgn = PInvoke.CreateRectRgn(clipLeft, 0, clipRight, newWindowRect.Height);
                    _ = PInvoke.SetWindowRgn(_hwnd, hrgn, true);
                }
            }
            else
            {
                var clipTop = _taskbarMetrics.ButtonsWidthInPixels;
                var clipBottom = newWindowRect.Height - _taskbarMetrics.TrayWidthInPixels;

                if (clipTop > clipBottom)
                {
                    clipTop = Math.Max(0, clipBottom);
                }

                if (clipBottom > clipTop)
                {
                    var hrgn = PInvoke.CreateRectRgn(0, clipTop, newWindowRect.Width, clipBottom);
                    _ = PInvoke.SetWindowRgn(_hwnd, hrgn, true);
                }
            }
        }

        ClipWindow().ConfigureAwait(false);
        _lastKnownEdge = _taskbarMetrics.Edge;
    }

    private async Task<bool> UpdateTaskbarButtonsAsync()
    {
        // Capture dispatcher before crossing to a background thread.
        var dispatcher = DispatcherQueue;

        // Run UIA enumeration on a background thread.
        var changed = await _taskbarMetrics.UpdateAsync();

        // On the very first call _lastContentSpace is 0 and the grid
        // columns haven't been set yet. Always apply layout in that case,
        // even if the metrics didn't change (they were pre-loaded).
        if (!changed && _lastContentSpace != 0)
        {
            dispatcher.TryEnqueue(() => _bandsControl.SetMaxAvailableWidth(_lastContentSpace));
            return false;
        }

        // Capture values from the background result.
        var buttonsPixels = _taskbarMetrics.ButtonsWidthInPixels;
        var trayPixels = _taskbarMetrics.TrayWidthInPixels;

        var tcs = new TaskCompletionSource<bool>();
        dispatcher.TryEnqueue(() =>
        {
            try
            {
                var scaleFactor = PInvoke.GetDpiForWindow(_hwnd) / 96.0f;
                var isHorizontal = _taskbarMetrics.IsHorizontal;

                double buttonsInDips = buttonsPixels / scaleFactor;
                var trayInDips = trayPixels / scaleFactor;

                PInvoke.GetWindowRect(_hwnd, out var winRect);

                double forContent;
                if (isHorizontal)
                {
                    var available = winRect.Width / (double)scaleFactor;

                    // If buttons + tray exceed available (UIA over-reports),
                    // the button measurement is unreliable. Set the buttons
                    // column to 0 so content fills all non-tray space.
                    if (buttonsInDips + trayInDips > available)
                    {
                        buttonsInDips = 0;
                    }

                    TaskbarButtons.Width = new GridLength(Math.Max(0, buttonsInDips - WindowsLogo.Width.Value));
                    TrayIcons.Width = new GridLength(trayInDips);

                    // Restore horizontal layout properties.
                    Microsoft.UI.Xaml.Controls.Grid.SetColumn(MainContent, 3);
                    Microsoft.UI.Xaml.Controls.Grid.SetColumnSpan(MainContent, 1);
                    MainContent.HorizontalAlignment = HorizontalAlignment.Right;
                    MainContent.VerticalAlignment = VerticalAlignment.Center;
                    MainContent.Margin = new Thickness(0);
                    MainContent.MaxHeight = double.PositiveInfinity;

                    forContent = available - buttonsInDips - trayInDips;
                }
                else
                {
                    // Vertical: buttons at top, tray at bottom.
                    // The tray measurement (from TrayNotifyWnd) is reliable.
                    // Button measurement (UIA) often over-reports, so anchor
                    // content from the bottom — just above the tray.
                    TaskbarButtons.Width = new GridLength(0);
                    TrayIcons.Width = new GridLength(0);

                    Microsoft.UI.Xaml.Controls.Grid.SetColumn(MainContent, 0);
                    Microsoft.UI.Xaml.Controls.Grid.SetColumnSpan(MainContent, 5);
                    MainContent.HorizontalAlignment = HorizontalAlignment.Center;
                    MainContent.VerticalAlignment = VerticalAlignment.Bottom;
                    MainContent.Margin = new Thickness(0, 0, 0, trayInDips);
                    MainContent.MaxHeight = double.PositiveInfinity;

                    forContent = winRect.Width / (double)scaleFactor;
                }

                if (_lastContentSpace == forContent)
                {
                    _bandsControl.SetMaxAvailableWidth(forContent);
                    tcs.TrySetResult(false);
                    return;
                }

                if (forContent > 0)
                {
                    // In edit mode, take the full available width so
                    // bands don't collapse while being reordered.
                    if (_bandsControl.IsEditMode)
                    {
                        ContentColumn.MaxWidth = forContent;
                    }
                    else
                    {
                        ContentColumn.MaxWidth = Root.ActualWidth == 0 ? double.MaxValue : forContent;
                    }

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
        // Increment version so that any older in-flight ClipWindow calls
        // will see a stale version and skip applying their clip region.
        var myVersion = Interlocked.Increment(ref _clipVersion);
        var dispatcher = DispatcherQueue;

        var taskbarChanged = await UpdateTaskbarButtonsAsync();
        if (onlyIfButtonsChanged && !taskbarChanged)
        {
            return;
        }

        // Wait for layout to settle.
        await Task.Delay(100);

        // If another ClipWindow was started while we were waiting, bail.
        if (Volatile.Read(ref _clipVersion) != myVersion)
        {
            return;
        }

        var tcs = new TaskCompletionSource();
        dispatcher.TryEnqueue(() =>
        {
            // Check again on the UI thread.
            if (Volatile.Read(ref _clipVersion) != myVersion)
            {
                tcs.TrySetResult();
                return;
            }

            try
            {
                var scaleFactor = PInvoke.GetDpiForWindow(_hwnd) / 96.0f;
                var isHorizontal = _taskbarMetrics.IsHorizontal;

                // Use GetWindowRect for the pixel dimensions — this.Bounds
                // may not be updated yet for standalone (non-child) windows.
                PInvoke.GetWindowRect(_hwnd, out var winRect);

                Logger.LogDebug($"ClipWindow: winRect=({winRect.left},{winRect.top},{winRect.right},{winRect.bottom}) W={winRect.Width} H={winRect.Height}");
                Logger.LogDebug($"ClipWindow: buttons={_taskbarMetrics.ButtonsWidthInPixels}px tray={_taskbarMetrics.TrayWidthInPixels}px scale={scaleFactor} edge={_taskbarMetrics.Edge}");

                int clipLeft, clipTop, clipRight, clipBottom;

                if (isHorizontal)
                {
                    // Horizontal: clip left/right to exclude buttons and tray.
                    clipLeft = 0;
                    clipTop = 0;
                    clipRight = winRect.Width - _taskbarMetrics.TrayWidthInPixels;
                    clipBottom = (int)(Root.ActualHeight * scaleFactor);

                    FrameworkElement clipToElement = MainContent;
                    if (clipToElement.ActualWidth > 0 && clipToElement.ActualHeight > 0)
                    {
                        var position = clipToElement.TransformToVisual(this.Content).TransformPoint(default);
                        var contentLeft = (int)(position.X * scaleFactor);
                        var contentRight = (int)((position.X + clipToElement.ActualWidth) * scaleFactor);

                        Logger.LogDebug($"ClipWindow: MainContent pos=({position.X},{position.Y}) contentLeft={contentLeft} contentRight={contentRight}");

                        clipLeft = contentLeft;
                        clipRight = Math.Min(clipRight, contentRight);
                    }
                }
                else
                {
                    // Vertical: content is bottom-aligned above the tray.
                    // Clip to content's actual position — the tray boundary
                    // is the hard limit at the bottom.
                    clipLeft = 0;
                    clipTop = 0;
                    clipRight = (int)(Root.ActualWidth * scaleFactor);
                    clipBottom = winRect.Height - _taskbarMetrics.TrayWidthInPixels;

                    FrameworkElement clipToElement = MainContent;
                    if (clipToElement.ActualWidth > 0 && clipToElement.ActualHeight > 0)
                    {
                        var position = clipToElement.TransformToVisual(this.Content).TransformPoint(default);
                        var contentTop = (int)(position.Y * scaleFactor);
                        var contentBottom = (int)((position.Y + clipToElement.ActualHeight) * scaleFactor);

                        Logger.LogDebug($"ClipWindow: MainContent pos=({position.X},{position.Y}) contentTop={contentTop} contentBottom={contentBottom}");

                        clipTop = Math.Max(clipTop, contentTop);
                        clipBottom = Math.Min(clipBottom, contentBottom);
                    }
                }

                Logger.LogDebug($"ClipWindow: FINAL clip=({clipLeft},{clipTop},{clipRight},{clipBottom}) → {(clipRight > clipLeft && clipBottom > clipTop ? "VISIBLE" : "ZERO-AREA CLIP")}");

                if (clipRight <= clipLeft || clipBottom <= clipTop)
                {
                    // No space for content — clear the region so the
                    // window stays visible rather than hiding entirely.
                    _ = PInvoke.SetWindowRgn(_hwnd, HRGN.Null, true);
                }
                else
                {
                    var hrgn = PInvoke.CreateRectRgn(clipLeft, clipTop, clipRight, clipBottom);
                    _ = PInvoke.SetWindowRgn(_hwnd, hrgn, true);
                }
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

    public void Receive(EnterEditModeMessage message)
    {
        _clipSuspended = true;
        _updateLayoutDebouncer?.Stop();
    }

    public void Receive(ExitEditModeMessage message)
    {
        // Re-apply layout and clip using cached metrics — no UIA
        // re-enumeration needed. The taskbar buttons/tray haven't
        // changed, only our content did.
        _updateLayoutDebouncer.Debounce(
            () =>
            {
                _clipSuspended = false;

                var scaleFactor = PInvoke.GetDpiForWindow(_hwnd) / 96.0f;
                var isHorizontal = _taskbarMetrics.IsHorizontal;
                double buttonsInDips = _taskbarMetrics.ButtonsWidthInPixels / scaleFactor;
                var trayInDips = _taskbarMetrics.TrayWidthInPixels / scaleFactor;
                PInvoke.GetWindowRect(_hwnd, out var winRect);

                double forContent;
                if (isHorizontal)
                {
                    var available = winRect.Width / (double)scaleFactor;

                    // Same overlap handling as UpdateTaskbarButtonsAsync
                    if (buttonsInDips + trayInDips > available)
                    {
                        buttonsInDips = 0;
                    }

                    forContent = available - buttonsInDips - trayInDips;

                    TaskbarButtons.Width = new GridLength(Math.Max(0, buttonsInDips - WindowsLogo.Width.Value));
                    TrayIcons.Width = new GridLength(trayInDips);

                    Microsoft.UI.Xaml.Controls.Grid.SetColumn(MainContent, 3);
                    Microsoft.UI.Xaml.Controls.Grid.SetColumnSpan(MainContent, 1);
                    MainContent.HorizontalAlignment = HorizontalAlignment.Right;
                    MainContent.VerticalAlignment = VerticalAlignment.Center;
                    MainContent.Margin = new Thickness(0);
                    MainContent.MaxHeight = double.PositiveInfinity;
                }
                else
                {
                    TaskbarButtons.Width = new GridLength(0);
                    TrayIcons.Width = new GridLength(0);

                    Microsoft.UI.Xaml.Controls.Grid.SetColumn(MainContent, 0);
                    Microsoft.UI.Xaml.Controls.Grid.SetColumnSpan(MainContent, 5);
                    MainContent.HorizontalAlignment = HorizontalAlignment.Center;
                    MainContent.VerticalAlignment = VerticalAlignment.Bottom;
                    MainContent.Margin = new Thickness(0, 0, 0, trayInDips);
                    MainContent.MaxHeight = double.PositiveInfinity;

                    forContent = winRect.Width / (double)scaleFactor;
                }

                if (forContent > 0)
                {
                    ContentColumn.MaxWidth = forContent;
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

                // Delegate clip to ClipWindow which handles content-
                // based clipping correctly. Don't duplicate the logic.
                ClipWindow().ConfigureAwait(false);
            },
            interval: TimeSpan.FromMilliseconds(500),
            immediate: false);
    }

    private static ElementTheme GetSystemTheme()
    {
        const string keyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        var value = Registry.GetValue(keyPath, "SystemUsesLightTheme", 1);
        return value is int i && i == 0 ? ElementTheme.Dark : ElementTheme.Light;
    }

    private void ApplySystemTheme()
    {
        var target = GetSystemTheme();

        // LOAD BEARING: Cycling Dark→Light→target forces XAML to refresh.
        Root.RequestedTheme = ElementTheme.Dark;
        Root.RequestedTheme = ElementTheme.Light;
        Root.RequestedTheme = target;
    }

    private void ThemeService_ThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(ApplySystemTheme);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _themeService.ThemeChanged -= ThemeService_ThemeChanged;
            _updateLayoutDebouncer?.Stop();
            _taskbarWatcher.Changed -= OnTaskbarChanged;
            _taskbarWatcher.Dispose();
            _taskbarMetrics.Dispose();
            WeakReferenceMessenger.Default.UnregisterAll(this);
            _disposed = true;
        }
    }
}
