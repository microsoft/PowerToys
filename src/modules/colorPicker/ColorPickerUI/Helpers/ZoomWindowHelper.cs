// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using ManagedCommon;
using Microsoft.Graphics.Canvas;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.Graphics.DirectX;

using Point = Windows.Foundation.Point;

namespace ColorPicker.Helpers
{
    /// <summary>
    /// Drives the zoom magnifier: captures a small screen region around the cursor with GDI,
    /// hands it to the Win2D-backed <see cref="Views.ZoomView"/> (via <see cref="ZoomWindow"/>),
    /// and centers the transparent magnifier window on the cursor once, when the zoom session first
    /// starts. The window then stays put while the wheel keeps zooming the region it opened over.
    /// </summary>
    public class ZoomWindowHelper
    {
        private const int ZoomFactor = 2;
        private const int BaseZoomImageSize = 50;
        private const int MaxZoomLevel = 4;
        private const int MinZoomLevel = 0;
        private const int WindowChrome = 30; // Border (12) + canvas (3) margins on each side.

        // The magnifier window is a constant size for the whole session — the level-4 (factor 8)
        // bounding box — so the centred card always fits and never clips. Only the inner card
        // animates its size (Composition scale in ZoomView), so there is no per-step window resize
        // and no shrink-trim bookkeeping. Expressed in device-independent pixels (DIP); it is scaled
        // to the target monitor's physical pixels when the window is first shown.
        private const int MaxWindowSize = (BaseZoomImageSize * 8) + WindowChrome; // 50*8 + 30 = 430

        private static readonly Bitmap _bmp = new Bitmap(BaseZoomImageSize, BaseZoomImageSize, PixelFormat.Format32bppArgb);
        private static readonly Graphics _graphics = Graphics.FromImage(_bmp);

        private readonly AppStateHandler _appStateHandler;

        private int _currentZoomLevel;
        private int _previousZoomLevel;
        private ZoomWindow _zoomWindow;
        private CanvasBitmap _capturedBitmap;
        private double _zoomFactorValue = 1;
        private bool _zoomWindowVisible;

        public ZoomWindowHelper(AppStateHandler appStateHandler)
        {
            _appStateHandler = appStateHandler;
            _appStateHandler.AppClosed += (s, e) => CloseZoomWindow();
            _appStateHandler.AppHidden += (s, e) => CloseZoomWindow();
        }

        public void Zoom(Point position, bool zoomIn)
        {
            if (zoomIn && _currentZoomLevel < MaxZoomLevel)
            {
                _previousZoomLevel = _currentZoomLevel;
                _currentZoomLevel++;
            }
            else if (!zoomIn && _currentZoomLevel > MinZoomLevel)
            {
                _previousZoomLevel = _currentZoomLevel;
                _currentZoomLevel--;
            }
            else
            {
                return;
            }

            SetZoomImage(position);
        }

        public void CloseZoomWindow()
        {
            _currentZoomLevel = 0;
            _previousZoomLevel = 0;
            _zoomWindowVisible = false;
            _zoomWindow?.ZoomViewControl.ResetScale();
            _zoomWindow?.ZoomViewControl.ClearBitmap();

            HideZoomWindow();

            // Release this session's captured GPU surface (the ZoomView reference is cleared above
            // so the canvas will not draw a disposed bitmap).
            _capturedBitmap?.Dispose();
            _capturedBitmap = null;
        }

        private void SetZoomImage(Point point)
        {
            if (_currentZoomLevel == 0)
            {
                _zoomWindowVisible = false;
                _zoomWindow?.ZoomViewControl.ResetScale();

                HideZoomWindow();
                return;
            }

            // Capture once when a zoom session starts (previous level was 0), or when a previous
            // attempt left no bitmap so a later wheel tick retries the capture.
            if (_previousZoomLevel == 0 || _capturedBitmap == null)
            {
                // Release the previous session's GPU surface before capturing a new one. The window
                // is hidden between sessions, so clear the ZoomView reference first (it is not
                // drawing this bitmap) and then dispose.
                _zoomWindow?.ZoomViewControl.ClearBitmap();
                _capturedBitmap?.Dispose();
                _capturedBitmap = null;

                var mainWindowHandle = _appStateHandler.GetMainWindowHandle();
                bool exclusionSuccess = WindowCaptureExclusionHelper.Exclude(mainWindowHandle);
                try
                {
                    var x = (int)point.X - (BaseZoomImageSize / 2);
                    var y = (int)point.Y - (BaseZoomImageSize / 2);
                    _graphics.CopyFromScreen(x, y, 0, 0, _bmp.Size, CopyPixelOperation.SourceCopy);
                    _capturedBitmap = BitmapToCanvasBitmap(_bmp);
                }
                catch (Win32Exception ex)
                {
                    // CopyFromScreen can fail (invalid screen DC) on a non-interactive session.
                    Logger.LogError("Failed to capture the zoom image", ex);
                    _capturedBitmap = null;
                }
                catch (ExternalException ex)
                {
                    // BitmapToCanvasBitmap can fail when the GPU device is lost or unavailable.
                    Logger.LogError("Failed to create the zoom image", ex);
                    _capturedBitmap = null;
                }
                finally
                {
                    if (exclusionSuccess)
                    {
                        WindowCaptureExclusionHelper.Include(mainWindowHandle);
                    }
                }
            }

            _zoomFactorValue = Math.Pow(ZoomFactor, _currentZoomLevel - 1);

            // The size the card is animating FROM: the previous level's factor (or the current one
            // on first appearance, which makes the scale a no-op snap).
            double previousFactor = _previousZoomLevel >= 1 ? Math.Pow(ZoomFactor, _previousZoomLevel - 1) : _zoomFactorValue;
            ShowZoomWindow(point, previousFactor);
        }

