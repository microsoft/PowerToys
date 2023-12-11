// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using ColorPicker.ViewModelContracts;

namespace ColorPicker.Helpers
{
    [Export(typeof(ZoomWindowHelper))]
    public class ZoomWindowHelper
    {
        private const int ZoomFactor = 2;
        private const int BaseZoomImageSize = 50;
        private const int MaxZoomLevel = 4;
        private const int MinZoomLevel = 0;

        private static readonly Bitmap _bmp = new Bitmap(BaseZoomImageSize, BaseZoomImageSize, PixelFormat.Format32bppArgb);
        private static readonly Graphics _graphics = Graphics.FromImage(_bmp);

        private readonly IZoomViewModel _zoomViewModel;
        private readonly AppStateHandler _appStateHandler;

        private int _currentZoomLevel;
        private int _previousZoomLevel;

        private ZoomWindow _zoomWindow;

        [ImportingConstructor]
        public ZoomWindowHelper(IZoomViewModel zoomViewModel, AppStateHandler appStateHandler)
        {
            _zoomViewModel = zoomViewModel;
            _appStateHandler = appStateHandler;
            _appStateHandler.AppClosed += AppStateHandler_AppClosed;
            _appStateHandler.AppHidden += AppStateHandler_AppClosed;
        }

        public void Zoom(System.Windows.Point position, bool zoomIn)
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
            HideZoomWindow(true);
        }

        private void SetZoomImage(System.Windows.Point point)
        {
            if (_currentZoomLevel == 0)
            {
                HideZoomWindow();
                return;
            }

            // we just started zooming, copy screen area
            if (_previousZoomLevel == 0)
            {
                var x = (int)point.X - (BaseZoomImageSize / 2);
                var y = (int)point.Y - (BaseZoomImageSize / 2);

                _graphics.CopyFromScreen(x, y, 0, 0, _bmp.Size, CopyPixelOperation.SourceCopy);

                _zoomViewModel.ZoomArea = BitmapToImageSource(_bmp);
            }

            _zoomViewModel.ZoomFactor = Math.Pow(ZoomFactor, _currentZoomLevel - 1);

            ShowZoomWindow(point);
        }

        private static BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Bmp);
                memory.Position = 0;

                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }

        private void HideZoomWindow(bool fully = false)
        {
            if (_zoomWindow != null)
            {
                _zoomWindow.Opacity = 0;
                _zoomViewModel.DesiredWidth = 0;
                _zoomViewModel.DesiredHeight = 0;

                if (fully)
                {
                    _zoomWindow.Hide();
                }
            }
        }

        private void ShowZoomWindow(System.Windows.Point point)
        {
            _zoomWindow ??= new ZoomWindow
            {
                Content = _zoomViewModel,
                Opacity = 0,
            };

            if (!_zoomWindow.IsVisible)
            {
                _zoomWindow.Show();
            }

            if (_zoomWindow.Opacity < 0.5)
            {
                var halfWidth = _zoomWindow.Width / 2;
                var halfHeight = _zoomWindow.Height / 2;

                // usually takes 1-3 iterations to converge
                // 5 is just an arbitrary limit to prevent infinite loops
                for (var i = 0; i < 5; i++)
                {
                    // mouse position relative to top left of _zoomWindow
                    var scaledPoint = _zoomWindow.PointFromScreen(point);

                    var diffX = scaledPoint.X - halfWidth;
                    var diffY = scaledPoint.Y - halfHeight;

                    // minimum difference that is considered important
                    const double minDiff = 0.05;
                    if (Math.Abs(diffX) < minDiff && Math.Abs(diffY) < minDiff)
                    {
                        break;
                    }

                    _zoomWindow.Left += diffX;
                    _zoomWindow.Top += diffY;
                }

                // make sure color picker window is on top of just opened zoom window
                AppStateHandler.SetTopMost();
                _zoomWindow.Opacity = 1;
            }

            _zoomViewModel.DesiredHeight = BaseZoomImageSize * _zoomViewModel.ZoomFactor;
            _zoomViewModel.DesiredWidth = BaseZoomImageSize * _zoomViewModel.ZoomFactor;
        }

        private void AppStateHandler_AppClosed(object sender, EventArgs e)
        {
            CloseZoomWindow();
        }
    }
}
