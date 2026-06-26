// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Imaging;

using ColorPicker.Helpers;
using ColorPicker.Settings;
using Microsoft.UI.Dispatching;

using static ColorPicker.NativeMethods;

using Point = Windows.Foundation.Point;

namespace ColorPicker.Mouse
{
    public class MouseInfoProvider : IMouseInfoProvider
    {
        private readonly double _mousePullInfoIntervalInMs;
        private readonly DispatcherQueueTimer _timer;
        private readonly MouseHook _mouseHook;
        private readonly IUserSettings _userSettings;
        private Point _previousMousePosition = new Point(-1, 1);
        private Color _previousColor = Color.Transparent;
        private bool _colorFormatChanged;

        public MouseInfoProvider(AppStateHandler appStateMonitor, IUserSettings userSettings)
        {
            _mousePullInfoIntervalInMs = 1000.0 / GetMainDisplayRefreshRate();

            // WPF DispatcherTimer -> the UI-thread DispatcherQueueTimer. Resolve on the UI thread.
            _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(_mousePullInfoIntervalInMs);
            _timer.Tick += Timer_Tick;

            if (appStateMonitor != null)
            {
                appStateMonitor.AppShown += AppStateMonitor_AppShown;
                appStateMonitor.AppClosed += AppStateMonitor_AppClosed;
                appStateMonitor.AppHidden += AppStateMonitor_AppClosed;
            }

            _mouseHook = new MouseHook();
            _userSettings = userSettings;
            _userSettings.CopiedColorRepresentation.PropertyChanged += CopiedColorRepresentation_PropertyChanged;
            _previousMousePosition = GetCursorPosition();
            _previousColor = GetPixelColor(_previousMousePosition);
        }

        public event EventHandler<Color> MouseColorChanged;

        public event EventHandler<Point> MousePositionChanged;

        public event EventHandler<Tuple<Point, bool>> OnMouseWheel;

        public event PrimaryMouseDownEventHandler OnPrimaryMouseDown;

        public event SecondaryMouseUpEventHandler OnSecondaryMouseUp;

        public event MiddleMouseDownEventHandler OnMiddleMouseDown;

        public Point CurrentPosition => _previousMousePosition;

        public Color CurrentColor => _previousColor;

        private void Timer_Tick(DispatcherQueueTimer sender, object args)
        {
            UpdateMouseInfo();
        }

        private void UpdateMouseInfo()
        {
            var mousePosition = GetCursorPosition();
            if (_previousMousePosition != mousePosition)
            {
                _previousMousePosition = mousePosition;
                MousePositionChanged?.Invoke(this, mousePosition);
            }

            var color = GetPixelColor(mousePosition);
            if (_previousColor != color || _colorFormatChanged)
            {
                _previousColor = color;
                _colorFormatChanged = false;
                MouseColorChanged?.Invoke(this, color);
            }
        }

        private static Color GetPixelColor(Point mousePosition)
        {
            var rect = new Rectangle((int)mousePosition.X, (int)mousePosition.Y, 1, 1);
            using (var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(rect.Left, rect.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
                }

                return bmp.GetPixel(0, 0);
            }
        }

        private static Point GetCursorPosition()
        {
            GetCursorPos(out PointInter lpPoint);
            return (Point)lpPoint;
        }

        private static double GetMainDisplayRefreshRate()
        {
            double refreshRate = 60.0;

            foreach (var monitor in MonitorResolutionHelper.AllMonitors)
            {
                if (monitor.IsPrimary && EnumDisplaySettingsW(monitor.Name, ENUM_CURRENT_SETTINGS, out DEVMODEW lpDevMode))
                {
                    refreshRate = (double)lpDevMode.dmDisplayFrequency;
                    break;
                }
            }

            return refreshRate;
        }

        private void AppStateMonitor_AppClosed(object sender, EventArgs e)
        {
            DisposeHook();
        }

        private void AppStateMonitor_AppShown(object sender, EventArgs e)
        {
            UpdateMouseInfo();
            if (!_timer.IsRunning)
            {
                _timer.Start();
            }

            _mouseHook.OnPrimaryMouseDown += MouseHook_OnPrimaryMouseDown;
            _mouseHook.OnMouseWheel += MouseHook_OnMouseWheel;
            _mouseHook.OnSecondaryMouseUp += MouseHook_OnSecondaryMouseUp;
            _mouseHook.OnMiddleMouseDown += MouseHook_OnMiddleMouseDown;

            if (_userSettings.ChangeCursor.Value)
            {
                CursorManager.SetColorPickerCursor();
            }
        }

        private void MouseHook_OnMouseWheel(object sender, int delta)
        {
            if (delta == 0)
            {
                return;
            }

            var zoomIn = delta > 0;
            OnMouseWheel?.Invoke(this, new Tuple<Point, bool>(_previousMousePosition, zoomIn));
        }

        private void MouseHook_OnPrimaryMouseDown(object sender, IntPtr wParam)
        {
            DisposeHook();
            OnPrimaryMouseDown?.Invoke(this, wParam);
        }

        private void MouseHook_OnSecondaryMouseUp(object sender, IntPtr wParam)
        {
            DisposeHook();
            OnSecondaryMouseUp?.Invoke(this, wParam);
        }

        private void MouseHook_OnMiddleMouseDown(object sender, IntPtr wParam)
        {
            DisposeHook();
            OnMiddleMouseDown?.Invoke(this, wParam);
        }

        private void CopiedColorRepresentation_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            _colorFormatChanged = true;
        }

        private void DisposeHook()
        {
            if (_timer.IsRunning)
            {
                _timer.Stop();
            }

            _previousMousePosition = new Point(-1, 1);
            _mouseHook.OnPrimaryMouseDown -= MouseHook_OnPrimaryMouseDown;
            _mouseHook.OnMouseWheel -= MouseHook_OnMouseWheel;
            _mouseHook.OnSecondaryMouseUp -= MouseHook_OnSecondaryMouseUp;
            _mouseHook.OnMiddleMouseDown -= MouseHook_OnMiddleMouseDown;

            if (_userSettings.ChangeCursor.Value)
            {
                CursorManager.RestoreOriginalCursors();
            }
        }
    }
}
