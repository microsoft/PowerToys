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
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Dispatching;

using static ColorPicker.NativeMethods;

using Point = Windows.Foundation.Point;

namespace ColorPicker.Mouse
{
    public class MouseInfoProvider : IMouseInfoProvider
    {
        private const double DefaultDisplayRefreshRate = 60.0;

        // Largest supported sample area, in pixels.
        internal static readonly int MaxSampleSize = ColorPickerProperties.SupportedSampleSizes[^1];

        // Reused GDI surface for the per-tick screen sample, sized for the largest sample area.
        // Allocating a fresh Bitmap + Graphics on every timer tick (which fires at the monitor refresh
        // rate while a pick session is active) churns the GC heap and grows the process working set on
        // each activation; reuse a single cached surface instead (mirrors ZoomWindowHelper's static
        // _bmp/_graphics). All access is on the UI thread (the constructor and the DispatcherQueueTimer tick).
        private static readonly Bitmap _screenPixelBitmap = new Bitmap(MaxSampleSize, MaxSampleSize, PixelFormat.Format32bppArgb);
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
            if (TryGetPixelColor(() => CaptureColor(initPos), out var initialColor))
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

            if (TryGetPixelColor(() => CaptureColor(mousePosition), out var color))
            {
                if (_previousColor != color || _colorFormatChanged)
                {
                    _previousColor = color;
                    _colorFormatChanged = false;
                    MouseColorChanged?.Invoke(this, color);
                }
            }
        }

        private Color CaptureColor(Point position)
        {
            var x = (int)position.X;
            var y = (int)position.Y;
            var sampleSize = ColorPickerProperties.NormalizeSampleSize(_userSettings.SampleSize.Value);

            if (sampleSize == 1)
            {
                _screenPixelGraphics.CopyFromScreen(x, y, 0, 0, new Size(1, 1), CopyPixelOperation.SourceCopy);
                return _screenPixelBitmap.GetPixel(0, 0);
            }

            var sampleArea = GetSampleArea(x, y, sampleSize, GetMonitorBounds(x, y));
            _screenPixelGraphics.CopyFromScreen(sampleArea.X, sampleArea.Y, 0, 0, sampleArea.Size, CopyPixelOperation.SourceCopy);
            return GetAverageColor(_screenPixelBitmap, sampleArea.Width, sampleArea.Height);
        }

        /// <summary>
        /// Returns the <paramref name="sampleSize"/> x <paramref name="sampleSize"/> square centered on
        /// (<paramref name="x"/>, <paramref name="y"/>), clipped to <paramref name="monitorBounds"/> so
        /// that off-screen pixels (captured as black) don't darken the average near a screen edge.
        /// An empty <paramref name="monitorBounds"/> means the bounds are unknown and nothing is clipped.
        /// </summary>
        internal static Rectangle GetSampleArea(int x, int y, int sampleSize, Rectangle monitorBounds)
        {
            var halfSize = sampleSize / 2;
            var sampleArea = new Rectangle(x - halfSize, y - halfSize, sampleSize, sampleSize);
            if (!monitorBounds.IsEmpty)
            {
                sampleArea.Intersect(monitorBounds);
            }

            return sampleArea.IsEmpty ? new Rectangle(x, y, 1, 1) : sampleArea;
        }

        /// <summary>
        /// Returns the average color of the <paramref name="width"/> x <paramref name="height"/> area
        /// in the top-left corner of <paramref name="bitmap"/>, rounding each channel to the nearest value.
        /// The area is read with a single LockBits call: up to 25x25 GetPixel calls per timer tick would be slow.
        /// </summary>
        internal static Color GetAverageColor(Bitmap bitmap, int width, int height)
        {
            var bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var red = 0;
                var green = 0;
                var blue = 0;
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        // Format32bppArgb pixels read as a little-endian 0xAARRGGBB value.
                        var pixel = Marshal.ReadInt32(bitmapData.Scan0, (y * bitmapData.Stride) + (x * 4));
                        red += (pixel >> 16) & 0xFF;
                        green += (pixel >> 8) & 0xFF;
                        blue += pixel & 0xFF;
                    }
                }

                var count = width * height;
                return Color.FromArgb((red + (count / 2)) / count, (green + (count / 2)) / count, (blue + (count / 2)) / count);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }

        private static Rectangle GetMonitorBounds(int x, int y)
        {
            var monitor = MonitorFromPoint(new POINT { x = x, y = y }, MonitorDefaultToNearest);
            var info = new MonitorInfoEx();
            if (monitor == IntPtr.Zero || !GetMonitorInfo(new HandleRef(null, monitor), info))
            {
                return Rectangle.Empty;
            }

            return Rectangle.FromLTRB(info.rcMonitor.left, info.rcMonitor.top, info.rcMonitor.right, info.rcMonitor.bottom);
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
            double refreshRate = DefaultDisplayRefreshRate;

            foreach (var monitor in MonitorResolutionHelper.AllMonitors)
            {
                if (monitor.IsPrimary && EnumDisplaySettingsW(monitor.Name, ENUM_CURRENT_SETTINGS, out DEVMODEW lpDevMode))
                {
                    refreshRate = GetDisplayRefreshRateOrDefault(lpDevMode.dmDisplayFrequency);
                    break;
                }
            }

            return refreshRate;
        }

        // EnumDisplaySettings uses 0 and 1 to represent the hardware default refresh rate.
        internal static double GetDisplayRefreshRateOrDefault(uint displayFrequency)
            => displayFrequency > 1 ? displayFrequency : DefaultDisplayRefreshRate;

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
            OnMouseWheel?.Invoke(this, new Tuple<Point, bool>(GetCursorPosition(), zoomIn));
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
