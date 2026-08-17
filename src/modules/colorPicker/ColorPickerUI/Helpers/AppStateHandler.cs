// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using ColorPicker.Settings;
using ColorPicker.ViewModelContracts;
using Common.UI;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.UI.Xaml;
using WinUIEx;

using static ColorPicker.Helpers.NativeMethodsHelper;

namespace ColorPicker.Helpers
{
    public class AppStateHandler
    {
        private readonly IColorEditorViewModel _colorEditorViewModel;
        private readonly IUserSettings _userSettings;
        private readonly object _colorPickerVisibilityLock = new object();
        private ColorEditorWindow _colorEditorWindow;
        private bool _colorPickerShown;
        private IntPtr _mainWindowHandle;

        public AppStateHandler(IColorEditorViewModel colorEditorViewModel, IUserSettings userSettings)
        {
            // App.Window (the picking overlay) is created and assigned before the DI graph that
            // resolves this handler, so it is available here.
            if (App.Window != null)
            {
                App.Window.Closed += MainWindow_Closed;
            }

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

                if (!((App)Application.Current).IsRunningDetachedFromPowerToys())
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

                    if (!((App)Application.Current).IsRunningDetachedFromPowerToys())
                    {
                        UserSessionEnded?.Invoke(this, EventArgs.Empty);
                    }

                    SessionEventHelper.End();

                    return true;
                }

                return false;
            }
        }

        public void OpenColorEditor()
        {
            lock (_colorPickerVisibilityLock)
            {
                HideColorPicker();
            }

            ShowColorPickerEditor();
        }

        private void ShowColorPicker()
        {
            if (!_colorPickerShown)
            {
                AppShown?.Invoke(this, EventArgs.Empty);
                (App.Window as ColorPickerOverlayWindow)?.Show();
                _colorPickerShown = true;
            }
        }

        private void HideColorPicker()
        {
            if (_colorPickerShown)
            {
                (App.Window as ColorPickerOverlayWindow)?.Hide();
                AppHidden?.Invoke(this, EventArgs.Empty);
                _colorPickerShown = false;
            }
        }

        private void ShowColorPickerEditor()
        {
            if (_colorEditorWindow == null)
            {
                _colorEditorWindow = new ColorEditorWindow(this);

                // The export commands' FileSavePicker needs the editor window's HWND
                // (InitializeWithWindow); assign it here, once the window exists.
                _colorEditorViewModel.WindowHandle = _colorEditorWindow.GetWindowHandle();
                _colorEditorWindow.ContentPresenter.Content = new Views.ColorEditorView { DataContext = _colorEditorViewModel };
                _colorEditorViewModel.OpenColorPickerRequested += ColorEditorViewModel_OpenColorPickerRequested;
                _colorEditorViewModel.OpenSettingsRequested += ColorEditorViewModel_OpenSettingsRequested;
                _colorEditorViewModel.OpenColorPickerRequested += (object sender, EventArgs e) =>
                {
                    SessionEventHelper.Event.EditorColorPickerOpened = true;
                };
            }

            _colorEditorViewModel.Initialize();
            _colorEditorWindow.Show();
            _colorEditorWindow.Activate();
            SessionEventHelper.Event.EditorOpened = true;
        }

        private void HideColorPickerEditor()
        {
            _colorEditorWindow?.Hide();
        }

        public bool IsColorPickerEditorVisible()
        {
            return _colorEditorWindow != null && _colorEditorWindow.AppWindow.IsVisible;
        }

        public bool IsColorPickerVisible()
        {
            return _colorPickerShown;
        }

        private void MainWindow_Closed(object sender, WindowEventArgs e)
        {
            AppClosed?.Invoke(this, EventArgs.Empty);
        }

        private void ColorEditorViewModel_OpenColorPickerRequested(object sender, EventArgs e)
        {
            lock (_colorPickerVisibilityLock)
            {
                ShowColorPicker();
            }

            _colorEditorWindow?.Hide();
        }

        private void ColorEditorViewModel_OpenSettingsRequested(object sender, EventArgs e)
        {
            SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.ColorPicker);
        }

        internal void RegisterWindowHandle(IntPtr hwnd)
        {
            _mainWindowHandle = hwnd;
        }

        public bool HandleEnterPressed()
        {
            if (!_colorPickerShown)
            {
                return false;
            }

            EnterPressed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public bool HandleEscPressed()
        {
            if (!EditorState.BlockEscapeKeyClosingColorPickerEditor
                && (_colorPickerShown || (_colorEditorWindow != null && _colorEditorWindow.IsActiveWindow)))
            {
                return EndUserSession();
            }

            return false;
        }

        internal void MoveCursor(int xOffset, int yOffset)
        {
            GetCursorPos(out POINT lpPoint);
            lpPoint.X += xOffset;
            lpPoint.Y += yOffset;
            SetCursorPos(lpPoint.X, lpPoint.Y);
        }

        internal IntPtr GetMainWindowHandle() => _mainWindowHandle;
    }
}
