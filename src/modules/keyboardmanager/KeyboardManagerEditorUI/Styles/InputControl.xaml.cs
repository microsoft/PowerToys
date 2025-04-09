// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using KeyboardManagerEditorUI.Interop;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace KeyboardManagerEditorUI.Styles
{
    public sealed partial class InputControl : UserControl, IDisposable
    {
        // List to store pressed keys
        private HashSet<VirtualKey> _currentlyPressedKeys = new HashSet<VirtualKey>();

        // List to store order of remapped keys
        private List<VirtualKey> _keyPressOrder = new List<VirtualKey>();

        // Collection to store original and remapped keys
        private ObservableCollection<string> _originalKeys = new ObservableCollection<string>();
        private ObservableCollection<string> _remappedKeys = new ObservableCollection<string>();

        private HotkeySettingsControlHook? _keyboardHook;
        private bool _disposed;

        // Define newMode as a DependencyProperty for binding
        public static readonly DependencyProperty NewModeProperty =
            DependencyProperty.Register(
                "NewMode",
                typeof(bool),
                typeof(InputControl),
                new PropertyMetadata(false, OnNewModeChanged));

        public bool NewMode
        {
            get { return (bool)GetValue(NewModeProperty); }
            set { SetValue(NewModeProperty, value); }
        }

        public InputControl()
        {
            this.InitializeComponent();

            this.OriginalKeys.ItemsSource = _originalKeys;
            this.RemappedKeys.ItemsSource = _remappedKeys;

            this.Unloaded += InputControl_Unloaded;

            // Set the default focus state
            OriginalToggleBtn.IsChecked = true;

            // Ensure AllAppsCheckBox is in the correct state initially
            UpdateAllAppsCheckBoxState();
        }

        private void InputControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Reset the control when it is unloaded
            Reset();
        }

        private void InputControl_KeyDown(int key)
        {
            // if no keys are pressed, clear the lists when a new key is pressed
            if (_currentlyPressedKeys.Count == 0)
            {
                if (NewMode)
                {
                    _remappedKeys.Clear();
                }
                else
                {
                    _originalKeys.Clear();
                }

                _keyPressOrder.Clear();
            }

            VirtualKey virtualKey = (VirtualKey)key;

            if (_currentlyPressedKeys.Add(virtualKey))
            {
                _keyPressOrder.Add(virtualKey);
                UpdateKeysDisplay();
            }
        }

        private void InputControl_KeyUp(int key)
        {
            VirtualKey virtualKey = (VirtualKey)key;

            if (_currentlyPressedKeys.Remove(virtualKey))
            {
                _keyPressOrder.Remove(virtualKey);
            }
        }

        private static void OnNewModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is InputControl control)
            {
                // Clear the lists when the mode changes
                control._currentlyPressedKeys.Clear();
                control._keyPressOrder.Clear();
            }
        }

        public void SetKeyboardHook()
        {
            CleanupKeyboardHook();

            _keyboardHook = new HotkeySettingsControlHook(
                InputControl_KeyDown,
                InputControl_KeyUp,
                () => true,
                (key, extraInfo) => true);
        }

        public void CleanupKeyboardHook()
        {
            if (_keyboardHook != null)
            {
                _keyboardHook.Dispose();
                _keyboardHook = null;

                _currentlyPressedKeys.Clear();
                _keyPressOrder.Clear();
            }
        }

        private void UpdateKeysDisplay()
        {
            var formattedKeys = GetFormattedKeyList();

            if (NewMode)
            {
                _remappedKeys.Clear();
                foreach (var key in formattedKeys)
                {
                    _remappedKeys.Add(key);
                }
            }
            else
            {
                _originalKeys.Clear();
                foreach (var key in formattedKeys)
                {
                    _originalKeys.Add(key);
                }

                UpdateAllAppsCheckBoxState();
            }
        }

        private List<string> GetFormattedKeyList()
        {
            List<string> keyList = new List<string>();
            List<VirtualKey> modifierKeys = new List<VirtualKey>();
            VirtualKey? actionKey = null;

            foreach (var key in _keyPressOrder)
            {
                if (!_currentlyPressedKeys.Contains(key))
                {
                    continue;
                }

                if (IsModifierKey(key))
                {
                    if (!modifierKeys.Contains(key))
                    {
                        modifierKeys.Add(key);
                    }
                }
                else
                {
                    actionKey = key;
                }
            }

            foreach (var key in modifierKeys)
            {
                keyList.Add(GetKeyDisplayName((int)key));
            }

            if (actionKey.HasValue)
            {
                keyList.Add(GetKeyDisplayName((int)actionKey.Value));
            }

            return keyList;
        }

        private string GetKeyDisplayName(int keyCode)
        {
            var keyName = new System.Text.StringBuilder(64);
            KeyboardManagerInterop.GetKeyDisplayName(keyCode, keyName, keyName.Capacity);
            return keyName.ToString();
        }

        private bool IsModifierKey(VirtualKey key)
        {
            return key == VirtualKey.Control
                || key == VirtualKey.LeftControl
                || key == VirtualKey.RightControl
                || key == VirtualKey.Menu
                || key == VirtualKey.LeftMenu
                || key == VirtualKey.RightMenu
                || key == VirtualKey.Shift
                || key == VirtualKey.LeftShift
                || key == VirtualKey.RightShift
                || key == VirtualKey.LeftWindows
                || key == VirtualKey.RightWindows;
        }

        public void SetRemappedKeys(List<string> keys)
        {
            _remappedKeys.Clear();
            if (keys != null)
            {
                foreach (var key in keys)
                {
                    _remappedKeys.Add(key);
                }
            }

            UpdateAllAppsCheckBoxState();
        }

        public void SetOriginalKeys(List<string> keys)
        {
            _originalKeys.Clear();
            if (keys != null)
            {
                foreach (var key in keys)
                {
                    _originalKeys.Add(key);
                }
            }
        }

        public List<string> GetOriginalKeys()
        {
            return _originalKeys.ToList();
        }

        public List<string> GetRemappedKeys()
        {
            return _remappedKeys.ToList();
        }

        public bool GetIsAppSpecific()
        {
            return AllAppsCheckBox.IsChecked ?? false;
        }

        public string GetAppName()
        {
            return AppNameTextBox.Text ?? string.Empty;
        }

        private void RemappedToggleBtn_Checked(object sender, RoutedEventArgs e)
        {
            // Only set NewMode to true if RemappedToggleBtn is checked
            if (RemappedToggleBtn.IsChecked == true)
            {
                NewMode = true;

                // Make sure OriginalToggleBtn is unchecked
                if (OriginalToggleBtn.IsChecked == true)
                {
                    OriginalToggleBtn.IsChecked = false;
                }

                SetKeyboardHook();
            }
            else
            {
                CleanupKeyboardHook();
            }
        }

        private void OriginalToggleBtn_Checked(object sender, RoutedEventArgs e)
        {
            // Only set NewMode to false if OriginalToggleBtn is checked
            if (OriginalToggleBtn.IsChecked == true)
            {
                NewMode = false;

                // Make sure RemappedToggleBtn is unchecked
                if (RemappedToggleBtn.IsChecked == true)
                {
                    RemappedToggleBtn.IsChecked = false;
                }

                SetKeyboardHook();
            }
        }

        public void SetApp(bool isSpecificApp, string appName)
        {
            if (isSpecificApp)
            {
                AllAppsCheckBox.IsChecked = true;
                AppNameTextBox.Text = appName;
                AppNameTextBox.Visibility = Visibility.Visible;
            }
            else
            {
                AllAppsCheckBox.IsChecked = false;
                AppNameTextBox.Visibility = Visibility.Collapsed;
            }
        }

        private void AllAppsCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (RemappedToggleBtn != null && RemappedToggleBtn.IsChecked == true)
            {
                RemappedToggleBtn.IsChecked = false;
            }

            if (OriginalToggleBtn != null && OriginalToggleBtn.IsChecked == true)
            {
                OriginalToggleBtn.IsChecked = false;
            }

            CleanupKeyboardHook();

            AppNameTextBox.Visibility = Visibility.Visible;
        }

        private void AllAppsCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AppNameTextBox.Visibility = Visibility.Collapsed;
        }

        private void AppNameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Reset the focus state when the AppNameTextBox is focused
            if (RemappedToggleBtn != null && RemappedToggleBtn.IsChecked == true)
            {
                RemappedToggleBtn.IsChecked = false;
            }

            if (OriginalToggleBtn != null && OriginalToggleBtn.IsChecked == true)
            {
                OriginalToggleBtn.IsChecked = false;
            }

            CleanupKeyboardHook();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            AllAppsCheckBox.Checked += AllAppsCheckBox_Checked;
            AllAppsCheckBox.Unchecked += AllAppsCheckBox_Unchecked;

            AppNameTextBox.GotFocus += AppNameTextBox_GotFocus;
        }

        public void ResetToggleButtons()
        {
            // Reset toggle button status without clearing the key displays
            if (RemappedToggleBtn != null)
            {
                RemappedToggleBtn.IsChecked = false;
            }

            if (OriginalToggleBtn != null)
            {
                OriginalToggleBtn.IsChecked = false;
            }
        }

        public void SetUpToggleButtonInitialStatus()
        {
            // Ensure OriginalToggleBtn is checked
            if (OriginalToggleBtn != null && OriginalToggleBtn.IsChecked != true)
            {
                OriginalToggleBtn.IsChecked = true;
            }

            // Make sure RemappedToggleBtn is not checked
            if (RemappedToggleBtn != null && RemappedToggleBtn.IsChecked == true)
            {
                RemappedToggleBtn.IsChecked = false;
            }
        }

        public void UpdateAllAppsCheckBoxState()
        {
            // Only enable app-specific remapping for shortcuts (multiple keys)
            bool isShortcut = _originalKeys.Count > 1;

            AllAppsCheckBox.IsEnabled = isShortcut;

            // If it's not a shortcut, ensure the checkbox is unchecked and app textbox is hidden
            if (!isShortcut)
            {
                AllAppsCheckBox.IsChecked = false;
                AppNameTextBox.Visibility = Visibility.Collapsed;
            }
       }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    CleanupKeyboardHook();
                    Reset();
                }

                _disposed = true;
            }
        }

        public void Reset()
        {
            // Reset key status
            _currentlyPressedKeys.Clear();
            _keyPressOrder.Clear();

            // Reset displayed keys
            _originalKeys.Clear();
            _remappedKeys.Clear();

            // Reset toggle button status
            if (RemappedToggleBtn != null)
            {
                RemappedToggleBtn.IsChecked = false;
            }

            if (OriginalToggleBtn != null)
            {
                OriginalToggleBtn.IsChecked = false;
            }

            NewMode = false;

            // Reset app name text box
            if (AppNameTextBox != null)
            {
                AppNameTextBox.Text = string.Empty;
            }

            UpdateAllAppsCheckBoxState();

            // Reset the focus status
            if (this.FocusState != FocusState.Unfocused)
            {
                this.IsTabStop = false;
                this.IsTabStop = true;
            }
        }
    }
}
