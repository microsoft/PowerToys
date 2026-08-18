// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Threading;

using ColorPicker.Helpers;
using ColorPicker.Settings;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;

using static ColorPicker.NativeMethods;

namespace ColorPicker.Mouse
{
    [Export(typeof(IMouseInfoProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class MouseInfoProvider : IMouseInfoProvider
    {
        internal delegate bool TryGetCursorPositionDelegate(out System.Windows.Point position, out int nativeErrorCode);

        internal delegate bool TrySampleColorDelegate(System.Windows.Point position, out Color color, out int nativeErrorCode);

        private const double DefaultDisplayRefreshRate = 60.0;
        private static readonly TimeSpan SamplingRetryInterval = TimeSpan.FromMilliseconds(250);
        private static readonly System.Windows.Point InvalidMousePosition = new System.Windows.Point(-1, 1);

        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private readonly MouseHook _mouseHook;
        private readonly TryGetCursorPositionDelegate _tryGetCursorPosition;
        private readonly TrySampleColorDelegate _trySampleColor;
        private readonly IUserSettings _userSettings;
        private TimeSpan _normalSamplingInterval = TimeSpan.FromMilliseconds(1000.0 / DefaultDisplayRefreshRate);
        private System.Windows.Point _previousMousePosition = InvalidMousePosition;
        private Color _previousColor = Color.Transparent;
        private bool _colorFormatChanged;
        private bool _hasValidSample;
        private bool _samplingFailureLogged;

        [ImportingConstructor]
        public MouseInfoProvider(AppStateHandler appStateMonitor, IUserSettings userSettings)
            : this(appStateMonitor, userSettings, TryGetCursorPosition, TrySampleColor)
        {
        }

        internal MouseInfoProvider(
            AppStateHandler appStateMonitor,
            IUserSettings userSettings,
            TryGetCursorPositionDelegate tryGetCursorPosition,
            TrySampleColorDelegate trySampleColor)
        {
            ArgumentNullException.ThrowIfNull(userSettings);
            ArgumentNullException.ThrowIfNull(tryGetCursorPosition);
            ArgumentNullException.ThrowIfNull(trySampleColor);

            // Cursor and screen access is intentionally deferred until the picker is shown.
            _timer.Interval = _normalSamplingInterval;
            _timer.Tick += Timer_Tick;

            if (appStateMonitor != null)
            {
                appStateMonitor.AppShown += AppStateMonitor_AppShown;
                appStateMonitor.AppClosed += AppStateMonitor_AppClosed;
                appStateMonitor.AppHidden += AppStateMonitor_AppClosed;
                appStateMonitor.EnterPressed += AppStateMonitor_EnterPressed;
            }

            _mouseHook = new MouseHook();
            _tryGetCursorPosition = tryGetCursorPosition;
            _trySampleColor = trySampleColor;
            _userSettings = userSettings;
            _userSettings.CopiedColorRepresentation.PropertyChanged += CopiedColorRepresentation_PropertyChanged;
        }

        public event EventHandler<Color> MouseColorChanged;

        public event EventHandler<System.Windows.Point> MousePositionChanged;

        public event EventHandler SampleUnavailable;

        public event EventHandler<Tuple<System.Windows.Point, bool>> OnMouseWheel;

        public event PrimaryMouseDownEventHandler OnPrimaryMouseDown;

        public event SecondaryMouseUpEventHandler OnSecondaryMouseUp;

        public event MiddleMouseDownEventHandler OnMiddleMouseDown;

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

        internal bool TryRefreshSample()
        {
            if (!_tryGetCursorPosition(out System.Windows.Point position, out int nativeErrorCode))
            {
                return HandleSamplingFailure("GetCursorPos", nativeErrorCode);
            }

            bool positionChanged = !_hasValidSample || _previousMousePosition != position;
            if (positionChanged)
            {
                // Move the picker away before capturing so it cannot become part of its own sample.
                MousePositionChanged?.Invoke(this, position);
            }

            if (!_trySampleColor(position, out Color color, out nativeErrorCode))
            {
                return HandleSamplingFailure("CopyFromScreen", nativeErrorCode);
            }

            bool colorChanged = !_hasValidSample || _previousColor != color || _colorFormatChanged;

            _previousMousePosition = position;
            _previousColor = color;
            _colorFormatChanged = false;
            _hasValidSample = true;
            _timer.Interval = _normalSamplingInterval;

            if (_samplingFailureLogged)
            {
                Logger.LogInfo("Screen color sampling recovered.");
                _samplingFailureLogged = false;
            }

            if (colorChanged)
            {
                MouseColorChanged?.Invoke(this, color);
            }

            return true;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _ = TryRefreshSample();
        }

        private bool HandleSamplingFailure(string operation, int nativeErrorCode)
        {
            InvalidateSample();
            _timer.Interval = SamplingRetryInterval;

            if (!_samplingFailureLogged)
            {
                Logger.LogWarning(
                    $"Screen color sampling is temporarily unavailable. Operation={operation}, nativeErrorCode={nativeErrorCode}. Color Picker will retry while active.");
                _samplingFailureLogged = true;
            }

            return false;
        }

        private void AppStateMonitor_AppClosed(object sender, EventArgs e)
        {
            DisposeHook();
            InvalidateSample();
            _samplingFailureLogged = false;
        }

        private void AppStateMonitor_AppShown(object sender, EventArgs e)
        {
            _normalSamplingInterval = TimeSpan.FromMilliseconds(1000.0 / GetMainDisplayRefreshRate());
            _timer.Interval = _normalSamplingInterval;
            _ = TryRefreshSample();
            if (!_timer.IsEnabled)
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

        private void AppStateMonitor_EnterPressed(object sender, EventArgs e)
        {
            _ = TryHandlePrimaryMouseDown(IntPtr.Zero);
        }

        private void MouseHook_OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta == 0 || !TryRefreshSample())
            {
                return;
            }

            var zoomIn = e.Delta > 0;
            OnMouseWheel?.Invoke(this, new Tuple<System.Windows.Point, bool>(_previousMousePosition, zoomIn));
        }

        private void MouseHook_OnPrimaryMouseDown(object sender, IntPtr wParam)
        {
            _ = TryHandlePrimaryMouseDown(wParam);
        }

        internal bool TryHandlePrimaryMouseDown(IntPtr wParam)
        {
            return TryDispatchMouseAction(
                _userSettings.PrimaryClickAction.Value,
                () => OnPrimaryMouseDown?.Invoke(this, wParam));
        }

        private void MouseHook_OnSecondaryMouseUp(object sender, IntPtr wParam)
        {
            _ = TryDispatchMouseAction(
                _userSettings.SecondaryClickAction.Value,
                () => OnSecondaryMouseUp?.Invoke(this, wParam));
        }

        private void MouseHook_OnMiddleMouseDown(object sender, IntPtr wParam)
        {
            _ = TryDispatchMouseAction(
                _userSettings.MiddleClickAction.Value,
                () => OnMiddleMouseDown?.Invoke(this, wParam));
        }

        private bool TryDispatchMouseAction(ColorPickerClickAction action, Action dispatchAction)
        {
            if (action != ColorPickerClickAction.Close && !TryRefreshSample())
            {
                return false;
            }

            DisposeHook();
            dispatchAction();
            return true;
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

            _mouseHook.OnPrimaryMouseDown -= MouseHook_OnPrimaryMouseDown;
            _mouseHook.OnMouseWheel -= MouseHook_OnMouseWheel;
            _mouseHook.OnSecondaryMouseUp -= MouseHook_OnSecondaryMouseUp;
            _mouseHook.OnMiddleMouseDown -= MouseHook_OnMiddleMouseDown;

            if (_userSettings.ChangeCursor.Value)
            {
                CursorManager.RestoreOriginalCursors();
            }
        }

        private void InvalidateSample()
        {
            bool hadValidSample = _hasValidSample;
            _previousMousePosition = InvalidMousePosition;
            _previousColor = Color.Transparent;
            _hasValidSample = false;

            if (hadValidSample)
            {
                SampleUnavailable?.Invoke(this, EventArgs.Empty);
            }
        }

        private static bool TryGetCursorPosition(out System.Windows.Point position, out int nativeErrorCode)
        {
            if (!GetCursorPos(out PointInter cursorPosition))
            {
                position = default;
                nativeErrorCode = Marshal.GetLastWin32Error();
                return false;
            }

            position = (System.Windows.Point)cursorPosition;
            nativeErrorCode = 0;
            return true;
        }

        private static bool TrySampleColor(System.Windows.Point position, out Color color, out int nativeErrorCode)
        {
            try
            {
                color = GetPixelColor(position);
                nativeErrorCode = 0;
                return true;
            }
            catch (Win32Exception ex)
            {
                color = default;
                nativeErrorCode = ex.NativeErrorCode;
                return false;
            }
        }

        private static Color GetPixelColor(System.Windows.Point mousePosition)
        {
            var rect = new Rectangle((int)mousePosition.X, (int)mousePosition.Y, 1, 1);
            using (var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(bmp))
                {
                    graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
                }

                return bmp.GetPixel(0, 0);
            }
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
    }
}
