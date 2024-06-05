// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using ColorPicker.Common;
using ColorPicker.Helpers;
using ColorPicker.Keyboard;
using ColorPicker.Mouse;
using ColorPicker.Settings;
using ColorPicker.ViewModelContracts;
using Common.UI;
using interop;
using ManagedCommon;

namespace ColorPicker.ViewModels
{
    [Export(typeof(IMainViewModel))]
    public class MainViewModel : ViewModelBase, IMainViewModel
    {
        private readonly ZoomWindowHelper _zoomWindowHelper;
        private readonly AppStateHandler _appStateHandler;
        private readonly IUserSettings _userSettings;
        private KeyboardMonitor _keyboardMonitor;

        /// <summary>
        /// Backing field for <see cref="OtherColor"/>
        /// </summary>
        private string _colorText;

        /// <summary>
        /// Backing field for <see cref="ColorBrush"/>
        /// </summary>
        private Brush _colorBrush;

        /// <summary>
        /// Backing field for <see cref="ColorName"/>
        /// </summary>
        private string _colorName;

        [ImportingConstructor]
        public MainViewModel(
            IMouseInfoProvider mouseInfoProvider,
            ZoomWindowHelper zoomWindowHelper,
            AppStateHandler appStateHandler,
            KeyboardMonitor keyboardMonitor,
            IUserSettings userSettings,
            CancellationToken exitToken)
        {
            _zoomWindowHelper = zoomWindowHelper;
            _appStateHandler = appStateHandler;
            _userSettings = userSettings;
            _keyboardMonitor = keyboardMonitor;

            NativeEventWaiter.WaitForEventLoop(
                Constants.ShowColorPickerSharedEvent(),
                _appStateHandler.StartUserSession,
                Application.Current.Dispatcher,
                exitToken);

            NativeEventWaiter.WaitForEventLoop(
                Constants.ColorPickerSendSettingsTelemetryEvent(),
                _userSettings.SendSettingsTelemetry,
                Application.Current.Dispatcher,
                exitToken);

            if (mouseInfoProvider != null)
            {
                SetColorDetails(mouseInfoProvider.CurrentColor);
                mouseInfoProvider.MouseColorChanged += Mouse_ColorChanged;
                mouseInfoProvider.OnMouseDown += MouseInfoProvider_OnMouseDown;
                mouseInfoProvider.OnMouseWheel += MouseInfoProvider_OnMouseWheel;
                mouseInfoProvider.OnSecondaryMouseUp += MouseInfoProvider_OnSecondaryMouseUp;
            }

            _userSettings.ShowColorName.PropertyChanged += (s, e) => { OnPropertyChanged(nameof(ShowColorName)); };

            _appStateHandler.EnterPressed += AppStateHandler_EnterPressed;
            _appStateHandler.UserSessionStarted += AppStateHandler_UserSessionStarted;
            _appStateHandler.UserSessionEnded += AppStateHandler_UserSessionEnded;

            // Only start a local keyboard low level hook if running as a standalone.
            // Otherwise, the global keyboard hook from runner will be used to activate Color Picker through ShowColorPickerSharedEvent
            // The appStateHandler starts and disposes a low level hook when ColorPicker is being used.
            // The hook catches the Esc, Space, Enter and Arrow key presses.
            // This is much lighter than using a permanent local low level keyboard hook.
            if ((System.Windows.Application.Current as ColorPickerUI.App).IsRunningDetachedFromPowerToys())
            {
                keyboardMonitor?.Start();
            }
        }

        private void AppStateHandler_UserSessionEnded(object sender, EventArgs e)
        {
            _keyboardMonitor.Dispose();
        }

        private void AppStateHandler_UserSessionStarted(object sender, EventArgs e)
        {
            _keyboardMonitor?.Start();
        }

        private void AppStateHandler_EnterPressed(object sender, EventArgs e)
        {
            MouseInfoProvider_OnMouseDown(null, default(System.Drawing.Point));
        }

        /// <summary>
        /// Gets the current selected color as a <see cref="Brush"/>
        /// </summary>
        public Brush ColorBrush
        {
            get => _colorBrush;
            private set
            {
                _colorBrush = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the text representation of the selected color value
        /// </summary>
        public string ColorText
        {
            get => _colorText;
            private set
            {
                _colorText = value;
                OnPropertyChanged();
            }
        }

        public string ColorName
        {
            get => _colorName;
            private set
            {
                _colorName = value;
                OnPropertyChanged();
            }
        }

        public bool ShowColorName
        {
            get => _userSettings.ShowColorName.Value;
        }

        /// <summary>
        /// Tell the color picker that the color on the position of the mouse cursor have changed
        /// </summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="color">The new <see cref="Color"/> under the mouse cursor</param>
        private void Mouse_ColorChanged(object sender, System.Drawing.Color color)
        {
            SetColorDetails(color);
        }

        /// <summary>
        /// Tell the color picker that the user have press a mouse button (after release the button)
        /// </summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="p">The current <see cref="System.Drawing.Point"/> of the mouse cursor</param>
        private void MouseInfoProvider_OnMouseDown(object sender, System.Drawing.Point p)
        {
            ClipboardHelper.CopyToClipboard(ColorText);

            var color = GetColorString();

            var oldIndex = _userSettings.ColorHistory.IndexOf(color);
            if (oldIndex != -1)
            {
                _userSettings.ColorHistory.Move(oldIndex, 0);
            }
            else
            {
                _userSettings.ColorHistory.Insert(0, color);
            }

            if (_userSettings.ColorHistory.Count > _userSettings.ColorHistoryLimit.Value)
            {
                _userSettings.ColorHistory.RemoveAt(_userSettings.ColorHistory.Count - 1);
            }

            _appStateHandler.OnColorPickerMouseDown();
        }

        private void MouseInfoProvider_OnSecondaryMouseUp(object sender, IntPtr wParam)
        {
            _appStateHandler.EndUserSession();
        }

        private string GetColorString()
        {
            var color = ((SolidColorBrush)ColorBrush).Color;
            return color.A + "|" + color.R + "|" + color.G + "|" + color.B;
        }

        private void SetColorDetails(System.Drawing.Color color)
        {
            ColorBrush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
            ColorText = ColorRepresentationHelper.GetStringRepresentation(color, _userSettings.CopiedColorRepresentation.Value, _userSettings.CopiedColorRepresentationFormat.Value);
            ColorName = ColorRepresentationHelper.GetColorNameFromColorIdentifier(ColorNameHelper.GetColorNameIdentifier(color));
        }

        /// <summary>
        /// Tell the color picker that the user have used the mouse wheel
        /// </summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="e">The new values for the zoom</param>
        private void MouseInfoProvider_OnMouseWheel(object sender, Tuple<Point, bool> e)
            => _zoomWindowHelper.Zoom(e.Item1, e.Item2);

        public void RegisterWindowHandle(System.Windows.Interop.HwndSource hwndSource)
        {
            _appStateHandler.RegisterWindowHandle(hwndSource);
        }
    }
}
