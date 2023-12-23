// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace FileActionsMenu
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            if (Wpf.Ui.Appearance.Theme.GetSystemTheme() == Wpf.Ui.Appearance.SystemThemeType.Light)
            {
                Wpf.Ui.Appearance.Theme.Apply(Wpf.Ui.Appearance.ThemeType.Light);
            }
            else
            {
                Wpf.Ui.Appearance.Theme.Apply(Wpf.Ui.Appearance.ThemeType.Dark);
            }

            Wpf.Ui.Appearance.Watcher.Watch(this, Wpf.Ui.Appearance.BackgroundType.None, true);
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
