// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace FileActionsMenu
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this, WindowBackdropType.None, true);
            InitializeComponent();
            ContextMenu cm = (ContextMenu)FindResource("Menu");
            cm.IsOpen = true;
            cm.Closed += (sender, args) => Close();
        }

        private void GenerateHash(object sender, RoutedEventArgs e)
        {
        }

        private void GenerateHashes(object sender, RoutedEventArgs e)
        {
        }
    }
}
