// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

using ColorPicker.Helpers;
using ColorPicker.Settings;
using Microsoft.UI.Dispatching;

using static ColorPicker.NativeMethods;

using Point = Windows.Foundation.Point;

namespace ColorPicker.Mouse
{
    public class MouseInfoProvider : IMouseInfoProvider
    {
        // Reused 1x1 GDI surface for the per-tick screen-pixel sample. Allocating a fresh Bitmap +
        // Graphics on every timer tick (which fires at the monitor refresh rate while a pick session
        // is active) churns the GC heap and grows the process working set on each activation; reuse a
        // single cached surface instead (mirrors ZoomWindowHelper's static _bmp/_graphics). All access
        // is on the UI thread (the constructor and the DispatcherQueueTimer tick).
        private static readonly Bitmap _screenPixelBitmap = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        private static readonly Graphics _screenPixelGraphics = Graphics.FromImage(_screenPixelBitmap);

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
            var initPos = _previousMousePosition;
            if (TryGetPixelColor(
                () =>
                {
                    _screenPixelGraphics.CopyFromScreen((int)initPos.X, (int)initPos.Y, 0, 0, _screenPixelBitmap.Size, CopyPixelOperation.SourceCopy);
                    return _screenPixelBitmap.GetPixel(0, 0);
                },
                out var initialColor))
            {
                _previousColor = initialColor;
            }

            // else _previousColor stays Color.Transparent (field initializer default)
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

            if (TryGetPixelColor(
                () =>
                {
                    _screenPixelGraphics.CopyFromScreen((int)mousePosition.X, (int)mousePosition.Y, 0, 0, _screenPixelBitmap.Size, CopyPixelOperation.SourceCopy);
                    return _screenPixelBitmap.GetPixel(0, 0);
                },
                out var color))
            {
                if (_previousColor != color || _colorFormatChanged)
                {
                    _previousColor = color;
                    _colorFormatChanged = false;
                    MouseColorChanged?.Invoke(this, color);
                }
            }
        }

        /// <summary>
        /// Attempts to obtain a pixel colour using the supplied <paramref name="captureFunc"/>.
        /// Returns <see langword="true"/> and sets <paramref name="color"/> on success.
        /// Returns <see langword="false"/> (and <paramref name="color"/> = <see cref="Color.Transparent"/>)
        /// when <paramref name="captureFunc"/> throws <see cref="Win32Exception"/> or
        /// <see cref="ExternalException"/> — the expected GDI failure modes.
        /// Any other exception propagates to the caller unchanged.
        /// </summary>
        internal static bool TryGetPixelColor(Func<Color> captureFunc, out Color color)
        {
            color = Color.Transparent;
            try
            {
                color = captureFunc();
                return true;
            }
            catch (Win32Exception)
            {
                // GDI CopyFromScreen: "the handle is invalid" when no desktop DC is available
                // (non-interactive / disconnected session, or before the desktop is ready).
                return false;
            }
            catch (ExternalException)
            {
                // GDI+ returns a non-Ok status code (e.g. OutOfMemory, Aborted).
                return false;
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
