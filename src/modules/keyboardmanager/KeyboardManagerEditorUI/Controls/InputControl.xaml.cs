// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Interop;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace KeyboardManagerEditorUI.Controls
{
    public sealed partial class InputControl : UserControl, IDisposable, IKeyboardHookTarget
    {
        // Collection to store original and remapped keys
        private ObservableCollection<string> _originalKeys = new ObservableCollection<string>();
        private ObservableCollection<string> _remappedKeys = new ObservableCollection<string>();

        // TeachingTip for notifications
        private TeachingTip? currentNotification;
        private DispatcherTimer? notificationTimer;

        private bool _disposed;

        public static readonly DependencyProperty InputModeProperty =
            DependencyProperty.Register(
                "InputMode",
                typeof(KeyInputMode),
                typeof(InputControl),
                new PropertyMetadata(KeyInputMode.OriginalKeys));

        public KeyInputMode InputMode
        {
            get { return (KeyInputMode)GetValue(InputModeProperty); }
            set { SetValue(InputModeProperty, value); }
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

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            AllAppsCheckBox.Checked += AllAppsCheckBox_Checked;
            AllAppsCheckBox.Unchecked += AllAppsCheckBox_Unchecked;

            AppNameTextBox.GotFocus += AppNameTextBox_GotFocus;
        }

        private void InputControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Reset the control when it is unloaded
            Reset();
        }

        public void OnKeyDown(VirtualKey key, List<string> formattedKeys)
        {
            if (InputMode == KeyInputMode.RemappedKeys)
            {
                _remappedKeys.Clear();
                foreach (var keyName in formattedKeys)
                {
                    _remappedKeys.Add(keyName);
                }
            }
            else
            {
                _originalKeys.Clear();
                foreach (var keyName in formattedKeys)
                {
                    _originalKeys.Add(keyName);
                }
            }

            UpdateAllAppsCheckBoxState();
        }

        public void ClearKeys()
        {
            if (InputMode == KeyInputMode.RemappedKeys)
            {
                _remappedKeys.Clear();
            }
            else
            {
                _originalKeys.Clear();
            }
        }

        public void OnInputLimitReached()
        {
            ShowNotificationTip("Shortcuts can only have up to 4 modifier keys");
        }

        public void CleanupKeyboardHook()
        {
            KeyboardHookHelper.Instance.CleanupHook();
        }

        private void RemappedToggleBtn_Checked(object sender, RoutedEventArgs e)
        {
            // Only set NewMode to true if RemappedToggleBtn is checked
            if (RemappedToggleBtn.IsChecked == true)
            {
                InputMode = KeyInputMode.RemappedKeys;

                // Make sure OriginalToggleBtn is unchecked
                if (OriginalToggleBtn.IsChecked == true)
                {
                    OriginalToggleBtn.IsChecked = false;
                }

                KeyboardHookHelper.Instance.ActivateHook(this);
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
                InputMode = KeyInputMode.OriginalKeys;

                // Make sure RemappedToggleBtn is unchecked
                if (RemappedToggleBtn.IsChecked == true)
                {
                    RemappedToggleBtn.IsChecked = false;
                }

                KeyboardHookHelper.Instance.ActivateHook(this);
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

        public void ShowNotificationTip(string message)
        {
            // If there's already an active notification, close and remove it first
            CloseExistingNotification();

            // Create a new notification
            currentNotification = new TeachingTip
            {
                Title = "Input Limit Reached",
                Subtitle = message,
                IsLightDismissEnabled = true,
                PreferredPlacement = TeachingTipPlacementMode.Top,
                XamlRoot = this.XamlRoot,
                IconSource = new SymbolIconSource { Symbol = Symbol.Important },
            };

            // Target the toggle button that triggered the notification
            currentNotification.Target = InputMode == KeyInputMode.RemappedKeys ? RemappedToggleBtn : OriginalToggleBtn;

            // Add the notification to the root panel and show it
            if (this.Content is Panel rootPanel)
            {
                rootPanel.Children.Add(currentNotification);
                currentNotification.IsOpen = true;

                // Create a timer to auto-dismiss the notification
                notificationTimer = new DispatcherTimer();
                notificationTimer.Interval = TimeSpan.FromMilliseconds(EditorConstants.DefaultNotificationTimeout);
                notificationTimer.Tick += (s, e) =>
                {
                    CloseExistingNotification();
                    notificationTimer = null;
                };
                notificationTimer.Start();
            }
        }

        // Helper method to close existing notifications
        private void CloseExistingNotification()
        {
            // Stop any running timer
            if (notificationTimer != null)
            {
                notificationTimer.Stop();
                notificationTimer = null;
            }

            // Close and remove any existing notification
            if (currentNotification != null && currentNotification.IsOpen)
            {
                currentNotification.IsOpen = false;

                if (this.Content is Panel rootPanel)
                {
                    rootPanel.Children.Remove(currentNotification);
                }

                currentNotification = null;
            }
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

        public void Reset()
        {
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

            InputMode = KeyInputMode.OriginalKeys;

            // Reset app name text box
            if (AppNameTextBox != null)
            {
                AppNameTextBox.Text = string.Empty;
            }

            UpdateAllAppsCheckBoxState();

            // Close any existing notifications
            CloseExistingNotification();

            // Reset the focus status
            if (this.FocusState != FocusState.Unfocused)
            {
                this.IsTabStop = false;
                this.IsTabStop = true;
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
                    CloseExistingNotification();
                    Reset();
                }

                _disposed = true;
            }
        }
    }
}
