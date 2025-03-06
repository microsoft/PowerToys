// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace KeyboardManagerEditorUI.Styles
{
    public sealed partial class InputControl : UserControl
    {
        private List<string> pressedKeys = new List<string>();
        private List<string> newPressedKeys = new List<string>();

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
            this.KeyDown += (sender, e) => InputControl_KeyDown(sender, e, NewMode);
            this.KeyUp += (sender, e) => InputControl_KeyUp(sender, e, NewMode);
        }

        private static void OnNewModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as InputControl;
            if (control != null)
            {
                bool newMode = (bool)e.NewValue;
                control.UpdateKeyDisplay(newMode);
            }
        }

        private void InputControl_KeyDown(object sender, KeyRoutedEventArgs e, bool newMode)
        {
            // Get the key name and add it to the list if it's not already there
            string keyName = e.Key.ToString();
            keyName = NormalizeKeyName(keyName);

            var currentKeyList = newMode ? newPressedKeys : pressedKeys;

            if (!currentKeyList.Contains(keyName))
            {
                var allowedModifiers = new[] { "Shift", "Ctrl", "LWin", "RWin", "Alt" };
                if (currentKeyList.All(k => allowedModifiers.Contains(k)) && pressedKeys.Count < 4)
                {
                    currentKeyList.Add(keyName);
                    UpdateKeyDisplay(newMode);
                }
            }
        }

        private void InputControl_KeyUp(object sender, KeyRoutedEventArgs e, bool newMode)
        {
            // Console.WriteLine(newMode);
            string keyName = e.Key.ToString();
            var currentKeyList = newMode ? newPressedKeys : pressedKeys;

            if (!currentKeyList.Contains(keyName))
            {
                return;
            }

            // Remove the key name from the list when the key is released
            // currentKeyList.Remove(keyName);
            UpdateKeyDisplay(newMode);
        }

        private void UpdateKeyDisplay(bool newMode)
        {
            // Clear current UI elements
            if (newMode)
            {
                NewKeyStackPanel.Children.Clear();
            } // Assuming keyPanel2 is your second stack panel
            else
            {
                KeyStackPanel.Children.Clear();
            }

            var currentKeyList = newMode ? newPressedKeys : pressedKeys;

            // Add each pressed key as a TextBlock in the StackPanel
            foreach (var key in currentKeyList)
            {
                Border keyBlockContainer = new Border
                {
                    Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                    Padding = new Thickness(10),
                    Margin = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.DarkBlue),
                    BorderThickness = new Thickness(1),
                };

                TextBlock keyBlock = new TextBlock
                {
                    Text = key,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                };

                // Add TextBlock inside the Border container
                keyBlockContainer.Child = keyBlock;

                // Add Border to StackPanel
                if (newMode)
                {
                    keyBlockContainer.Background = Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as SolidColorBrush;
                    keyBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black);
                    NewKeyStackPanel.Children.Add(keyBlockContainer); // For remapping keys
                }
                else
                {
                    KeyStackPanel.Children.Add(keyBlockContainer); // For normal keys
                }
            }
        }

        public void SetRemappedKeys(List<string> keys)
        {
            RemappedKeys.ItemsSource = keys;
        }

        public void SetOriginalKeys(List<string> keys)
        {
            OriginalKeys.ItemsSource = keys;
        }

        public List<string> GetOriginalKeys()
        {
            return pressedKeys as List<string> ?? new List<string>();
        }

        public List<string> GetRemappedKeys()
        {
            return newPressedKeys as List<string> ?? new List<string>();
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
            NewMode = true;
            RemappedToggleBtn.IsChecked = false;
        }

        private void OriginalToggleBtn_Checked(object sender, RoutedEventArgs e)
        {
            NewMode = false;
            OriginalToggleBtn.IsChecked = false;
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
            AppNameTextBox.Visibility = Visibility.Visible;
        }

        private void AllAppsCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AppNameTextBox.Visibility = Visibility.Collapsed;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            AllAppsCheckBox.Checked += AllAppsCheckBox_Checked;
            AllAppsCheckBox.Unchecked += AllAppsCheckBox_Unchecked;
        }

        private string NormalizeKeyName(string keyName)
        {
            switch (keyName)
            {
                case "Control":
                    return "Ctrl";
                case "Menu":
                    return "Alt";
                case "LeftWindows":
                    return "LWin";
                case "RightWindows":
                    return "RWin";
                default:
                    return keyName; // 默认返回原值
            }
        }
    }
}
