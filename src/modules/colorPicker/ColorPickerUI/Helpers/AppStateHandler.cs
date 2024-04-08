// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Interop;
using ColorPicker.Settings;
using ColorPicker.ViewModelContracts;
using Common.UI;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;

namespace ColorPicker.Helpers
{
    [Export(typeof(AppStateHandler))]
    public class AppStateHandler
    {
        private readonly IColorEditorViewModel _colorEditorViewModel;
        private readonly IUserSettings _userSettings;
        private ColorEditorWindow _colorEditorWindow;
        private bool _colorPickerShown;
        private object _colorPickerVisibilityLock = new object();

        private HwndSource _hwndSource;
        private const int _globalEscHotKeyId = 0x0001;
        private const int _globalEnterHotKeyId = 0x0002;
        private const int _globalSpaceHotKeyId = 0x0003;

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

                // Handle the escape key to close Color Picker locally when being spawn from PowerToys, since Keyboard Hooks from the KeyboardMonitor are heavy.
                if (!(System.Windows.Application.Current as ColorPickerUI.App).IsRunningDetachedFromPowerToys())
                {
                    SetupGlobalKeyShortcuts();
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

                    // Handle the escape key to close Color Picker locally when being spawn from PowerToys, since Keyboard Hooks from the KeyboardMonitor are heavy.
                    if (!(System.Windows.Application.Current as ColorPickerUI.App).IsRunningDetachedFromPowerToys())
                    {
                        ClearGlobalKeyShortcuts();
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

        public bool HandleEnterPressed()
        {
            if (!IsColorPickerVisible())
            {
                return false;
            }

            EnterPressed?.Invoke(this, EventArgs.Empty);
            return true;
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

        public IntPtr ProcessWindowMessages(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            switch (msg)
            {
                case NativeMethods.WM_HOTKEY:
                    switch (wparam)
                    {
                        case _globalEscHotKeyId:
                            if (!BlockEscapeKeyClosingColorPickerEditor)
                            {
                                handled = EndUserSession();
                            }
                            else
                            {
                                // If escape key is blocked it means a submenu is open.
                                // Send the escape key to the Window to close that submenu.
                                // Description for LPARAM in https://learn.microsoft.com/windows/win32/inputdev/wm-keyup#parameters
                                // It's basically some modifiers + scancode for escape (1) + number of repetitions (1)
                                handled = true;
                                handled &= NativeMethods.PostMessage(_hwndSource.Handle, NativeMethods.WM_KEYDOWN, (IntPtr)NativeMethods.VK_ESCAPE, (IntPtr)0x00010001);

                                // Need to make the value unchecked as a workaround for changes introduced in .NET 7
                                // https://github.com/dotnet/roslyn/blob/main/docs/compilers/CSharp/Compiler%20Breaking%20Changes%20-%20DotNet%207.md#checked-operators-on-systemintptr-and-systemuintptr
                                handled &= NativeMethods.PostMessage(_hwndSource.Handle, NativeMethods.WM_KEYUP, (IntPtr)NativeMethods.VK_ESCAPE, unchecked((IntPtr)0xC0010001));
                            }

                            break;

                        case _globalEnterHotKeyId:
                        case _globalSpaceHotKeyId:
                            handled = HandleEnterPressed();
                            break;
                    }

                    break;
            }

            return IntPtr.Zero;
        }

        public void SetupGlobalKeyShortcuts()
        {
            if (_hwndSource == null)
            {
                return;
            }

            _hwndSource.AddHook(ProcessWindowMessages);
            if (!NativeMethods.RegisterHotKey(_hwndSource.Handle, _globalEscHotKeyId, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_ESCAPE))
            {
                Logger.LogWarning("Couldn't register the hotkey for Esc.");
            }

            if (!NativeMethods.RegisterHotKey(_hwndSource.Handle, _globalEnterHotKeyId, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_RETURN))
            {
                Logger.LogWarning("Couldn't register the hotkey for Enter.");
            }

            if (!NativeMethods.RegisterHotKey(_hwndSource.Handle, _globalSpaceHotKeyId, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_SPACE))
            {
                Logger.LogWarning("Couldn't register the hotkey for Space.");
            }
        }

        public void ClearGlobalKeyShortcuts()
        {
            if (_hwndSource == null)
            {
                return;
            }

            if (!NativeMethods.UnregisterHotKey(_hwndSource.Handle, _globalEscHotKeyId))
            {
                Logger.LogWarning("Couldn't unregister the hotkey for Esc.");
            }

            if (!NativeMethods.UnregisterHotKey(_hwndSource.Handle, _globalEnterHotKeyId))
            {
                Logger.LogWarning("Couldn't unregister the hotkey for Enter.");
            }

            if (!NativeMethods.UnregisterHotKey(_hwndSource.Handle, _globalSpaceHotKeyId))
            {
                Logger.LogWarning("Couldn't unregister the hotkey for Space.");
            }

            _hwndSource.RemoveHook(ProcessWindowMessages);
        }
    }
}
