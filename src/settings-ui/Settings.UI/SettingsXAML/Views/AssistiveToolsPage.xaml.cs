// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class AssistiveToolsPage : NavigablePage, IRefreshablePage
    {
        private NewPlusViewModel ViewModel { get; set; }

        public AssistiveToolsPage()
        {
            InitializeComponent();
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        private void ControlModeSelectionBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ControlModeSelectionBox.SelectedIndex == 0)
            {
                DelayCard.Visibility = Visibility.Visible;
                ModeComboBox.IsEnabled = true;
                ReducedLineSpeedCard.Visibility = Visibility.Collapsed;
                InitialSpeedCard.Visibility = Visibility.Collapsed;
            }
            else
            {
                DelayCard.Visibility = Visibility.Collapsed;
                ModeComboBox.IsEnabled = false;
                ReducedLineSpeedCard.Visibility = Visibility.Visible;
                InitialSpeedCard.Visibility = Visibility.Visible;
            }
        }

        private void ControlModeSelectionBox_Loaded(object sender, RoutedEventArgs e)
        {
            ControlModeSelectionBox.SelectionChanged += ControlModeSelectionBox_SelectionChanged;
        }

        private void ModeComboBox_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ModeComboBox.SelectionChanged += ModeComboBox_SelectionChanged;
        }

        private void ModeComboBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (ModeComboBox.SelectedIndex == 0)
            {
                DelayCard.IsEnabled = true;
            }
            else
            {
                DelayCard.IsEnabled = false;
            }
        }
    }
}
