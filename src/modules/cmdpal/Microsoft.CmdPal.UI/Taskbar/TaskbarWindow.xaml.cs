// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using ManagedCommon;
using Microsoft.CmdPal.UI.Dock;
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
    IRecipient<RequestShowPaletteAtMessage>,
    IDisposable
{
    private readonly uint wMTASKBARRESTART;
    private readonly HWND _hwnd;
    private readonly TaskbarMetrics _taskbarMetrics;
    private readonly TaskbarBandControl _bandsControl;

    private readonly WNDPROC? _originalWndProc;
    private readonly WNDPROC? _customWndProc;
    private readonly IThemeService _themeService;

    private readonly DispatcherQueueTimer _updateLayoutDebouncer;
    private readonly DispatcherQueueTimer _autoHidePollTimer;

    private TaskbarWatcher _taskbarWatcher;

    private double _lastContentSpace;
    private int _clipVersion;
    private bool _clipSuspended;
    private bool _disposed;
    private TaskbarEdge _lastKnownEdge;
    private RECT _lastPollTaskbarRect;

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

        // Any close path (toggle-off in settings, QuitMessage, app
        // shutdown, or user-initiated) must fully tear down native
        // resources. Wiring Closed → Dispose guarantees the watcher's
        // OS WinEvent hooks, timers, and event subscriptions are
        // released so nothing fires against the destroyed window.
        Closed += TaskbarWindow_Closed;

        UpdateFrame();

        MainContent.Content = _bandsControl;

        wMTASKBARRESTART = PInvoke.RegisterWindowMessage("TaskbarCreated");

        _hwnd = new HWND(WindowNative.GetWindowHandle(this));
        _bandsControl.OwnerHwnd = (nint)_hwnd;

        // Hide from alt-tab. Must be set early, before WinUI manages
        // the window, to avoid fighting with the framework.
        var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        _ = PInvoke.SetWindowLong(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)(exStyle | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW));

        _updateLayoutDebouncer = DispatcherQueue.CreateTimer();

        _autoHidePollTimer = DispatcherQueue.CreateTimer();
        _autoHidePollTimer.Interval = TimeSpan.FromMilliseconds(100);
        _autoHidePollTimer.Tick += AutoHidePollTick;

        MainContent.SizeChanged += MainContent_SizeChanged;

        WeakReferenceMessenger.Default.Register<QuitMessage>(this);
        WeakReferenceMessenger.Default.Register<EnterEditModeMessage>(this);
        WeakReferenceMessenger.Default.Register<ExitEditModeMessage>(this);
        WeakReferenceMessenger.Default.Register<RequestShowPaletteAtMessage>(this);

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

    private void TaskbarWindow_Closed(object sender, WindowEventArgs args)
    {
        // Fully release native resources on close. Without this, the
        // TaskbarWatcher's WinEvent hooks (and theme/timer callbacks)
        // keep firing against a destroyed window and crash.
        Dispose();
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
        if (_clipSuspended)
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
        if (_disposed || _bandsControl.IsEditMode || _clipSuspended)
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

            // Auto-hide taskbars slide off-screen keeping their full
            // height/width. Check the on-screen intersection, not the
            // raw rect dimensions.
            var isTaskbarVisible = IsTaskbarOnScreen(shellTray, taskbarRect);

            if (!isTaskbarVisible)
            {
                PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_HIDE);

                // Win11's auto-hide slide-in uses composition animations
                // that may not fire EVENT_OBJECT_LOCATIONCHANGE. Poll
                // until the taskbar reappears and settles.
                _lastPollTaskbarRect = default;
                _autoHidePollTimer.Start();
                return;
            }
            else if (!PInvoke.IsWindowVisible(_hwnd))
            {
                // Event-driven path detected the taskbar reappearing.
                // Show immediately but let the poll timer keep running
                // to follow the slide-in animation and reposition.
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
    /// Polling fallback for auto-hide. Runs every 100ms while our window
    /// is hidden (waiting for the taskbar to reappear) and continues
    /// during the slide-in animation to reposition our window as the
    /// taskbar moves. Stops once the taskbar rect stabilizes.
    /// </summary>
    private void AutoHidePollTick(DispatcherQueueTimer sender, object args)
    {
        if (_disposed)
        {
            sender.Stop();
            return;
        }

        var shellTray = PInvoke.FindWindow("Shell_TrayWnd", null);
        if (shellTray.IsNull)
        {
            return;
        }

        PInvoke.GetWindowRect(shellTray, out var taskbarRect);

        if (!IsTaskbarOnScreen(shellTray, taskbarRect))
        {
            // Still hidden — keep polling, reset stabilization tracking.
            _lastPollTaskbarRect = default;
            return;
        }

        // Taskbar is at least partially on-screen — show if hidden.
        if (!PInvoke.IsWindowVisible(_hwnd))
        {
            PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        }

        // Reposition to follow the slide-in animation.
        MoveToTaskbar();

        // Stop once the taskbar rect has stabilized (animation complete).
        if (taskbarRect.left == _lastPollTaskbarRect.left &&
            taskbarRect.top == _lastPollTaskbarRect.top &&
            taskbarRect.right == _lastPollTaskbarRect.right &&
            taskbarRect.bottom == _lastPollTaskbarRect.bottom)
        {
            _autoHidePollTimer.Stop();
        }

        _lastPollTaskbarRect = taskbarRect;
    }

    /// <summary>
    /// Checks whether enough of the taskbar rect is on-screen to be
    /// considered visible. Auto-hide taskbars slide off-screen keeping
    /// their full size, so raw rect dimensions are unreliable — we
    /// compute the intersection with the monitor work area instead.
    /// </summary>
    private static bool IsTaskbarOnScreen(HWND taskbarHwnd, RECT taskbarRect)
    {
        var monitor = PInvoke.MonitorFromWindow(taskbarHwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        PInvoke.GetMonitorInfo(monitor, ref monitorInfo);
        var screen = monitorInfo.rcMonitor;

        // Compute the on-screen intersection. Auto-hidden taskbar keeps
        // its full 40-60px height/width but slides so only ≤2px are
        // visible on the screen edge.
        var onScreenWidth = Math.Max(0, Math.Min(taskbarRect.right, screen.right) - Math.Max(taskbarRect.left, screen.left));
        var onScreenHeight = Math.Max(0, Math.Min(taskbarRect.bottom, screen.bottom) - Math.Max(taskbarRect.top, screen.top));

        var isHorizontal = taskbarRect.Width >= taskbarRect.Height;
        return isHorizontal ? onScreenHeight > 4 : onScreenWidth > 4;
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
        if (uMsg == PInvoke.WM_DISPLAYCHANGE || uMsg == PInvoke.WM_DPICHANGED)
        {
            // Display resolution or DPI changed — reposition and reclip.
            // DPI changes invalidate clip regions (physical pixels) and
            // may not trigger SPI_SETWORKAREA on all OS versions.
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
            Logger.LogDebug("TaskbarWindow: explorer restarted — reconnecting");
            DispatcherQueue.TryEnqueue(() => ReconnectAfterExplorerRestart());
        }

        return PInvoke.CallWindowProc(_originalWndProc, hwnd, uMsg, wParam, lParam);
    }

    /// <summary>
    /// Handles explorer.exe restart by re-creating the TaskbarWatcher
    /// (old hooks target the dead PID) and repositioning the window
    /// onto the new taskbar. The window stays alive — no close/recreate.
    /// </summary>
    private void ReconnectAfterExplorerRestart()
    {
        // Dispose the old watcher — its hooks target the old explorer PID.
        _taskbarWatcher.Changed -= OnTaskbarChanged;
        _taskbarWatcher.Dispose();

        // Create a fresh watcher scoped to the new explorer.exe PID.
        _taskbarWatcher = new TaskbarWatcher();
        _taskbarWatcher.Changed += OnTaskbarChanged;

        // Reset layout state so MoveToTaskbar does a full recalculation.
        _lastContentSpace = 0;
        _lastKnownEdge = _taskbarMetrics.Edge;

        MoveToTaskbar();
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

        Logger.LogDebug($"MoveToTaskbar: taskbar=({taskbarRect.left},{taskbarRect.top},{taskbarRect.right},{taskbarRect.bottom}) rebar=({reBarRect.left},{reBarRect.top},{reBarRect.right},{reBarRect.bottom}) rebarExists={!reBarWindow.IsNull}");

        // If the rebar doesn't exist, fall back to the taskbar rect.
        if (reBarWindow.IsNull)
        {
            reBarRect = taskbarRect;
        }
        else
        {
            // Clamp the rebar rect to the taskbar boundaries. On compact
            // vertical taskbars, the rebar can extend beyond the visible
            // taskbar area (e.g. 170px wide rebar on a 60px taskbar).
            reBarRect.left = Math.Max(reBarRect.left, taskbarRect.left);
            reBarRect.top = Math.Max(reBarRect.top, taskbarRect.top);
            reBarRect.right = Math.Min(reBarRect.right, taskbarRect.right);
            reBarRect.bottom = Math.Min(reBarRect.bottom, taskbarRect.bottom);
        }

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

        // Apply compact mode based on taskbar settings and dimensions.
        ApplyCompactMode();

        // Reset layout immediately so the XAML state matches
        // the new orientation before ClipWindow runs asynchronously.
        {
            var scaleFactor = PInvoke.GetDpiForWindow(_hwnd) / 96.0f;
            var trayInDips = _taskbarMetrics.TrayWidthInPixels / scaleFactor;
            var forContent = _taskbarMetrics.IsHorizontal
                ? (newWindowRect.Width / (double)scaleFactor) - trayInDips
                : newWindowRect.Width / (double)scaleFactor;
            ApplyGridLayout(_taskbarMetrics.IsHorizontal, 0, trayInDips, forContent);
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
                    // Clip to the overlap between our window and the taskbar.
                    // The window may start below the taskbar's top (rebar offset).
                    var clipTop = Math.Max(0, taskbarRect.top - newWindowRect.top);
                    var clipH = Math.Min(newWindowRect.Height, taskbarRect.bottom - newWindowRect.top);
                    var hrgn = PInvoke.CreateRectRgn(clipLeft, clipTop, clipRight, clipH);
                    _ = PInvoke.SetWindowRgn(_hwnd, hrgn, true);
                }
            }
            else
            {
                var clipTop = _taskbarMetrics.ButtonsWidthInPixels;
                var clipBottom = newWindowRect.Height - _taskbarMetrics.TrayWidthInPixels;

                // WinUI enforces minimum window width, so clip to actual
                // taskbar width for narrow vertical taskbars.
                var taskbarCross = _taskbarMetrics.RebarThicknessInPixels;
                var clipRight = taskbarCross > 0 ? Math.Min(newWindowRect.Width, taskbarCross) : newWindowRect.Width;

                if (clipTop > clipBottom)
                {
                    clipTop = Math.Max(0, clipBottom);
                }

                if (clipBottom > clipTop)
                {
                    var hrgn = PInvoke.CreateRectRgn(0, clipTop, clipRight, clipBottom);
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
            dispatcher.TryEnqueue(() =>
            {
                _bandsControl.SetMaxAvailableWidth(_lastContentSpace);
                ApplyCompactMode();
            });
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

                    forContent = available - buttonsInDips - trayInDips;
                }
                else
                {
                    forContent = winRect.Width / (double)scaleFactor;
                }

                if (_lastContentSpace == forContent)
                {
                    // Even if content space didn't change, ensure the
                    // grid layout matches the current orientation.
                    ApplyGridLayout(isHorizontal, buttonsInDips, trayInDips, forContent);
                    ApplyCompactMode();
                    tcs.TrySetResult(false);
                    return;
                }

                ApplyGridLayout(isHorizontal, buttonsInDips, trayInDips, forContent);

                // Reapply compact mode — metrics may have changed.
                ApplyCompactMode();

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
                    // The window may start below the taskbar's top edge (rebar
                    // offset) and WinUI may enforce a minimum height. Clip to
                    // the actual overlap between our window and the taskbar.
                    var taskbarHwnd = PInvoke.FindWindow("Shell_TrayWnd", null);
                    PInvoke.GetWindowRect(taskbarHwnd, out var tbRect);

                    // Window-relative: how much of our window overlaps the taskbar
                    var overlapTop = Math.Max(0, tbRect.top - winRect.top);
                    var overlapBottom = Math.Max(0, tbRect.bottom - winRect.top);

                    clipLeft = 0;
                    clipTop = overlapTop;
                    clipRight = winRect.Width - _taskbarMetrics.TrayWidthInPixels;
                    clipBottom = Math.Min(winRect.Height, overlapBottom);

                    FrameworkElement clipToElement = MainContent;
                    if (!_bandsControl.IsEditMode &&
                        clipToElement.ActualWidth > 0 && clipToElement.ActualHeight > 0)
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
                    // WinUI enforces a minimum window width (~170px) that
                    // can exceed the taskbar width (e.g. 60px compact).
                    // Clip to the actual taskbar width so the excess is hidden.
                    var taskbarCrossAxis = _taskbarMetrics.RebarThicknessInPixels;
                    clipLeft = 0;
                    clipTop = 0;
                    clipRight = taskbarCrossAxis > 0 ? Math.Min(winRect.Width, taskbarCrossAxis) : winRect.Width;
                    clipBottom = winRect.Height - _taskbarMetrics.TrayWidthInPixels;

                    FrameworkElement clipToElement = MainContent;
                    if (!_bandsControl.IsEditMode &&
                        clipToElement.ActualWidth > 0 && clipToElement.ActualHeight > 0)
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
        _autoHidePollTimer?.Stop();
        DispatcherQueue.TryEnqueue(() => Close());
    }

    public void Receive(EnterEditModeMessage message)
    {
        // Don't freeze the clip — expand it to the full taskbar space.
        // ClipWindow checks IsEditMode and skips content-based narrowing.
        _clipSuspended = false;
        _updateLayoutDebouncer?.Stop();
        ClipWindow().ConfigureAwait(false);
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
                    if (buttonsInDips + trayInDips > available)
                    {
                        buttonsInDips = 0;
                    }

                    forContent = available - buttonsInDips - trayInDips;
                }
                else
                {
                    forContent = winRect.Width / (double)scaleFactor;
                }

                ApplyGridLayout(isHorizontal, buttonsInDips, trayInDips, forContent);
                ApplyCompactMode();

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
        if (_disposed)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_disposed)
            {
                ApplySystemTheme();
            }
        });
    }

    void IRecipient<RequestShowPaletteAtMessage>.Receive(RequestShowPaletteAtMessage message)
    {
        // Only handle messages targeting this taskbar window.
        if (_disposed || message.OwnerHwnd != (nint)_hwnd)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => RequestShowPaletteOnUiThread(message.PosDips));
    }

    private void RequestShowPaletteOnUiThread(global::Windows.Foundation.Point posDips)
    {
        // Convert click position from root-relative DIPs to screen pixels.
        var rootPosDips = MainContent.TransformToVisual(null).TransformPoint(new global::Windows.Foundation.Point(0, 0));
        var screenPosDips = new global::Windows.Foundation.Point(rootPosDips.X + posDips.X, rootPosDips.Y + posDips.Y);

        var dpi = PInvoke.GetDpiForWindow(_hwnd);
        var scaleFactor = dpi / 96.0;
        var screenPosPixels = new global::Windows.Foundation.Point(screenPosDips.X * scaleFactor, screenPosDips.Y * scaleFactor);

        var screenWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
        var screenHeight = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);

        var onTopHalf = screenPosPixels.Y < screenHeight / 2;
        var onLeftHalf = screenPosPixels.X < screenWidth / 2;

        // Map TaskbarEdge → AnchorPoint (mirrors DockWindow logic for DockSide).
        var edge = _taskbarMetrics.Edge;
        var anchorPoint = edge switch
        {
            TaskbarEdge.Top => onLeftHalf ? AnchorPoint.TopLeft : AnchorPoint.TopRight,
            TaskbarEdge.Bottom => onLeftHalf ? AnchorPoint.BottomLeft : AnchorPoint.BottomRight,
            TaskbarEdge.Left => onTopHalf ? AnchorPoint.TopLeft : AnchorPoint.BottomLeft,
            TaskbarEdge.Right => onTopHalf ? AnchorPoint.TopRight : AnchorPoint.BottomRight,
            _ => AnchorPoint.TopLeft,
        };

        // Offset the position away from the taskbar edge.
        var paddingDips = 8;
        var paddingPixels = paddingDips * scaleFactor;
        PInvoke.GetWindowRect(_hwnd, out var ourRect);

        switch (edge)
        {
            case TaskbarEdge.Top:
                screenPosPixels = new global::Windows.Foundation.Point(screenPosPixels.X, ourRect.bottom + paddingPixels);
                break;
            case TaskbarEdge.Bottom:
                screenPosPixels = new global::Windows.Foundation.Point(screenPosPixels.X, ourRect.top - paddingPixels);
                break;
            case TaskbarEdge.Left:
                screenPosPixels = new global::Windows.Foundation.Point(ourRect.right + paddingPixels, screenPosPixels.Y);
                break;
            case TaskbarEdge.Right:
                screenPosPixels = new global::Windows.Foundation.Point(ourRect.left - paddingPixels, screenPosPixels.Y);
                break;
        }

        WeakReferenceMessenger.Default.Send<ShowPaletteAtMessage>(new(screenPosPixels, anchorPoint));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _themeService.ThemeChanged -= ThemeService_ThemeChanged;
            _updateLayoutDebouncer?.Stop();
            _autoHidePollTimer?.Stop();
            _taskbarWatcher.Changed -= OnTaskbarChanged;
            _taskbarWatcher.Dispose();
            _taskbarMetrics.Dispose();
            WeakReferenceMessenger.Default.UnregisterAll(this);
            _disposed = true;
        }
    }

    /// <summary>
    /// Switches the root Grid between column-based (horizontal) and
    /// row-based (vertical) layout, positions MainContent accordingly,
    /// and sets the available content space on the band control.
    /// </summary>
    private void ApplyGridLayout(bool isHorizontal, double buttonsInDips, double trayInDips, double forContent)
    {
        if (isHorizontal)
        {
            // Columns active: Logo | Buttons | Mid(*) | Content(Auto) | Tray
            WindowsLogoCol.Width = new GridLength(0);
            ButtonsCol.Width = new GridLength(Math.Max(0, buttonsInDips - WindowsLogoCol.Width.Value));
            MidCol.Width = new GridLength(1, GridUnitType.Star);
            ContentCol.Width = GridLength.Auto;
            TrayCol.Width = new GridLength(trayInDips);

            // Collapse rows — single effective row
            ButtonsRow.Height = new GridLength(0);
            MidRow.Height = new GridLength(0);
            ContentRow.Height = new GridLength(1, GridUnitType.Star);
            TrayRow.Height = new GridLength(0);

            // MainContent in its column, spanning all rows
            Microsoft.UI.Xaml.Controls.Grid.SetColumn(MainContent, 3);
            Microsoft.UI.Xaml.Controls.Grid.SetColumnSpan(MainContent, 1);
            Microsoft.UI.Xaml.Controls.Grid.SetRow(MainContent, 0);
            Microsoft.UI.Xaml.Controls.Grid.SetRowSpan(MainContent, 4);
            MainContent.HorizontalAlignment = HorizontalAlignment.Right;
            MainContent.VerticalAlignment = VerticalAlignment.Center;
            MainContent.Margin = new Thickness(0);

            if (forContent > 0)
            {
                ContentCol.MaxWidth = _bandsControl.IsEditMode
                    ? forContent
                    : (Root.ActualWidth == 0 ? double.MaxValue : forContent);
                ContentCol.Width = GridLength.Auto;
                _bandsControl.SetMaxAvailableWidth(forContent);
            }
            else
            {
                ContentCol.MaxWidth = 0;
                ContentCol.Width = new GridLength(0);
                _bandsControl.SetMaxAvailableWidth(0);
            }
        }
        else
        {
            // Collapse columns — single effective column
            WindowsLogoCol.Width = new GridLength(0);
            ButtonsCol.Width = new GridLength(0);
            MidCol.Width = new GridLength(0);
            ContentCol.Width = new GridLength(1, GridUnitType.Star);
            ContentCol.MaxWidth = double.PositiveInfinity;
            TrayCol.Width = new GridLength(0);

            // Rows active: Buttons(0) | Mid(*) | Content(Auto) | Tray
            ButtonsRow.Height = new GridLength(0);
            MidRow.Height = new GridLength(1, GridUnitType.Star);
            ContentRow.Height = GridLength.Auto;
            TrayRow.Height = new GridLength(trayInDips);

            // MainContent in ContentRow, filling the single column
            Microsoft.UI.Xaml.Controls.Grid.SetColumn(MainContent, 0);
            Microsoft.UI.Xaml.Controls.Grid.SetColumnSpan(MainContent, 5);
            Microsoft.UI.Xaml.Controls.Grid.SetRow(MainContent, 2);
            Microsoft.UI.Xaml.Controls.Grid.SetRowSpan(MainContent, 1);
            MainContent.HorizontalAlignment = HorizontalAlignment.Stretch;
            MainContent.VerticalAlignment = VerticalAlignment.Bottom;
            MainContent.Margin = new Thickness(0);

            _bandsControl.SetMaxAvailableWidth(forContent);
        }

        _lastContentSpace = forContent;
    }

    /// <summary>
    /// Determines whether the taskbar is in compact mode and whether
    /// text should be hidden, then propagates to the band control.
    /// </summary>
    private void ApplyCompactMode()
    {
        var isCompact = _taskbarMetrics.IsCompact;
        var hideText = false;

        if (!_taskbarMetrics.IsHorizontal)
        {
            // For vertical taskbars, detect narrow/icon-only mode by
            // measuring the rebar width. If the rebar is narrow,
            // hide text and force compact.
            var scaleFactor = PInvoke.GetDpiForWindow(_hwnd) / 96.0f;
            var rebarWidthDips = _taskbarMetrics.RebarThicknessInPixels / scaleFactor;

            // Threshold: a vertical taskbar in icon-only mode is typically
            // ~62px at 100% DPI. With labels, it's ~150px+.
            if (rebarWidthDips < 90)
            {
                hideText = true;
                isCompact = true;
            }
        }

        Logger.LogDebug($"ApplyCompactMode: isCompact={isCompact} hideText={hideText} rebarPx={_taskbarMetrics.RebarThicknessInPixels} edge={_taskbarMetrics.Edge}");
        _bandsControl.SetCompactMode(isCompact, hideText);

        // When compact, WinUI enforces a minimum window size larger than
        // the taskbar. The clip region starts at (0,0) in window-relative
        // coords, so anchor the Root Grid to the top-left corner so content
        // aligns with the visible clip area.
        if (isCompact)
        {
            if (_taskbarMetrics.IsHorizontal)
            {
                Root.VerticalAlignment = VerticalAlignment.Top;
                Root.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
            else
            {
                Root.VerticalAlignment = VerticalAlignment.Stretch;
                Root.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }
        else
        {
            Root.VerticalAlignment = VerticalAlignment.Stretch;
            Root.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
    }
}
