// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

using ColorPicker.Helpers;
using ColorPicker.Mouse;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
using Windows.Graphics;
using WinUIEx;

namespace ColorPicker
{
    /// <summary>
    /// The picking-overlay tooltip window: hosts <see cref="Views.ColorPickerView"/> inside the
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
    /// window is kept top-most so the tooltip stays visible over the magnifier. Per-tick work
    /// resolves the <see cref="DisplayArea"/> under the cursor, sizes the tooltip to that display's
    /// effective DPI, and moves it with <see cref="FlyoutWindowHelper.MoveAndResizeOnDisplay"/>
    /// (which handles mixed-DPI monitor crossings). The tooltip size is cached and only recomputed
    /// on first show / content change / DPI change; z-order is re-asserted so the tooltip stays
    /// above the top-most zoom window.
    /// </remarks>
    public sealed partial class ColorPickerOverlayWindow : TransparentWindow
    {
        // Cursor-follow offsets + monitor-edge handling (ported from the WPF
        // ChangeWindowPositionBehavior; logical px, scaled to physical at the point of use).
        private const double XOffset = 5;
        private const double YOffset = 10;

        // Z-order-only SetWindowPos: HWND_TOPMOST with NOMOVE|NOSIZE|NOACTIVATE. Geometry is owned
        // by FlyoutWindowHelper.MoveAndResizeOnDisplay; SetWindowPos is used solely to reassert the
        // tooltip above the top-most zoom magnifier without moving, resizing, or activating it.
        private const int HwndTopmost = -1;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoActivate = 0x0010;

        private readonly nint _hwnd;
        private readonly CoalescedAction _resizeRequest;

        private IMouseInfoProvider _mouseInfoProvider;
        private Storyboard _appearStoryboard;
        private Point _lastCursor;
        private double _scale = 1.0;
        private int _width = -1;
        private int _height = -1;

        public ColorPickerOverlayWindow()
        {
            InitializeComponent();
            _resizeRequest = new CoalescedAction(
                callback => DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => callback()),
                ResizeToContent);
            _hwnd = this.GetWindowHandle();
        }

        /// <summary>
        /// Makes the overlay size itself to the <see cref="Views.ColorPickerView"/> tooltip and follow
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
            ColorPickerViewControl.DesiredSizeInvalidated += ColorPickerViewControl_DesiredSizeInvalidated;
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
            ColorPickerViewControl.Opacity = 0;
            base.Show();

            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                PlayAppearAnimation();
                _resizeRequest.Request();
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
                Storyboard.SetTarget(fadeIn, ColorPickerViewControl);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");
                _appearStoryboard = new Storyboard();
                _appearStoryboard.Children.Add(fadeIn);
            }

            _appearStoryboard.Stop();
            ColorPickerViewControl.Opacity = 0;
            _appearStoryboard.Begin();
        }

        private void ColorPickerViewControl_DesiredSizeInvalidated(object sender, EventArgs e)
        {
            _resizeRequest.Request();
        }

        private void ResizeToContent()
        {
            if (_mouseInfoProvider != null)
            {
                // The DisplayArea and DPI under the cursor are resolved per move inside
                // MoveToCursor, so a tooltip crossing into a different-DPI monitor re-sizes.
                MoveToCursor(_mouseInfoProvider.CurrentPosition, resize: true);
            }
        }

        private void MoveToCursor(Point cursorPhysical, bool resize)
        {
            _lastCursor = cursorPhysical;

            // Resolve the display under the cursor from its physical coordinates. Nearest fallback
            // keeps a cursor just outside a work area mapped to the closest monitor; a null result
            // (degenerate/no-display state) means there is nothing to position against, so skip.
            var targetDisplay = DisplayArea.GetFromPoint(
                new PointInt32((int)cursorPhysical.X, (int)cursorPhysical.Y),
                DisplayAreaFallback.Nearest);
            if (targetDisplay is null)
            {
                return;
            }

            // Re-query the DPI of the monitor under the cursor. On mixed-DPI multi-monitor setups
            // the scale changes as the cursor crosses monitors; a value frozen at Show() would
            // mis-size and mis-offset the tooltip (the WPF behavior re-queried DPI per move).
            double scale = FlyoutWindowHelper.GetDpiScale(targetDisplay);
            if (scale > 0 && scale != _scale)
            {
                _scale = scale;
                resize = true; // re-measure the tooltip card at the new monitor's scale
            }

            // WinUI has no SizeToContent: size the window to the tooltip card. Only measured when a
            // resize is requested (first show / content change), so the cursor tick stays a move.
            if (resize || _width <= 0 || _height <= 0)
            {
                ColorPickerViewControl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                int width = (int)Math.Ceiling(ColorPickerViewControl.DesiredSize.Width * _scale);
                int height = (int)Math.Ceiling(ColorPickerViewControl.DesiredSize.Height * _scale);
                if (width <= 0 || height <= 0)
                {
                    // Card still collapsed/unmeasured (fired before the show completes); a later
                    // tick or the post-Show() queue will size and position it.
                    return;
                }

                _width = width;
                _height = height;
            }

            // Place next to the cursor, flipping away from the display's right/bottom edge so the
            // tooltip never spills off-screen. OuterBounds is in screen physical pixels (X/Y may be
            // negative on secondary monitors), matching the cursor's physical coordinates.
            var bounds = targetDisplay.OuterBounds;
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

            // Absolute screen physical-pixel geometry via the shared helper, which handles the
            // mixed-DPI monitor crossing (teleport-then-resize to avoid WM_DPICHANGED double-scaling).
            FlyoutWindowHelper.MoveAndResizeOnDisplay(this, targetDisplay, new RectInt32(left, top, _width, _height));

            // Geometry is owned by MoveAndResizeOnDisplay; reassert z-order (only) so the tooltip
            // stays above the top-most zoom magnifier, matching the WPF helper that re-asserted
            // MainWindow.Topmost after showing the zoom.
            ReassertTopmost();
        }

        /// <summary>
        /// Reasserts the overlay as the frontmost top-most window without moving, resizing, or
        /// activating it. Called after every cursor move and (by <see cref="Helpers.ZoomWindowHelper"/>)
        /// after the zoom window is shown, so the tooltip stays above the equally top-most magnifier
        /// even when the cursor is stationary.
        /// </summary>
        internal void ReassertTopmost()
        {
            _ = SetWindowPos(_hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
    }
}
