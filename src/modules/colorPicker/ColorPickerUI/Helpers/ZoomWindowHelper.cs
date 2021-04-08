// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using ColorPicker.Telemetry;
using ColorPicker.ViewModelContracts;
using Microsoft.PowerToys.Telemetry;

namespace ColorPicker.Helpers
{
    [Export(typeof(ZoomWindowHelper))]
    public class ZoomWindowHelper
    {
        private const int ZoomWindowChangeDelayInMS = 50;
        private const int ZoomFactor = 2;
        private const int BaseZoomImageSize = 50;
        private const int MaxZoomLevel = 4;
        private const int MinZoomLevel = 0;

        private readonly IZoomViewModel _zoomViewModel;
        private readonly AppStateHandler _appStateHandler;
        private readonly IThrottledActionInvoker _throttledActionInvoker;

        private int _currentZoomLevel;
        private int _previousZoomLevel;

        private ZoomWindow _zoomWindow;

        private double _lastLeft;
        private double _lastTop;

        private double _previousScaledX;
        private double _previousScaledY;

        [ImportingConstructor]
        public ZoomWindowHelper(IZoomViewModel zoomViewModel, AppStateHandler appStateHandler, IThrottledActionInvoker throttledActionInvoker)
        {
            _zoomViewModel = zoomViewModel;
            _appStateHandler = appStateHandler;
            _throttledActionInvoker = throttledActionInvoker;
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
            HideZoomWindow();
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
                var rect = new Rectangle(x, y, BaseZoomImageSize, BaseZoomImageSize);

                using (var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb))
                {
                    var g = Graphics.FromImage(bmp);
                    g.CopyFromScreen(rect.Left, rect.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

                    var bitmapImage = BitmapToImageSource(bmp);

                    _zoomViewModel.ZoomArea = bitmapImage;
                    _zoomViewModel.ZoomFactor = 1;
                }
            }
            else
            {
                var enlarge = (_currentZoomLevel - _previousZoomLevel) > 0 ? true : false;
                var currentZoomFactor = enlarge ? ZoomFactor : 1.0 / ZoomFactor;

                _zoomViewModel.ZoomFactor *= currentZoomFactor;
            }

            ShowZoomWindow((int)point.X, (int)point.Y);
        }

        private static BitmapSource BitmapToImageSource(Bitmap bitmap)
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

        private void HideZoomWindow()
        {
            if (_zoomWindow != null)
            {
                _zoomWindow.Hide();
            }
        }

        private void ShowZoomWindow(int x, int y)
        {
            if (_zoomWindow == null)
            {
                _zoomWindow = new ZoomWindow();
                _zoomWindow.Content = _zoomViewModel;
                _zoomWindow.Loaded += ZoomWindow_Loaded;
                _zoomWindow.IsVisibleChanged += ZoomWindow_IsVisibleChanged;
            }

            // we just started zooming, remember where we opened zoom window
            if (_currentZoomLevel == 1 && _previousZoomLevel == 0)
            {
                var dpi = MonitorResolutionHelper.GetCurrentMonitorDpi();
                _previousScaledX = x / dpi.DpiScaleX;
                _previousScaledY = y / dpi.DpiScaleY;
            }

            _lastLeft = Math.Floor(_previousScaledX - (BaseZoomImageSize * Math.Pow(ZoomFactor, _currentZoomLevel - 1) / 2));
            _lastTop = Math.Floor(_previousScaledY - (BaseZoomImageSize * Math.Pow(ZoomFactor, _currentZoomLevel - 1) / 2));

            var justShown = false;
            if (!_zoomWindow.IsVisible)
            {
                _zoomWindow.Left = _lastLeft;
                _zoomWindow.Top = _lastTop;
                _zoomViewModel.Height = BaseZoomImageSize;
                _zoomViewModel.Width = BaseZoomImageSize;
                _zoomWindow.Show();
                justShown = true;

                // make sure color picker window is on top of just opened zoom window
                AppStateHandler.SetTopMost();
            }

            // dirty hack - sometimes when we just show a window on a second monitor with different DPI,
            // window position is not set correctly on a first time, we need to "ping" it again to make it appear on the proper location
            if (justShown)
            {
                _zoomWindow.Left = _lastLeft + 1;
                _zoomWindow.Top = _lastTop + 1;
                SessionEventHelper.Event.ZoomUsed = true;
            }

            _throttledActionInvoker.ScheduleAction(
            () =>
            {
                _zoomWindow.DesiredLeft = _lastLeft;
                _zoomWindow.DesiredTop = _lastTop;
                _zoomViewModel.DesiredHeight = BaseZoomImageSize * _zoomViewModel.ZoomFactor;
                _zoomViewModel.DesiredWidth = BaseZoomImageSize * _zoomViewModel.ZoomFactor;
            },
            ZoomWindowChangeDelayInMS);
        }

        private void ZoomWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // need to set at this point again, to avoid issues moving between screens with different scaling
            if ((bool)e.NewValue)
            {
                _zoomWindow.Left = _lastLeft;
                _zoomWindow.Top = _lastTop;
            }
        }

        private void ZoomWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // need to call it again at load time, because it does was not dpi aware at the first time of Show() call
            _zoomWindow.Left = _lastLeft;
            _zoomWindow.Top = _lastTop;
        }

        private void AppStateHandler_AppClosed(object sender, EventArgs e)
        {
            CloseZoomWindow();
        }
    }
}
