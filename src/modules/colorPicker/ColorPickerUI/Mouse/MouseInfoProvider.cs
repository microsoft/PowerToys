// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Input;
using System.Windows.Threading;

using ColorPicker.Helpers;
using ColorPicker.Settings;

using static ColorPicker.NativeMethods;

namespace ColorPicker.Mouse
{
    [Export(typeof(IMouseInfoProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class MouseInfoProvider : IMouseInfoProvider, IDisposable
    {
        private readonly double _mousePullInfoIntervalInMs;
        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private readonly MouseHook _mouseHook;
        private readonly IUserSettings _userSettings;
        private readonly AppStateHandler _appStateMonitor; // Store AppStateHandler to unsubscribe
        private System.Windows.Point _previousMousePosition = new System.Windows.Point(-1, 1);
        private Color _previousColor = Color.Transparent;
        private bool _colorFormatChanged;
        private bool _disposed = false;

        [ImportingConstructor]
        public MouseInfoProvider(AppStateHandler appStateMonitor, IUserSettings userSettings)
        {
            _appStateMonitor = appStateMonitor; // Store for later unsubscribe
            _userSettings = userSettings;

            _mousePullInfoIntervalInMs = 1000.0 / GetMainDisplayRefreshRate();
            _timer.Interval = TimeSpan.FromMilliseconds(_mousePullInfoIntervalInMs);
            _timer.Tick += Timer_Tick;

            if (_appStateMonitor != null)
            {
                _appStateMonitor.AppShown += AppStateMonitor_AppShown;
                _appStateMonitor.AppClosed += AppStateMonitor_AppClosed;
                _appStateMonitor.AppHidden += AppStateMonitor_AppClosed; // Assuming AppClosed handler is appropriate for AppHidden as well
            }

            _mouseHook = new MouseHook();
            _userSettings.CopiedColorRepresentation.PropertyChanged += CopiedColorRepresentation_PropertyChanged;
            _previousMousePosition = GetCursorPosition();
            _previousColor = GetPixelColor(_previousMousePosition);
        }

        public event EventHandler<Color> MouseColorChanged;

        public event EventHandler<System.Windows.Point> MousePositionChanged;

        public event EventHandler<Tuple<System.Windows.Point, bool>> OnMouseWheel;

        public event MouseUpEventHandler OnMouseDown;

        public event SecondaryMouseUpEventHandler OnSecondaryMouseUp;

        public System.Windows.Point CurrentPosition
        {
            get
            {
                return _previousMousePosition;
            }
        }

        public Color CurrentColor
        {
            get
            {
                return _previousColor;
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
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

        private static Color GetPixelColor(System.Windows.Point mousePosition)
        {
            try
            {
                var rect = new Rectangle((int)mousePosition.X, (int)mousePosition.Y, 1, 1);
                using (var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp)) // Ensure Graphics object is disposed
                    {
                        g.CopyFromScreen(rect.Left, rect.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
                    }

                    return bmp.GetPixel(0, 0);
                }
            }
            catch (Exception ex) // Catches potential errors during screen capture (e.g., on secure desktops)
            {
                // Optional: Log the exception (ex) if a logging mechanism is available
                // System.Diagnostics.Debug.WriteLine($"Error getting pixel color: {ex.Message}");
                return Color.Transparent; // Return a default color on failure
            }
        }

        private static System.Windows.Point GetCursorPosition()
        {
            GetCursorPos(out PointInter lpPoint);
            return (System.Windows.Point)lpPoint;
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
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }

            _mouseHook.OnMouseDown += MouseHook_OnMouseDown;
            _mouseHook.OnMouseWheel += MouseHook_OnMouseWheel;
            _mouseHook.OnSecondaryMouseUp += MouseHook_OnSecondaryMouseUp;

            if (_userSettings.ChangeCursor.Value)
            {
                CursorManager.SetColorPickerCursor();
            }
        }

        private void MouseHook_OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta == 0)
            {
                return;
            }

            var zoomIn = e.Delta > 0;
            OnMouseWheel?.Invoke(this, new Tuple<System.Windows.Point, bool>(_previousMousePosition, zoomIn));
        }

        private void MouseHook_OnMouseDown(object sender, Point p)
        {
            DisposeHook();
            OnMouseDown?.Invoke(this, p);
        }

        private void MouseHook_OnSecondaryMouseUp(object sender, IntPtr wParam)
        {
            DisposeHook();
            OnSecondaryMouseUp?.Invoke(this, wParam);
        }

        private void CopiedColorRepresentation_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            _colorFormatChanged = true;
        }

        private void DisposeHook()
        {
            if (_timer.IsEnabled)
            {
                _timer.Stop();
            }

            _previousMousePosition = new System.Windows.Point(-1, 1);
            _mouseHook.OnMouseDown -= MouseHook_OnMouseDown;
            _mouseHook.OnMouseWheel -= MouseHook_OnMouseWheel;
            _mouseHook.OnSecondaryMouseUp -= MouseHook_OnSecondaryMouseUp;

            if (_userSettings.ChangeCursor.Value)
            {
                CursorManager.RestoreOriginalCursors();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Dispose managed resources
                DisposeHook(); // Call this first to stop active operations

                _timer.Tick -= Timer_Tick;

                if (_userSettings != null && _userSettings.CopiedColorRepresentation != null)
                {
                    _userSettings.CopiedColorRepresentation.PropertyChanged -= CopiedColorRepresentation_PropertyChanged;
                }

                if (_appStateMonitor != null)
                {
                    _appStateMonitor.AppShown -= AppStateMonitor_AppShown;
                    _appStateMonitor.AppClosed -= AppStateMonitor_AppClosed;
                    _appStateMonitor.AppHidden -= AppStateMonitor_AppClosed; // Assuming this was the intended unsubscribe
                }

                _mouseHook?.Dispose();
            }
            // No unmanaged resources to clean up directly
            _disposed = true;
        }

        ~MouseInfoProvider()
        {
            Dispose(false);
        }
    }
}
