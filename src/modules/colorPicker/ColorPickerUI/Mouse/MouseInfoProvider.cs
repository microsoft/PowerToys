// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Drawing;
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
        private const double DefaultDisplayRefreshRate = 60.0;
        private static readonly TimeSpan SamplingRetryInterval = TimeSpan.FromMilliseconds(250);

        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private readonly MouseHook _mouseHook;
        private readonly IScreenColorSampler _screenColorSampler;
        private readonly IUserSettings _userSettings;
        private TimeSpan _normalSamplingInterval = TimeSpan.FromMilliseconds(1000.0 / DefaultDisplayRefreshRate);
        private System.Windows.Point _previousMousePosition = new System.Windows.Point(-1, 1);
        private Color _previousColor = Color.Transparent;
        private bool _colorFormatChanged;
        private bool _isDispatchingMouseAction;
        private bool _samplingFailureLogged;

        [ImportingConstructor]
        public MouseInfoProvider(AppStateHandler appStateMonitor, IUserSettings userSettings)
            : this(appStateMonitor, userSettings, new ScreenColorSampler())
        {
        }

        internal MouseInfoProvider(AppStateHandler appStateMonitor, IUserSettings userSettings, IScreenColorSampler screenColorSampler)
        {
            ArgumentNullException.ThrowIfNull(userSettings);
            ArgumentNullException.ThrowIfNull(screenColorSampler);

            // Screen access is intentionally deferred until the picker is shown. The Runner can start
            // while an RDP session does not have a capturable desktop.
            _timer.Interval = _normalSamplingInterval;
            _timer.Tick += Timer_Tick;

            if (appStateMonitor != null)
            {
                appStateMonitor.AppShown += AppStateMonitor_AppShown;
                appStateMonitor.AppClosed += AppStateMonitor_AppClosed;
                appStateMonitor.AppHidden += AppStateMonitor_AppClosed;
            }

            _mouseHook = new MouseHook();
            _screenColorSampler = screenColorSampler;
            _userSettings = userSettings;
            _userSettings.CopiedColorRepresentation.PropertyChanged += CopiedColorRepresentation_PropertyChanged;
        }

        public event EventHandler<Color> MouseColorChanged;

        public event EventHandler<System.Windows.Point> MousePositionChanged;

        public event EventHandler<bool> SampleValidityChanged;

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

        public bool HasValidSample { get; private set; }

        public bool TryPrepareForColorSelection()
            => TryUpdateMouseInfo();

        private void Timer_Tick(object sender, EventArgs e)
        {
            _ = TryUpdateMouseInfo();
        }

        internal bool TryUpdateMouseInfo()
        {
            if (!_screenColorSampler.TryGetCursorPosition(out System.Windows.Point position, out ScreenColorSamplingFailure failure))
            {
                return HandleSamplingFailure(failure);
            }

            bool hadValidSample = HasValidSample;
            bool positionChanged = !hadValidSample || _previousMousePosition != position;

            if (positionChanged)
            {
                // Move the picker away from the cursor before capturing the pixel so the picker
                // cannot become part of its own sample.
                MousePositionChanged?.Invoke(this, position);
            }

            if (!_screenColorSampler.TrySampleColor(position, out Color color, out failure))
            {
                return HandleSamplingFailure(failure);
            }

            bool colorChanged = !hadValidSample || _previousColor != color || _colorFormatChanged;

            _previousMousePosition = position;
            _previousColor = color;
            _colorFormatChanged = false;
            SetSampleValidity(true);
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

        private bool HandleSamplingFailure(ScreenColorSamplingFailure failure)
        {
            InvalidateSample();
            _timer.Interval = SamplingRetryInterval;
            LogSamplingFailure(failure);
            return false;
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
            if (!_isDispatchingMouseAction)
            {
                InvalidateSample();
            }
        }

        private void AppStateMonitor_AppShown(object sender, EventArgs e)
        {
            _normalSamplingInterval = TimeSpan.FromMilliseconds(1000.0 / GetMainDisplayRefreshRate());
            _timer.Interval = _normalSamplingInterval;
            _ = TryUpdateMouseInfo();
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

        private void MouseHook_OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta == 0)
            {
                return;
            }

            if (!TryUpdateMouseInfo())
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

        private bool CanHandleAction(ColorPickerClickAction action)
            => action == ColorPickerClickAction.Close || TryPrepareForColorSelection();

        private bool TryDispatchMouseAction(ColorPickerClickAction action, Action dispatchAction)
        {
            if (!CanHandleAction(action))
            {
                return false;
            }

            DisposeHook();
            _isDispatchingMouseAction = true;
            try
            {
                dispatchAction();
                return true;
            }
            finally
            {
                _isDispatchingMouseAction = false;
                InvalidateSample();
            }
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
            _previousMousePosition = new System.Windows.Point(-1, 1);
            _previousColor = Color.Transparent;
            SetSampleValidity(false);
        }

        private void SetSampleValidity(bool isValid)
        {
            if (HasValidSample == isValid)
            {
                return;
            }

            HasValidSample = isValid;
            SampleValidityChanged?.Invoke(this, isValid);
        }

        private void LogSamplingFailure(ScreenColorSamplingFailure failure)
        {
            if (_samplingFailureLogged)
            {
                return;
            }

            Logger.LogWarning(
                $"Screen color sampling is temporarily unavailable. Reason={failure.Reason}, nativeErrorCode={failure.NativeErrorCode}, message={failure.Message}. Color Picker will retry while active.");
            _samplingFailureLogged = true;
        }
    }
}