        /// <summary>
        /// Hides the zoom window with FIFO-safe ordering.
        /// <para>
        /// <see cref="Microsoft.UI.Windowing.AppWindow.Hide"/> runs synchronously first so
        /// screen capture cannot include the magnifier. Then
        /// <see cref="Microsoft.PowerToys.Common.UI.Controls.Window.TransparentWindow.Hide"/>
        /// enqueues a trailing <see cref="Microsoft.UI.Windowing.AppWindow.Hide"/> at Low priority
        /// behind any <c>SW_SHOWNA</c> already queued by
        /// <see cref="Microsoft.PowerToys.Common.UI.Controls.Window.TransparentWindow.Show()"/>.
        /// With no <c>Hiding</c> subscribers the queued item resolves to
        /// <see cref="Microsoft.UI.Windowing.AppWindow.Hide"/> immediately; FIFO ordering guarantees
        /// the final state is hidden regardless of Show/Hide interleaving.
        /// </para>
        /// </summary>
        private void HideZoomWindow()
        {
            if (_zoomWindow == null)
            {
                return;
            }

            // Synchronous immediate hide: capture cannot include the magnifier after this returns.
            _zoomWindow.AppWindow.Hide();

            // Trailing queued hide: enqueued at Low priority after any SW_SHOWNA already queued by
            // Show(), so the window ends hidden regardless of rapid Show→hide interleaving.
            _zoomWindow.Hide();
        }

        private static CanvasBitmap BitmapToCanvasBitmap(Bitmap bitmap)
        {
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var bytes = new byte[data.Stride * data.Height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

                // GDI 32bppArgb is BGRA in memory, matching B8G8R8A8.
                return CanvasBitmap.CreateFromBytes(CanvasDevice.GetSharedDevice(), bytes, bitmap.Width, bitmap.Height, DirectXPixelFormat.B8G8R8A8UIntNormalized);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private void ShowZoomWindow(Point point, double previousFactor)
        {
            if (_capturedBitmap == null)
            {
                return;
            }

            _zoomWindow ??= new ZoomWindow();

            // Draw the capture at the FINAL zoom factor now; the compositor scales that crisp texture
            // during the resize tween (see ZoomView.AnimateResize).
            _zoomWindow.ZoomViewControl.SetZoom(_capturedBitmap, _zoomFactorValue);

            if (!_zoomWindowVisible)
            {
                // First appearance this session. Give the window its constant max size (the level-4
                // bounding box, so the centred card never clips) and center it on the cursor ONCE,
                // then leave it there for the rest of the session — matching the WPF behavior, which
                // only repositions the window while it is still transparent (Opacity < 0.5). Moving
                // it on every scroll step would drag the magnifier to wherever the cursor currently
                // is instead of keeping it on — and zooming — the region it was first opened over.

                // Resolve the display under the cursor so the window is sized to that monitor's DPI
                // and moved on it (MoveAndResizeOnDisplay handles the mixed-DPI monitor crossing).
                var targetDisplay = DisplayArea.GetFromPoint(
                    new PointInt32((int)point.X, (int)point.Y),
                    DisplayAreaFallback.Nearest);
                if (targetDisplay is null)
                {
                    // No display resolved for the cursor (degenerate/no-display state): skip showing
                    // rather than crash. A later wheel tick retries.
                    return;
                }

                double dpiScale = FlyoutWindowHelper.GetDpiScale(targetDisplay);
                int sizePhysical = FlyoutWindowHelper.ScaleToPhysicalPixels(MaxWindowSize, dpiScale);

                // Center the window on the cursor. Cursor coordinates and AppWindow geometry are
                // physical pixels; the card is centred inside the constant-size window.
                int left = (int)point.X - (sizePhysical / 2);
                int top = (int)point.Y - (sizePhysical / 2);
                FlyoutWindowHelper.MoveAndResizeOnDisplay(_zoomWindow, targetDisplay, new RectInt32(left, top, sizePhysical, sizePhysical));

                // The card shows directly at the current size (no tween), so there is no scale
                // animation to race against the async Show().
                _zoomWindow.ZoomViewControl.ResetScale();
                _zoomWindow.Show();
                _zoomWindowVisible = true;

                // TransparentWindow.Show() queues its SW_SHOWNA on the dispatcher (Low priority).
                // Enqueue the overlay's z-order reassertion on the same DispatcherQueue at Low
                // priority so it runs AFTER the zoom show request, keeping the tooltip above the
                // equally top-most magnifier even when the cursor is stationary (no move tick to
                // reassert it).
                _zoomWindow.DispatcherQueue.TryEnqueue(
                    DispatcherQueuePriority.Low,
                    () => (App.Window as ColorPickerOverlayWindow)?.ReassertTopmost());
            }
            else
            {
                // Same already-shown, laid-out window: animate the card between the old and new size.
                _zoomWindow.ZoomViewControl.AnimateResize(previousFactor, _zoomFactorValue);
            }
        }
    }
}
