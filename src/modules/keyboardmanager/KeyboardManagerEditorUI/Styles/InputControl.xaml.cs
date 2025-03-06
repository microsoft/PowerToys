// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace KeyboardManagerEditorUI.Styles
{
    public sealed partial class InputControl : UserControl
    {
        public InputControl()
        {
            this.InitializeComponent();
        }

        public void SetOriginalKeys(List<string> keys)
        {
            OriginalKeys.ItemsSource = keys;
        }

        public void SetRemappedKeys(List<string> keys)
        {
            RemappedKeys.ItemsSource = keys;
        }

        public List<string> GetOriginalKeys()
        {
            return OriginalKeys.ItemsSource as List<string> ?? new List<string>();
        }

        public List<string> GetRemappedKeys()
        {
            return RemappedKeys.ItemsSource as List<string> ?? new List<string>();
        }

        public bool GetIsAppSpecific()
        {
            return AllAppsCheckBox.IsChecked ?? false;
        }

        public string GetAppName()
        {
            return AppNameTextBox.Text ?? string.Empty;
        }

        private void OriginalToggleBtn_Checked(object sender, RoutedEventArgs e)
        {
            RemappedToggleBtn.IsChecked = false;
        }

        private void RemappedToggleBtn_Checked(object sender, RoutedEventArgs e)
        {
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
    }
}
