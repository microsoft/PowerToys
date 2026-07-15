// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using ColorPicker.Helpers;
using ColorPicker.Keyboard;
using ColorPicker.Mouse;
using ColorPicker.Settings;
using ColorPicker.ViewModelContracts;
using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PowerToys.Interop;
using Windows.UI;

using Point = Windows.Foundation.Point;

namespace ColorPicker.ViewModels
{
    public class MainViewModel : ObservableObject, IMainViewModel
    {
        private readonly ZoomWindowHelper _zoomWindowHelper;
        private readonly AppStateHandler _appStateHandler;
        private readonly IUserSettings _userSettings;
        private KeyboardMonitor _keyboardMonitor;

        private string _colorText;
        private Brush _colorBrush;
        private string _colorName;

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

            // The three-arg NativeEventWaiter captures the UI-thread DispatcherQueue itself, so it
            // must be constructed on the UI thread (it is, via DI in App.OnLaunched).
            NativeEventWaiter.WaitForEventLoop(
                Constants.TerminateColorPickerSharedEvent(),
                Shutdown,
                exitToken);

            NativeEventWaiter.WaitForEventLoop(
                Constants.ShowColorPickerSharedEvent(),
                _appStateHandler.StartUserSession,
                exitToken);

            NativeEventWaiter.WaitForEventLoop(
                Constants.ColorPickerSendSettingsTelemetryEvent(),
                _userSettings.SendSettingsTelemetry,
                exitToken);

            if (mouseInfoProvider != null)
            {
                SetColorDetails(mouseInfoProvider.CurrentColor);
                mouseInfoProvider.MouseColorChanged += Mouse_ColorChanged;
                mouseInfoProvider.OnPrimaryMouseDown += MouseInfoProvider_OnPrimaryMouseDown;
                mouseInfoProvider.OnMouseWheel += MouseInfoProvider_OnMouseWheel;
                mouseInfoProvider.OnSecondaryMouseUp += MouseInfoProvider_OnSecondaryMouseUp;
                mouseInfoProvider.OnMiddleMouseDown += MouseInfoProvider_OnMiddleMouseDown;
            }

            _userSettings.ShowColorName.PropertyChanged += (s, e) => { OnPropertyChanged(nameof(ShowColorName)); };

            _appStateHandler.EnterPressed += AppStateHandler_EnterPressed;
            _appStateHandler.UserSessionStarted += AppStateHandler_UserSessionStarted;
            _appStateHandler.UserSessionEnded += AppStateHandler_UserSessionEnded;

            // Only start a local keyboard low level hook if running as a standalone.
            // Otherwise, the global keyboard hook from runner will be used to activate Color Picker through ShowColorPickerSharedEvent.
            if (((App)Application.Current).IsRunningDetachedFromPowerToys())
            {
                keyboardMonitor?.Start();
            }
        }

        // WinUI replacement for the WPF Application.Current.Shutdown on the terminate event:
        // cancel the shared exit token (so the event-waiter loops exit) and exit the process.
        private static void Shutdown()
        {
            App.GetService<CancellationTokenSource>()?.Cancel();
            Environment.Exit(0);
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
            MouseInfoProvider_OnPrimaryMouseDown(null, default);
        }

        public Brush ColorBrush
        {
            get => _colorBrush;
            private set
            {
                _colorBrush = value;
                OnPropertyChanged();
            }
        }

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

        private void Mouse_ColorChanged(object sender, System.Drawing.Color color)
        {
            SetColorDetails(color);
        }

        private void MouseInfoProvider_OnPrimaryMouseDown(object sender, IntPtr wParam)
        {
            HandleMouseClickAction(_userSettings.PrimaryClickAction.Value);
        }

        private void MouseInfoProvider_OnMiddleMouseDown(object sender, IntPtr wParam)
        {
            HandleMouseClickAction(_userSettings.MiddleClickAction.Value);
        }

        private void MouseInfoProvider_OnSecondaryMouseUp(object sender, IntPtr wParam)
        {
            HandleMouseClickAction(_userSettings.SecondaryClickAction.Value);
        }

        private void HandleMouseClickAction(ColorPickerClickAction action)
        {
            switch (action)
            {
                case ColorPickerClickAction.PickColorThenEditor:
                    CopyToClipboard(ColorText);
                    UpdateColorHistory(GetColorString());

                    _appStateHandler.OpenColorEditor();

                    break;

                case ColorPickerClickAction.PickColorAndClose:
                    CopyToClipboard(ColorText);
                    UpdateColorHistory(GetColorString());

                    _appStateHandler.EndUserSession();

                    break;

                case ColorPickerClickAction.Close:
                    _appStateHandler.EndUserSession();
                    break;
            }
        }

        private static void CopyToClipboard(string colorText)
        {
            if (!ManagedCommon.ClipboardHelper.TrySetText(colorText, flush: true))
            {
                Logger.LogError("Failed to set text into clipboard");
            }
        }

        private void UpdateColorHistory(string color)
        {
            int oldIndex = _userSettings.ColorHistory.IndexOf(color);
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
        }

        private string GetColorString()
        {
            var color = ((SolidColorBrush)ColorBrush).Color;
            return color.A + "|" + color.R + "|" + color.G + "|" + color.B;
        }

        private void SetColorDetails(System.Drawing.Color color)
        {
            ColorBrush = new SolidColorBrush(new Color { A = color.A, R = color.R, G = color.G, B = color.B });
            ColorText = ColorRepresentationHelper.GetStringRepresentation(color, _userSettings.CopiedColorRepresentation.Value, _userSettings.CopiedColorRepresentationFormat.Value);
            ColorName = ColorRepresentationHelper.GetColorNameFromColorIdentifier(ColorNameHelper.GetColorNameIdentifier(color));
        }

        private void MouseInfoProvider_OnMouseWheel(object sender, Tuple<Point, bool> e)
            => _zoomWindowHelper.Zoom(e.Item1, e.Item2);

        public void RegisterWindowHandle(IntPtr hwnd)
        {
            _appStateHandler.RegisterWindowHandle(hwnd);
        }
    }
}
