// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using PowerAccent.UI.SettingsPage;

namespace PowerAccent.UI
{
    /// <summary>
    /// Logique d'interaction pour Settings.xaml
    /// </summary>
    public partial class Settings : Window
    {
        public Settings()
        {
            InitializeComponent();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Position.IsChecked = true;
        }

        private void Position_Checked(object sender, RoutedEventArgs e)
        {
            Options.IsChecked = false;
            Sort.IsChecked = false;
            this.ParentFrame.Navigate(new PositionPage());
        }

        private void Options_Checked(object sender, RoutedEventArgs e)
        {
            Position.IsChecked = false;
            Sort.IsChecked = false;
            this.ParentFrame.Navigate(new OptionsPage());
        }

        private void Sort_Checked(object sender, RoutedEventArgs e)
        {
            Options.IsChecked = false;
            Position.IsChecked = false;
            this.ParentFrame.Navigate(new SortPage());
        }
    }
}
