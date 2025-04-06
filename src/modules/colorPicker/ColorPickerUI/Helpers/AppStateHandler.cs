// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

using ColorPicker.Settings;
using ColorPicker.ViewModelContracts;
using Common.UI;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;

using static ColorPicker.Helpers.NativeMethodsHelper;

namespace ColorPicker.Helpers
{
    [Export(typeof(AppStateHandler))]
    public class AppStateHandler
    {
        private readonly IColorEditorViewModel _colorEditorViewModel;
        private readonly IUserSettings _userSettings;
        private ColorEditorWindow _colorEditorWindow;
        private bool _colorPickerShown;
        private Lock _colorPickerVisibilityLock = new Lock();

        private HwndSource _hwndSource;
        private const int _globalHotKeyId = 0x0001;

        // Blocks using the escape key to close the color picker editor when the adjust color flyout is open.
        public static bool BlockEscapeKeyClosingColorPickerEditor { get; set; }

        [ImportingConstructor]
        public AppStateHandler(IColorEditorViewModel colorEditorViewModel, IUserSettings userSettings)
        {
            Application.Current.MainWindow.Closed += MainWindow_Closed;
            _colorEditorViewModel = colorEditorViewModel;
            _userSettings = userSettings;
        }

        public event EventHandler AppShown;

        public event EventHandler AppHidden;

        public event EventHandler AppClosed;

        public event EventHandler EnterPressed;

        public event EventHandler UserSessionStarted;

        public event EventHandler UserSessionEnded;

        public void StartUserSession()
        {
            EndUserSession(); // Ends current user session if there's an active one.
            lock (_colorPickerVisibilityLock)
            {
                if (!_colorPickerShown && !IsColorPickerEditorVisible())
                {
                    SessionEventHelper.Start(_userSettings.ActivationAction.Value);
                }

                if (_userSettings.ActivationAction.Value == ColorPickerActivationAction.OpenEditor)
                {
                    ShowColorPickerEditor();
                }
                else
                {
                    ShowColorPicker();
                }

                if (!(System.Windows.Application.Current as ColorPickerUI.App).IsRunningDetachedFromPowerToys())
                {
                    UserSessionStarted?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool EndUserSession()
        {
            lock (_colorPickerVisibilityLock)
            {
                if (IsColorPickerEditorVisible() || _colorPickerShown)
                {
                    if (IsColorPickerEditorVisible())
                    {
                        HideColorPickerEditor();
                    }
                    else
                    {
                        HideColorPicker();
                    }

                    if (!(System.Windows.Application.Current as ColorPickerUI.App).IsRunningDetachedFromPowerToys())
                    {
                        UserSessionEnded?.Invoke(this, EventArgs.Empty);
                    }

                    SessionEventHelper.End();

                    return true;
                }

                return false;
            }
        }

        public void OnColorPickerMouseDown()
        {
            if (_userSettings.ActivationAction.Value == ColorPickerActivationAction.OpenColorPickerAndThenEditor || _userSettings.ActivationAction.Value == ColorPickerActivationAction.OpenEditor)
            {
                lock (_colorPickerVisibilityLock)
                {
                    HideColorPicker();
                }

                ShowColorPickerEditor();
            }
            else
            {
                EndUserSession();
            }
        }

        public static void SetTopMost()
        {
            Application.Current.MainWindow.Topmost = false;
            Application.Current.MainWindow.Topmost = true;
        }

        private void ShowColorPicker()
        {
            if (!_colorPickerShown)
            {
                AppShown?.Invoke(this, EventArgs.Empty);
                Application.Current.MainWindow.Opacity = 0;
                Application.Current.MainWindow.Visibility = Visibility.Visible;
                _colorPickerShown = true;
            }
        }

        private void HideColorPicker()
        {
            if (_colorPickerShown)
            {
                Application.Current.MainWindow.Opacity = 0;
                Application.Current.MainWindow.Visibility = Visibility.Collapsed;
                AppHidden?.Invoke(this, EventArgs.Empty);
                _colorPickerShown = false;
            }
        }

        private void ShowColorPickerEditor()
        {
            if (_colorEditorWindow == null)
            {
                _colorEditorWindow = new ColorEditorWindow(this);
                _colorEditorWindow.contentPresenter.Content = _colorEditorViewModel;
                _colorEditorViewModel.OpenColorPickerRequested += ColorEditorViewModel_OpenColorPickerRequested;
                _colorEditorViewModel.OpenSettingsRequested += ColorEditorViewModel_OpenSettingsRequested;
                _colorEditorViewModel.OpenColorPickerRequested += (object sender, EventArgs e) =>
                {
                    SessionEventHelper.Event.EditorColorPickerOpened = true;
                };
            }

            _colorEditorViewModel.Initialize();
            _colorEditorWindow.Show();
            SessionEventHelper.Event.EditorOpened = true;
        }

        private void HideColorPickerEditor()
        {
            if (_colorEditorWindow != null)
            {
                _colorEditorWindow.Hide();
            }
        }

        public bool IsColorPickerEditorVisible()
        {
            if (_colorEditorWindow != null)
            {
                // Check if we are visible and on top. Using focus producing unreliable results the first time the picker is opened.
                return _colorEditorWindow.Topmost && _colorEditorWindow.IsVisible;
            }

            return false;
        }

        public bool IsColorPickerVisible()
        {
            return _colorPickerShown;
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            AppClosed?.Invoke(this, EventArgs.Empty);
        }

        private void ColorEditorViewModel_OpenColorPickerRequested(object sender, EventArgs e)
        {
            lock (_colorPickerVisibilityLock)
            {
                ShowColorPicker();
            }

            _colorEditorWindow.Hide();
        }

        private void ColorEditorViewModel_OpenSettingsRequested(object sender, EventArgs e)
        {
            SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.ColorPicker, false);
        }

        internal void RegisterWindowHandle(System.Windows.Interop.HwndSource hwndSource)
        {
            _hwndSource = hwndSource;
        }

        public bool HandleEnterPressed()
        {
            if (!IsColorPickerVisible())
            {
                return false;
            }

            EnterPressed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public bool HandleEscPressed()
        {
            if (!BlockEscapeKeyClosingColorPickerEditor)
            {
                return EndUserSession();
            }
            else
            {
                return false;
            }
        }

        internal void MoveCursor(int xOffset, int yOffset)
        {
            POINT lpPoint;
            GetCursorPos(out lpPoint);
            lpPoint.X += xOffset;
            lpPoint.Y += yOffset;
            SetCursorPos(lpPoint.X, lpPoint.Y);
        }
    }
}
