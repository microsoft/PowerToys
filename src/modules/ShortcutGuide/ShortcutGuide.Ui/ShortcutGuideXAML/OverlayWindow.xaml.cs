// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Common.UI;
using CommunityToolkit.WinUI.Animations;
using ManagedCommon;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using ShortcutGuide.Controls;
using ShortcutGuide.Helpers;
using Windows.Foundation;
using Windows.System;
using WinRT.Interop;
using WinUIEx;
using WinUIEx.Messaging;

namespace ShortcutGuide
{
    /// <summary>
    /// Single transparent host that replaces both the previous
    /// <c>MainWindow</c> and <c>TaskbarWindow</c>. It covers the work area
    /// of the cursor's monitor and renders the two surfaces as pseudo-windows
    /// (<see cref="MainPaneControl"/> + <see cref="TaskbarPaneControl"/>)
    /// inside its XAML tree, so they can be coordinated by a single
    /// dispatcher, activate together, and (later) share an animation
    /// timeline.
    /// </summary>
    public sealed partial class OverlayWindow : TransparentWindow
    {
        private readonly Stopwatch _sessionStopwatch = Stopwatch.StartNew();
        private string _closeType = "Unknown";
        private bool _isClosing;

        // Reused one-shot timer that defers Hide() until the close animation
        // finishes, so CloseAnimated() doesn't allocate a timer + closure per call.
        private Microsoft.UI.Dispatching.DispatcherQueueTimer? _closeTimer;

        // Set true around AppWindow.MoveAndResize() so the WndProc swallows
        // WM_DPICHANGED. Without this, WinUIEx re-scales the rect we just
        // wrote by (newDpi / oldDpi), producing a 1.5x/0.66x window on
        // cross-monitor moves between mixed-DPI displays.
        private bool _suppressDpiChange;

        // Kept alive for the lifetime of the window: WindowMessageMonitor only
        // holds the hook while it is reachable, so storing it as a field is
        // required or a GC could silently break WM_DPICHANGED suppression and
        // WM_NCLBUTTONDBLCLK handling.
        private WindowMessageMonitor? _windowMessageMonitor;

        // Screen edge the taskbar is docked to (global Windows setting). Drives
        // the taskbar-indicator layout and the main pane's inset so the order
        // from a left/right taskbar reads taskbar | indicators | pane.
        private TaskbarEdge _taskbarEdge = TaskbarEdge.Bottom;

        internal long SessionDurationMs => _sessionStopwatch.ElapsedMilliseconds;

        internal string CloseType => _closeType;

        public MainPaneControl MainPaneControl => this.MainPane;

        internal TaskbarPaneControl TaskbarPaneControl => this.TaskbarPane;

        public OverlayWindow()
        {
            this.InitializeComponent();

            // The base TransparentWindow already applies the
            // TransparentTintBackdrop, extends content into the title bar and
            // collapses it, and strips the native chrome.
            this.Title = ResourceLoaderInstance.ResourceLoader.GetString("Title");

            // Install the message hook BEFORE the first MoveAndResize so the
            // WM_DPICHANGED suppression is in place from the very first
            // cross-monitor move. Otherwise the constructor's initial
            // RepositionToCursorMonitor() lets the default WndProc auto-
            // resize the window by (newDpi/oldDpi) and we get a 1.5×
            // overlay on the laptop screen. Also disable
            // WM_NCLBUTTONDBLCLK so the OS doesn't try to maximize when
            // the user double-clicks anywhere in the (collapsed) caption
            // area. Matches the original MainWindow behavior.
            WindowMessageMonitor msgMonitor = new(this);
            _windowMessageMonitor = msgMonitor;
            msgMonitor.WindowMessageReceived += (_, e) =>
            {
                const int WM_NCLBUTTONDBLCLK = 0x00A3;
                const int WM_DPICHANGED = 0x02E0;
                if (e.Message.MessageId == WM_NCLBUTTONDBLCLK)
                {
                    e.Result = 0;
                    e.Handled = true;
                    return;
                }

                if (e.Message.MessageId == WM_DPICHANGED && _suppressDpiChange)
                {
                    // We've already written the correctly-scaled physical
                    // rect for the target monitor — let WinUI update its
                    // RasterizationScale, but DO NOT let the default
                    // WndProc resize the window to its suggested rect
                    // (which is the OLD size scaled by newDpi/oldDpi and
                    // is what produced the doubled overlay).
                    e.Result = 0;
                    e.Handled = true;
                }
            };

            // Edge-to-edge overlay: opt into the aggressive full-bleed DWM
            // hardening so the OS never reveals a 1-px frame seam around the
            // monitor-sized transparent window. The baseline chrome is already
            // applied by the base constructor.
            this.ApplyFullBleedHardening();

            // Pre-size and position BEFORE Activate so the first frame the
            // user sees is already at the correct work-area size on the
            // cursor's monitor. Doing this in OnActivated produces a single
            // wrong-sized flash that "snaps" to the right size when focus
            // changes (because a later layout pass picks up the correct
            // AppWindow.Size).
            RepositionToCursorMonitor();
            ApplyMainPaneAlignment();

#if !DEBUG
            this.SetIsAlwaysOnTop(true);
            this.SetIsShownInSwitchers(false);
#endif

            this.Activated += OnActivated;

            // Esc closes the overlay regardless of which pseudo-window has
            // keyboard focus (handled at the Window.Content root because the
            // event bubbles up from whichever inner element has focus).
            if (this.Content is UIElement contentRoot)
            {
                contentRoot.KeyUp += OnContentKeyUp;
            }

            ApplyThemeFromSettings();

            // Reposition when the window's presenter changes (e.g. from a
            // PresenterKind switch). Matches the original MainWindow behavior.
            this.AppWindow.Changed += (_, args) =>
            {
                if (!args.DidPresenterChange)
                {
                    return;
                }

                RepositionToCursorMonitor();
            };

            this.MainPane.SelectedAppTaskbarVisibilityChanged += OnMainPaneTaskbarVisibilityChanged;
            this.MainPane.InitializationFailed += OnMainPaneInitializationFailed;
            this.MainPane.CloseRequested += (_, _) =>
            {
                _closeType = "CloseButton";
                CloseAnimated();
            };

            // Reveal the main pane after the window has loaded so the
            // Implicit.ShowAnimations play on first appearance.
            this.OverlayRoot.Loaded += (_, _) =>
            {
                this.MainPane.Visibility = Visibility.Visible;
            };
        }

