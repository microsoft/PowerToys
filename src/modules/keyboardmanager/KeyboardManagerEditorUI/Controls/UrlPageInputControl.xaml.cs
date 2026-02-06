// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using KeyboardManagerEditorUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;
using static KeyboardManagerEditorUI.Interop.ShortcutKeyMapping;

namespace KeyboardManagerEditorUI.Controls
{
    public sealed partial class UrlPageInputControl : UserControl, IKeyboardHookTarget
    {
        private ObservableCollection<string> _shortcutKeys = new ObservableCollection<string>();
        private TeachingTip? currentNotification;
        private DispatcherTimer? notificationTimer;

        // private bool _internalUpdate;
        public UrlPageInputControl()
        {
            this.InitializeComponent();
            this.ShortcutKeys.ItemsSource = _shortcutKeys;

            ShortcutToggleBtn.IsChecked = true;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            KeyboardHookHelper.Instance.ActivateHook(this);
            UrlPathInput.GotFocus += UrlInputBox_GotFocus;
        }

        private void ShortcutToggleBtn_Checked(object sender, RoutedEventArgs e)
        {
            if (ShortcutToggleBtn.IsChecked == true)
            {
                KeyboardHookHelper.Instance.ActivateHook(this);
            }
            else
            {
                KeyboardHookHelper.Instance.CleanupHook();
            }
        }

        public void OnKeyDown(VirtualKey key, List<string> formattedKeys)
        {
            _shortcutKeys.Clear();
            foreach (var keyName in formattedKeys)
            {
                _shortcutKeys.Add(keyName);
            }
        }

        private void UrlInputBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Clean up the keyboard hook when the text box gains focus
            KeyboardHookHelper.Instance.CleanupHook();

            if (ShortcutToggleBtn != null && ShortcutToggleBtn.IsChecked == true)
            {
                ShortcutToggleBtn.IsChecked = false;
            }
        }

        public void OnInputLimitReached()
        {
            ShowNotificationTip("Shortcuts can only have up to 4 modifier keys");
        }

        public void ShowNotificationTip(string message)
        {
            CloseExistingNotification();

            currentNotification = new TeachingTip
            {
                Title = "Input Limit",
                Subtitle = message,
                IsLightDismissEnabled = true,
                PreferredPlacement = TeachingTipPlacementMode.Top,
                XamlRoot = this.XamlRoot,
                IconSource = new SymbolIconSource { Symbol = Symbol.Important },
                Target = ShortcutToggleBtn,
            };

            if (this.Content is Panel rootPanel)
            {
                rootPanel.Children.Add(currentNotification);
                currentNotification.IsOpen = true;

                notificationTimer = new DispatcherTimer();
                notificationTimer.Interval = TimeSpan.FromMilliseconds(EditorConstants.DefaultNotificationTimeout);
                notificationTimer.Tick += (s, e) =>
                {
                    CloseExistingNotification();
                };
                notificationTimer.Start();
            }
        }

        private void CloseExistingNotification()
        {
            if (notificationTimer != null)
            {
                notificationTimer.Stop();
                notificationTimer = null;
            }

            if (currentNotification != null && currentNotification.IsOpen)
            {
                currentNotification.IsOpen = false;

                if (this.Content is Panel rootPanel && rootPanel.Children.Contains(currentNotification))
                {
                    rootPanel.Children.Remove(currentNotification);
                }

                currentNotification = null;
            }
        }

        public void ClearKeys()
        {
            _shortcutKeys.Clear();
        }

        public List<string> GetShortcutKeys()
        {
            List<string> keys = new List<string>();

            foreach (var key in _shortcutKeys)
            {
                keys.Add(key);
            }

            return keys;
        }

        public string GetUrlPathContent()
        {
            return UrlPathInput.Text;
        }

        public void SetShortcutKeys(List<string> keys)
        {
            if (keys != null)
            {
                _shortcutKeys.Clear();
                foreach (var key in keys)
                {
                    _shortcutKeys.Add(key);
                }
            }
        }

        public void SetUrlPathContent(string text)
        {
            UrlPathInput.Text = text;
        }
    }
}
