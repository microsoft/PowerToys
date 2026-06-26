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

        private static readonly Bitmap _bmp = new Bitmap(BaseZoomImageSize, BaseZoomImageSize, PixelFormat.Format32bppArgb);
        private static readonly Graphics _graphics = Graphics.FromImage(_bmp);

        private readonly IZoomViewModel _zoomViewModel;
        private readonly AppStateHandler _appStateHandler;

        private int _currentZoomLevel;
        private int _previousZoomLevel;
        private ZoomWindow _zoomWindow;
        private CanvasBitmap _capturedBitmap;
        private double _zoomFactorValue = 1;

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
            _zoomWindow?.Hide();
        }

        private void SetZoomImage(Point point)
        {
            if (_currentZoomLevel == 0)
            {
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
            ShowZoomWindow(point);
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

        private void ShowZoomWindow(Point point)
        {
            if (_capturedBitmap == null)
            {
                return;
            }

            _zoomWindow ??= new ZoomWindow();
            _zoomWindow.ZoomViewControl.SetZoom(_capturedBitmap, _zoomFactorValue);

            var winSize = (BaseZoomImageSize * _zoomFactorValue) + WindowChrome;
            _zoomWindow.SetWindowSize(winSize, winSize);

            // Center the magnifier on the cursor (both AppWindow.Position/Size and the cursor are
            // in physical pixels).
            var appWindow = _zoomWindow.AppWindow;
            appWindow.Move(new PointInt32((int)point.X - (appWindow.Size.Width / 2), (int)point.Y - (appWindow.Size.Height / 2)));

            _zoomWindow.Show();
        }
    }
}
