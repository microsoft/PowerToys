// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using FancyZonesEditor.Utils;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.Graphics;
using WinRT.Interop;
using WinUIEx;

namespace FancyZonesEditor
{
    /// <summary>
    /// Base class for the windows that float above the layout overlay - the layout picker and
    /// the two zone editors. It reproduces the WPF combination of
    /// <c>SizeToContent="WidthAndHeight"</c>, <c>MaxWidth/MaxHeight</c> clamped to the work area
    /// and <c>WindowStartupLocation="CenterOwner"</c>, none of which WinUI 3 provides.
    /// </summary>
    public partial class OverlayChildWindow : WindowEx
    {
        protected OverlayChildWindow()
        {
            Hwnd = WindowNative.GetWindowHandle(this);
        }

        protected IntPtr Hwnd { get; }

        /// <summary>
        /// Gets the work area of the monitor the layout overlay currently covers, in physical
        /// pixels. The overlay window is already positioned on that work area from the virtual
        /// coordinates the FancyZones backend supplies, so reading its rect back keeps every
        /// placement decision here in physical pixels and free of DPI conversions.
        /// </summary>
        protected RectInt32 OverlayWorkArea
        {
            get
            {
                var overlay = App.Overlay.CurrentLayoutWindow?.AppWindow;
                if (overlay == null)
                {
                    var display = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
                    return display.WorkArea;
                }

                return new RectInt32(overlay.Position.X, overlay.Position.Y, overlay.Size.Width, overlay.Size.Height);
            }
        }

        /// <summary>
        /// Puts the window on the overlay's monitor before it is activated, so the first frame is
        /// already on the right display instead of at WinUI's default rect on the primary one.
        /// The content-sized rect is applied later, once layout has run.
        /// </summary>
        protected void PrePlaceOnOverlayMonitor()
        {
            MoveAndResize(OverlayWorkArea);
        }

        /// <summary>
        /// Sizes the window to its content, clamped to the overlay monitor's work area, and
        /// centers it there.
        /// </summary>
        protected void SizeToContentAndCenter()
        {
            if (Content is not FrameworkElement root)
            {
                return;
            }

            RectInt32 workArea = OverlayWorkArea;

            // Move onto the target monitor first so the rasterization scale, and therefore the
            // DIP-to-pixel conversion below, belongs to that monitor. If that move crossed a DPI
            // boundary the default WM_DPICHANGED handling may rescale the window behind our back,
            // but the second call below runs after the new scale is known and is authoritative.
            MoveAndResize(workArea);

            double scale = root.XamlRoot?.RasterizationScale ?? NativeMethods.GetWindowScale(Hwnd);
            root.Measure(new Size(workArea.Width / scale, workArea.Height / scale));
            Size desired = root.DesiredSize;

            int width = Math.Clamp((int)Math.Ceiling(desired.Width * scale), 1, workArea.Width);
            int height = Math.Clamp((int)Math.Ceiling(desired.Height * scale), 1, workArea.Height);

            MoveAndResize(new RectInt32(
                workArea.X + ((workArea.Width - width) / 2),
                workArea.Y + ((workArea.Height - height) / 2),
                width,
                height));
        }

        /// <summary>
        /// Hides the window without letting XAML stop painting it, so bringing it back does not
        /// flash the frame it was showing when it went away.
        /// </summary>
        protected void ConcealWindow()
        {
            NativeMethods.CloakWindow(Hwnd);
        }

        /// <summary>
        /// Reveals a window concealed by <see cref="ConcealWindow"/>.
        /// </summary>
        protected void RevealWindow()
        {
            NativeMethods.UncloakWindow(Hwnd);
        }

        /// <summary>
        /// Moves and resizes the window using physical pixels.
        /// </summary>
        /// <remarks>
        /// Uses <see cref="Microsoft.UI.Windowing.AppWindow"/> directly rather than the WinUIEx
        /// <c>WindowEx.MoveAndResize</c> extension, whose width and height are DIPs and get
        /// scaled again internally. <c>MinWidth</c>/<c>MinHeight</c> are zeroed for the duration
        /// because WinUIEx enforces them in physical pixels derived from the current DPI, which
        /// would otherwise clamp the rect we are writing.
        /// </remarks>
        /// <param name="rect">Target rectangle in physical pixels.</param>
        private void MoveAndResize(RectInt32 rect)
        {
            double minWidth = MinWidth;
            double minHeight = MinHeight;

            try
            {
                MinWidth = 0;
                MinHeight = 0;
                AppWindow.MoveAndResize(rect);
            }
            finally
            {
                MinWidth = minWidth;
                MinHeight = minHeight;
            }
        }
    }
}
