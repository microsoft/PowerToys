// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KeyboardManagerEditorUI.Styles
{
    public sealed partial class InputControl : UserControl
    {
        private List<string> pressedKeys = new List<string>();
        private List<string> newpressedKeys = new List<string>();

        // Define newMode as a DependencyProperty for binding
        public static readonly DependencyProperty NewModeProperty = DependencyProperty.Register(
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
            control?.UpdateKeyDisplay((bool)e.NewValue); // Update UI whenever NewMode changes
        }

        private void InputControl_KeyDown(object sender, KeyRoutedEventArgs e, bool newMode)
        {
            // Get the key name and add it to the list if it's not already there
            string keyName = e.Key.ToString();
            keyName = NormalizeKeyName(keyName);

            var currentKeyList = newMode ? newpressedKeys : pressedKeys;

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

        private void ButtonA_Click(object sender, RoutedEventArgs e)
        {
            // Toggle the newMode value and update the UI
            NewMode = !NewMode; // Toggle newMode to true/false
            if (NewMode)
            {
                // Clear the remapping keys if we switch to remapping mode
                newpressedKeys.Clear();
            }

            // Optionally, update the UI to indicate remapping mode is active
            UpdateKeyDisplay(NewMode);
        }

        private void InputControl_KeyUp(object sender, KeyRoutedEventArgs e, bool newMode)
        {
            string keyName = e.Key.ToString();
            var currentKeyList = newMode ? newpressedKeys : pressedKeys;

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
            KeyStackPanel.Children.Clear();
            NewKeyStackPanel.Children.Clear(); // Assuming keyPanel2 is your second stack panel

            var currentKeyList = newMode ? newpressedKeys : pressedKeys;

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
                    keyBlockContainer.Background = new SolidColorBrush(Microsoft.UI.Colors.LightBlue);
                    keyBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
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

        private void RemappedToggleBtn_Checked(object sender, RoutedEventArgs e)
        {
            RemappedToggleBtn.IsChecked = false;
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