        private void ApplyThemeFromSettings()
        {
            switch (App.ShortcutGuideProperties.Theme.Value)
            {
                case "dark":
                    if (this.Content is FrameworkElement darkRoot)
                    {
                        darkRoot.RequestedTheme = ElementTheme.Dark;
                    }

                    break;
                case "light":
                    if (this.Content is FrameworkElement lightRoot)
                    {
                        lightRoot.RequestedTheme = ElementTheme.Light;
                    }

                    break;
                case "system":
                    // Default — follow the system theme.
                    break;
                default:
                    Logger.LogError("Invalid theme value in settings: " + App.ShortcutGuideProperties.Theme.Value);
                    break;
            }
        }

        private void OnActivated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated)
            {
#if !DEBUG
                _closeType = "Deactivated";
                CloseAnimated();
#endif
                return;
            }
        }

        private void OnContentKeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                _closeType = "Escape";
                CloseAnimated();
            }
        }

        /// <summary>
        /// Closes the overlay when the user clicks outside the pseudo-windows
        /// (i.e. anywhere in the transparent area). Clicks on
        /// <see cref="MainPaneControl"/> or <see cref="TaskbarPaneControl"/>
        /// are ignored — we detect them by walking the visual tree from
        /// <see cref="RoutedEventArgs.OriginalSource"/>.
        /// </summary>
        private void OverlayRoot_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.OriginalSource is DependencyObject src && IsInsidePseudoWindow(src))
            {
                return;
            }

            _closeType = "ClickOutside";
            CloseAnimated();
        }

        private bool IsInsidePseudoWindow(DependencyObject src)
        {
            DependencyObject? current = src;
            while (current is not null)
            {
                if (ReferenceEquals(current, this.MainPane) || ReferenceEquals(current, this.TaskbarPane))
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void OnMainPaneTaskbarVisibilityChanged(object? sender, bool shouldShow)
        {
            if (!shouldShow)
            {
                this.TaskbarPane.Visibility = Visibility.Collapsed;
                return;
            }

            UpdateTaskbarPaneLayout();
        }

        private void OnMainPaneInitializationFailed(object? sender, EventArgs e)
        {
            _closeType = "InitializationFailed";
            this.DispatcherQueue.TryEnqueue(() => this.Close());
        }

        /// <summary>
        /// Triggers hide animations on both pseudo-windows, waits for the
        /// longest animation to finish, then closes the window. Multiple
        /// calls are coalesced via <see cref="_isClosing"/>.
        /// </summary>
        public void CloseAnimated()
        {
            if (_isClosing)
            {
                return;
            }

            _isClosing = true;

            // Collapse both pseudo-windows so their Implicit.HideAnimations play
            this.MainPane.Visibility = Visibility.Collapsed;
            this.TaskbarPane.Visibility = Visibility.Collapsed;

            if (_closeTimer is null)
            {
                _closeTimer = this.DispatcherQueue.CreateTimer();
                _closeTimer.Interval = TimeSpan.FromMilliseconds(217);
                _closeTimer.IsRepeating = false;
                _closeTimer.Tick += OnCloseTimerTick;
            }

            _closeTimer.Start();
        }

        private void OnCloseTimerTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
        {
            sender.Stop();
            _isClosing = false;

            this.MainPane.Hide();

            this.AppWindow.Hide();
        }

        /// <summary>
        /// Recomputes the taskbar pane's indicator children and applies the
        /// resulting layout to the Canvas-positioned pseudo-window.
        /// </summary>
        public void UpdateTaskbarPaneLayout()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            float dpi = DpiHelper.GetDPIScaleForWindow(hwnd);
            Rect workArea = DisplayHelper.GetWorkAreaForDisplayWithWindow(hwnd);

            var layout = this.TaskbarPane.UpdateTasklistButtons(
                overlayPhysicalOriginX: this.AppWindow.Position.X,
                overlayPhysicalOriginY: this.AppWindow.Position.Y,
                dpi: dpi,
                workAreaPhysical: workArea,
                edge: _taskbarEdge);

            if (layout is null)
            {
                this.TaskbarPane.Visibility = Visibility.Collapsed;
                return;
            }

            this.TaskbarPane.Width = layout.Value.Width;
            this.TaskbarPane.Height = layout.Value.Height;
            Canvas.SetLeft(this.TaskbarPane, layout.Value.Left);
            Canvas.SetTop(this.TaskbarPane, layout.Value.Top);
            this.TaskbarPane.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Brings the kept-alive overlay back on screen for a new invocation.
        /// Repositions to the monitor under the cursor, shows the window, and —
        /// because a background (kept-alive) process loses the Windows
        /// foreground race that a freshly-launched process would win — forces
        /// the transparent overlay above the active window and gives it focus.
        /// Without this the overlay is shown behind the active window and the
        /// transparent host is effectively invisible.
        /// </summary>
        public void ShowOverlay()
        {
            _isClosing = false;

            RepositionToCursorMonitor();
            ApplyMainPaneAlignment();

            this.AppWindow.Show();

            var hwnd = WindowNative.GetWindowHandle(this);

            // Win32 foreground lock: SetForegroundWindow from a process that
            // does not already own the foreground is ignored. Temporarily
            // attach our input queue to the foreground thread's so the call is
            // honored, then detach. This is the canonical reliable
            // bring-to-front recipe and is what lets the persistent background
            // SG process surface its overlay on a hotkey press.
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            uint foregroundThread = NativeMethods.GetWindowThreadProcessId(foreground, out _);
            uint thisThread = NativeMethods.GetCurrentThreadId();

            bool attached = false;
            if (foregroundThread != 0 && foregroundThread != thisThread)
            {
                attached = NativeMethods.AttachThreadInput(thisThread, foregroundThread, true);
            }

            try
            {
                NativeMethods.BringWindowToTop(hwnd);
                NativeMethods.SetForegroundWindow(hwnd);
            }
            finally
            {
                if (attached)
                {
                    NativeMethods.AttachThreadInput(thisThread, foregroundThread, false);
                }
            }

            this.Activate();
            this.AppWindow.MoveInZOrderAtTop();
        }

        /// <summary>
        /// Sizes and positions the overlay to cover the work area of the
        /// monitor that currently contains the cursor. Looks the monitor up
        /// directly from the cursor position so the work-area rect doesn't
        /// depend on a previous asynchronous <see cref="AppWindow.Move"/>
        /// having actually committed yet.
        /// </summary>
        private void RepositionToCursorMonitor()
        {
            // The taskbar edge is a global setting; refresh it whenever we
            // reposition so the indicator layout follows a taskbar that the
            // user moved between sessions.
            _taskbarEdge = TasklistPositions.GetEdge();

            if (!NativeMethods.GetCursorPos(out NativeMethods.POINT cursor))
            {
                return;
            }

            IntPtr hmon = NativeMethods.MonitorFromPoint(
                cursor,
                (int)NativeMethods.MonitorFromWindowDwFlags.MONITOR_DEFAULTTONEAREST);
            if (hmon == IntPtr.Zero)
            {
                return;
            }

            var mi = new NativeMethods.MONITORINFO
            {
                CbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFO>(),
            };
            if (!NativeMethods.GetMonitorInfoW(hmon, ref mi))
            {
                return;
            }

            // rcWork is in physical pixels (virtual-screen coordinates).
            // AppWindow.MoveAndResize takes physical pixels too — no DPI math
            // required. Using AppWindow directly avoids the WinUIEx
            // WindowEx.MoveAndResize extension whose x/y are physical but
            // whose w/h are DIPs (and get DPI-scaled internally).
            var rect = new Windows.Graphics.RectInt32(
                mi.RcWork.Left,
                mi.RcWork.Top,
                mi.RcWork.Right - mi.RcWork.Left,
                mi.RcWork.Bottom - mi.RcWork.Top);

            // Cross-monitor moves trigger WM_DPICHANGED which causes WinUIEx
            // to auto-rescale the window by (newDpi/oldDpi) ON TOP of the
            // physical rect we just provided — making the overlay 1.5×
            // (or 0.66×) the work area on mixed-DPI multi-monitor setups.
            // CmdPal's MoveAndResizeDpiAware pattern: zero out MinWidth/
            // MinHeight (WinUIEx uses current DPI to recompute them in
            // physical px), set _suppressDpiChange so the WndProc swallows
            // WM_DPICHANGED, then MoveAndResize.
            var origMinWidth = this.MinWidth;
            var origMinHeight = this.MinHeight;
            _suppressDpiChange = true;
            try
            {
                this.MinWidth = 0;
                this.MinHeight = 0;
                this.AppWindow.MoveAndResize(rect);
            }
            finally
            {
                this.MinWidth = origMinWidth;
                this.MinHeight = origMinHeight;
                _suppressDpiChange = false;
            }

            // Cross-monitor moves can trigger WM_DPICHANGED, and Windows may
            // reset some of our DWM attributes (border color, corner pref)
            // during that transition. Re-apply them defensively so the
            // overlay never reveals an OS-drawn 1-px stroke or rounded
            // shadow.
            this.ApplyTransparentChrome();
            this.ApplyFullBleedHardening();

            // The taskbar pane is anchored against the bottom of the work area,
            // so any move/resize needs a fresh layout pass.
            if (this.TaskbarPane.Visibility == Visibility.Visible)
            {
                UpdateTaskbarPaneLayout();
            }
        }

        /// <summary>
        /// Applies the left/right alignment from the user setting to the
        /// main pseudo-window.
        /// </summary>
        private void ApplyMainPaneAlignment()
        {
            var windowPosition = (ShortcutGuideWindowPosition)App.ShortcutGuideProperties.WindowPosition.Value;
            bool isRight = windowPosition == ShortcutGuideWindowPosition.Right;

            this.MainPane.HorizontalAlignment = isRight
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left;

            // When the taskbar is docked to the same side the pane is aligned to,
            // reserve room for the vertical indicator strip so the layout reads
            // taskbar | indicators | pane instead of the pane overlapping the
            // indicators. The reserve = 8px edge gap + 46px (body + tail) + 16px
            // gap before the pane. Other edges keep the default 16px margin.
            const double DefaultMarginDip = 16;
            const double IndicatorReserveDip = 8 + 46 + 16;
            double leftMargin = DefaultMarginDip;
            double rightMargin = DefaultMarginDip;
            if (_taskbarEdge == TaskbarEdge.Left && !isRight)
            {
                leftMargin = IndicatorReserveDip;
            }
            else if (_taskbarEdge == TaskbarEdge.Right && isRight)
            {
                rightMargin = IndicatorReserveDip;
            }

            this.MainPane.Margin = new Thickness(leftMargin, DefaultMarginDip, rightMargin, DefaultMarginDip);

            // Slide direction matches the pane's edge: left-aligned slides
            // from the left, right-aligned slides from the right — same as
            // Windows 11 Widgets (left) and Action Center (right).
            string slideFrom = isRight ? "20,0,0" : "-20,0,0";

            var showAnimations = new ImplicitAnimationSet();
            showAnimations.Add(new OpacityAnimation { From = 0, To = 1.0, Duration = TimeSpan.FromMilliseconds(367) });
            showAnimations.Add(new TranslationAnimation
            {
                From = slideFrom,
                To = "0,0,0",
                Duration = TimeSpan.FromMilliseconds(367),
                EasingType = EasingType.Cubic,
                EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut,
            });
            Implicit.SetShowAnimations(this.MainPane, showAnimations);

            var hideAnimations = new ImplicitAnimationSet();
            hideAnimations.Add(new OpacityAnimation { From = 1.0, To = 0, Duration = TimeSpan.FromMilliseconds(200) });
            hideAnimations.Add(new TranslationAnimation
            {
                From = "0,0,0",
                To = slideFrom,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingType = EasingType.Cubic,
                EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn,
            });
            Implicit.SetHideAnimations(this.MainPane, hideAnimations);
        }
    }
}
