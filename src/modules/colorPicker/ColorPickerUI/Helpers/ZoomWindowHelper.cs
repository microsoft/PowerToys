// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using ColorPicker.ViewModelContracts;
using Microsoft.Graphics.Canvas;
using Windows.Graphics;
using Windows.Graphics.DirectX;
using WinUIEx;

using Point = Windows.Foundation.Point;

namespace ColorPicker.Helpers
{
    /// <summary>
    /// Drives the zoom magnifier: captures a small screen region around the cursor with GDI,
    /// hands it to the Win2D-backed <see cref="Views.ZoomView"/> (via <see cref="ZoomWindow"/>),
    /// and positions the transparent magnifier window centered on the cursor.
    /// </summary>
    public class ZoomWindowHelper
    {
        private const int ZoomFactor = 2;
        private const int BaseZoomImageSize = 50;
        private const int MaxZoomLevel = 4;
        private const int MinZoomLevel = 0;
        private const int WindowChrome = 30; // Border (12) + canvas (3) margins on each side.

        // ALT-1: the magnifier window is a constant size for the whole session — the level-4
        // (factor 8) bounding box — so the centred card always fits and never clips. Only the inner
        // card animates its size (Composition scale in ZoomView), so there is no per-step window
        // resize and no shrink-trim bookkeeping.
        private const int MaxWindowSize = (BaseZoomImageSize * 8) + WindowChrome; // 50*8 + 30 = 430

        private static readonly Bitmap _bmp = new Bitmap(BaseZoomImageSize, BaseZoomImageSize, PixelFormat.Format32bppArgb);
        private static readonly Graphics _graphics = Graphics.FromImage(_bmp);

        private readonly IZoomViewModel _zoomViewModel;
        private readonly AppStateHandler _appStateHandler;

        private int _currentZoomLevel;
        private int _previousZoomLevel;
        private ZoomWindow _zoomWindow;
        private CanvasBitmap _capturedBitmap;
        private double _zoomFactorValue = 1;
        private bool _zoomWindowVisible;

        public ZoomWindowHelper(IZoomViewModel zoomViewModel, AppStateHandler appStateHandler)
        {
            _zoomViewModel = zoomViewModel;
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
            _zoomWindow?.Hide();
        }

        private void SetZoomImage(Point point)
        {
            if (_currentZoomLevel == 0)
            {
                _zoomWindowVisible = false;
                _zoomWindow?.ZoomViewControl.ResetScale();
                _zoomWindow?.Hide();
                return;
            }

            // Capture once when a zoom session starts (previous level was 0).
            if (_previousZoomLevel == 0)
            {
                var mainWindowHandle = _appStateHandler.GetMainWindowHandle();
                bool exclusionSuccess = WindowCaptureExclusionHelper.Exclude(mainWindowHandle);
                try
                {
                    var x = (int)point.X - (BaseZoomImageSize / 2);
                    var y = (int)point.Y - (BaseZoomImageSize / 2);
                    _graphics.CopyFromScreen(x, y, 0, 0, _bmp.Size, CopyPixelOperation.SourceCopy);
                    _capturedBitmap = BitmapToCanvasBitmap(_bmp);
                }
                catch (Exception)
                {
                    // CopyFromScreen can fail (invalid screen DC) on a non-interactive session.
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
            _zoomViewModel.ZoomFactor = _zoomFactorValue;

            // The size the card is animating FROM: the previous level's factor (or the current one
            // on first appearance, which makes the scale a no-op snap).
            double previousFactor = _previousZoomLevel >= 1 ? Math.Pow(ZoomFactor, _previousZoomLevel - 1) : _zoomFactorValue;
            ShowZoomWindow(point, previousFactor);
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

            // The window is a constant max size; only set it on first show. Win2D is drawn at the
            // FINAL factor now — the compositor scales that crisp texture during the tween.
            if (!_zoomWindowVisible)
            {
                _zoomWindow.SetWindowSize(MaxWindowSize, MaxWindowSize);
            }

            _zoomWindow.ZoomViewControl.SetZoom(_capturedBitmap, _zoomFactorValue);

            // Re-center the fixed-size window on the cursor every step (AppWindow.Size and the cursor
            // are both physical pixels). The card is centred inside, so it stays on the cursor.
            var appWindow = _zoomWindow.AppWindow;
            appWindow.Move(new PointInt32((int)point.X - (appWindow.Size.Width / 2), (int)point.Y - (appWindow.Size.Height / 2)));

            if (!_zoomWindowVisible)
            {
                // First appearance this session: the card shows directly at the current size (no
                // tween), so there is no scale animation to race against the async Show().
                _zoomWindow.ZoomViewControl.ResetScale();
                _zoomWindow.Show();
                _zoomWindowVisible = true;
            }
            else
            {
                // Same already-shown, laid-out window: animate the card between the old and new size.
                _zoomWindow.ZoomViewControl.AnimateResize(previousFactor, _zoomFactorValue);
            }
        }
    }
}
