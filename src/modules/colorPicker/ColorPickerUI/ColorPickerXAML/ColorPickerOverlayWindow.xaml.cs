// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Runtime.InteropServices;

using ColorPicker.Helpers;
using ColorPicker.Mouse;
using ManagedCommon;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;

namespace ColorPicker
{
    /// <summary>
    /// The picking-overlay tooltip window: hosts <see cref="Views.MainView"/> inside the
    /// shared <see cref="TransparentWindow"/> (transparent <c>TransparentTintBackdrop</c>,
    /// frameless, tool-window so it stays out of the taskbar/Alt-Tab, and a no-activate
    /// <c>Show()</c> so it never steals foreground from the user's work).
    /// </summary>
    /// <remarks>
    /// This replaces the deleted WPF root MainWindow (AllowsTransparency + WindowStyle=None +
    /// SizeToContent + Topmost). The WPF window auto-sized to the small color tooltip, sat above
    /// the zoom magnifier (Topmost, re-asserted by the zoom helper), and was re-positioned next to
    /// the cursor by the <c>ChangeWindowPositionBehavior</c>. <see cref="InitializeCursorFollow"/>
    /// ports all three: WinUI has no <c>SizeToContent</c>, the behavior was WPF-only, and the
    /// window is kept top-most so the tooltip stays visible over the magnifier. Per-tick work is a
    /// single top-most <c>SetWindowPos</c> move plus a cheap per-monitor DPI lookup (so a tooltip
    /// dragged across mixed-DPI monitors re-sizes to the monitor under the cursor); the tooltip size
    /// and monitor list are cached and only recomputed on first show / content change.
    /// </remarks>
    public sealed partial class ColorPickerOverlayWindow : TransparentWindow
    {
        // Cursor-follow offsets + monitor-edge handling (ported from the WPF
        // ChangeWindowPositionBehavior; logical px, scaled to physical at the point of use).
        private const double XOffset = 5;
        private const double YOffset = 10;

        private const int HwndTopmost = -1;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoActivate = 0x0010;

        private const uint MonitorDefaultToNearest = 0x00000002;
        private const int MdtEffectiveDpi = 0;

        private const int SmCxScreen = 0;
        private const int SmCyScreen = 1;

        private readonly nint _hwnd;

        private IMouseInfoProvider _mouseInfoProvider;
        private Storyboard _appearStoryboard;
        private Point _lastCursor;
        private double _scale = 1.0;
        private Rect[] _monitors = Array.Empty<Rect>();
        private int _width = -1;
        private int _height = -1;

        public ColorPickerOverlayWindow()
        {
            InitializeComponent();
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        }

        /// <summary>
        /// Makes the overlay size itself to the <see cref="Views.MainView"/> tooltip and follow
        /// the cursor (kept above the zoom magnifier) while it is shown — the WinUI replacement for
        /// the WPF <c>SizeToContent="WidthAndHeight"</c> + <c>ChangeWindowPositionBehavior</c> +
        /// re-asserted <c>Topmost</c> that the migration had not yet ported.
        /// </summary>
        public void InitializeCursorFollow(IMouseInfoProvider mouseInfoProvider)
        {
            _mouseInfoProvider = mouseInfoProvider;

            // The provider polls on the UI thread (its DispatcherQueueTimer), so this fires on the
            // UI thread and can touch XAML/the window directly. It only fires when the cursor moves.
            _mouseInfoProvider.MousePositionChanged += (s, position) => MoveToCursor(position, resize: false);

            // The tooltip resizes only when its content changes (e.g. a longer color string or the
            // color-name line). Re-cache the size then and re-apply it; the per-tick path stays a
            // pure move so following is not throttled by a layout pass every cursor tick.
            if (Content is FrameworkElement card)
            {
                card.SizeChanged += (s, e) => MoveToCursor(_lastCursor, resize: true);
            }
        }

