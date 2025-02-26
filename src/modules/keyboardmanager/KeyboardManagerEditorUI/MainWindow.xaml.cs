// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

namespace KeyboardManagerEditorUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        [DllImport("KeyboardManagerEditorLibraryWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool CheckIfRemappingsAreValid();

        public ObservableCollection<KeyMapping> KeyMappings { get; } = new();

        public MainWindow()
        {
            this.InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null)
            {
                // button.Background = (SolidColorBrush)Application.Current.Resources["SystemControlBackgroundAccentBrush"];
                return;
            }
        }

        private async void OnSelectSourceClick(object sender, RoutedEventArgs e)
        {
            var dialog = new KeyCaptureDialog();
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                if (sender is Button btn && btn.Tag is KeyMapping mapping)
                {
                    mapping.SourceKey = dialog.SelectedKeys;
                }
            }
        }

        private async void OnSelectTargetClick(object sender, RoutedEventArgs e)
        {
            var dialog = new KeyCaptureDialog();
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                if (sender is Button btn && btn.Tag is KeyMapping mapping)
                {
                    mapping.TargetKeys = dialog.SelectedKeys;
                }
            }
        }

        private void AddKeyMapping(object sender, RoutedEventArgs e)
        {
            KeyMappings.Add(new KeyMapping
            {
                SourceKey = "Select",
                TargetKeys = "To send",
            });
        }

        private void OnDeleteMapping(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is KeyMapping mapping)
            {
                KeyMappings.Remove(mapping);
            }
        }
    }
}