        /// <summary>
        /// Hides <see cref="TransparentWindow.Show"/> so the overlay snaps to the cursor as it
        /// appears. <see cref="IMouseInfoProvider.MousePositionChanged"/> only fires when the
        /// cursor moves, so a stationary-cursor show would otherwise leave the tooltip parked at
        /// the default window position. Positioning is queued after <c>base.Show()</c> so the card
        /// is laid out (a collapsed element measures to 0) before sizing.
        /// </summary>
        public new void Show()
        {
            // Present the card transparent first (so there is no flash), then start the 250ms fade
            // and the initial positioning together at Low priority — after base TransparentWindow.Show()
            // has pumped its deferred SW_SHOWNA, so the fade clock begins when the window is actually
            // on screen and the card is laid out before it is sized to the cursor.
            MainViewControl.Opacity = 0;
            base.Show();

            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                PlayAppearAnimation();

                if (_mouseInfoProvider != null)
                {
                    // Cache the monitor bounds for this pick session (the DPI scale is re-queried per
                    // move so a tooltip crossing into a different-DPI monitor re-sizes correctly).
                    RefreshEnvironment();
                    MoveToCursor(_mouseInfoProvider.CurrentPosition, resize: true);
                }
            });
        }

        /// <summary>
        /// Port of the WPF <c>AppearAnimationBehavior</c>: fade the tooltip in over 250ms
        /// (CubicEase) every time the overlay is shown. WPF animated <c>Window.Opacity</c>; a WinUI
        /// <see cref="Window"/> has no Opacity, so the hosted content element is animated instead —
        /// visually identical since it fills the whole tooltip. (WPF's 1ms hide fade was effectively
        /// instant, so no hide animation is needed.)
        /// </summary>
        private void PlayAppearAnimation()
        {
            if (_appearStoryboard == null)
            {
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                };
                Storyboard.SetTarget(fadeIn, MainViewControl);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");
                _appearStoryboard = new Storyboard();
                _appearStoryboard.Children.Add(fadeIn);
            }

            _appearStoryboard.Stop();
            MainViewControl.Opacity = 0;
            _appearStoryboard.Begin();
        }

        private void RefreshEnvironment()
        {
            _scale = GetDpiForWindow(_hwnd) / 96.0;
            _monitors = MonitorResolutionHelper.AllMonitors.Select(m => m.Bounds).ToArray();
        }

        private void MoveToCursor(Point cursorPhysical, bool resize)
        {
            _lastCursor = cursorPhysical;

            if (_monitors.Length == 0)
            {
                RefreshEnvironment();
            }

            // Re-query the DPI of the monitor under the cursor. On mixed-DPI multi-monitor setups
            // the scale changes as the cursor crosses monitors; a value frozen at Show() would
            // mis-size and mis-offset the tooltip (the WPF behavior re-queried DPI per move).
            double scale = GetScaleForCursor(cursorPhysical);
            if (scale > 0 && scale != _scale)
            {
                _scale = scale;
                resize = true; // re-measure the tooltip card at the new monitor's scale
            }

            // WinUI has no SizeToContent: size the window to the tooltip card. Only measured when a
            // resize is requested (first show / content change), so the cursor tick stays a move.
            if (resize || _width <= 0 || _height <= 0)
            {
                if (Content is not FrameworkElement card)
                {
                    return;
                }

                card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                int width = (int)Math.Ceiling(card.DesiredSize.Width * _scale);
                int height = (int)Math.Ceiling(card.DesiredSize.Height * _scale);
                if (width <= 0 || height <= 0)
                {
                    // Card still collapsed/unmeasured (fired before the show completes); a later
                    // tick or the post-Show() queue will size and position it.
                    return;
                }

                _width = width;
                _height = height;
            }

            // Place next to the cursor, flipping away from the monitor's right/bottom edge so the
            // tooltip never spills off-screen. Everything here is in physical pixels.
            var bounds = GetMonitorBounds(cursorPhysical);
            int xOffset = (int)(XOffset * _scale);
            int yOffset = (int)(YOffset * _scale);
            int left = (int)cursorPhysical.X + xOffset;
            int top = (int)cursorPhysical.Y + yOffset;

            if (left + _width > bounds.X + bounds.Width)
            {
                left = (int)cursorPhysical.X - _width - xOffset;
            }

            if (top + _height > bounds.Y + bounds.Height)
            {
                top = (int)cursorPhysical.Y - _height - yOffset;
            }

            // Insert at HWND_TOPMOST so the tooltip stays above the (non-top-most) zoom magnifier,
            // matching the WPF helper that re-asserted MainWindow.Topmost after showing the zoom.
            uint flags = SwpNoActivate | (resize ? 0u : SwpNoSize);
            _ = SetWindowPos(_hwnd, HwndTopmost, left, top, _width, _height, flags);
        }

        private Rect GetMonitorBounds(Point cursorPhysical)
        {
            foreach (var bounds in _monitors)
            {
                if (bounds.Contains(cursorPhysical))
                {
                    return bounds;
                }
            }

            if (_monitors.Length > 0)
            {
                // Cursor not inside any enumerated monitor (rare; e.g. just outside the work area):
                // fall back to the first monitor so the edge-flip math still applies.
                return _monitors[0];
            }

            // No monitors enumerated at all (degenerate, e.g. a transient no-display state). Log it
            // and fall back to the primary screen bounds so the tooltip still edge-flips instead of
            // being placed against an unbounded rect.
            Logger.LogWarning("ColorPicker overlay: no monitors enumerated; falling back to primary screen bounds.");
            return new Rect(0, 0, GetSystemMetrics(SmCxScreen), GetSystemMetrics(SmCyScreen));
        }

        // Effective DPI scale of the monitor the cursor is on (1.0 == 96 DPI). Falls back to the
        // last known scale if the lookup fails.
        private double GetScaleForCursor(Point cursorPhysical)
        {
            var monitor = MonitorFromPoint(new POINT { X = (int)cursorPhysical.X, Y = (int)cursorPhysical.Y }, MonitorDefaultToNearest);
            if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, MdtEffectiveDpi, out uint dpiX, out _) == 0)
            {
                return dpiX / 96.0;
            }

            return _scale;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(nint hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
    }
}
