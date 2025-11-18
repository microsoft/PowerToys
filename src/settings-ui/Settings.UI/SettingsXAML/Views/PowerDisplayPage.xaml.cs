// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Controls;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class PowerDisplayPage : NavigablePage, IRefreshablePage
    {
        private PowerDisplayViewModel ViewModel { get; set; }

        // Track previous color temperature values to restore on cancel
        private Dictionary<string, int> _previousColorTemperatureValues = new Dictionary<string, int>();

        // Flag to prevent recursive SelectionChanged events
        private bool _isUpdatingColorTemperature;

        public PowerDisplayPage()
        {
            var settingsUtils = new SettingsUtils();
            ViewModel = new PowerDisplayViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                SettingsRepository<PowerDisplaySettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage);
            DataContext = ViewModel;
            InitializeComponent();
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        private void CopyVcpCodes_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is MonitorInfo monitor)
            {
                var vcpText = monitor.GetVcpCodesAsText();
                var dataPackage = new DataPackage();
                dataPackage.SetText(vcpText);
                Clipboard.SetContent(dataPackage);
            }
        }

        private async void ColorTemperatureComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.Tag is MonitorInfo monitor)
            {
                // Skip if we are programmatically updating the value
                if (_isUpdatingColorTemperature)
                {
                    return;
                }

                // Skip if this is the initial load (no removed items means programmatic selection)
                if (e.RemovedItems.Count == 0)
                {
                    // Store the initial value
                    if (!_previousColorTemperatureValues.ContainsKey(monitor.HardwareId))
                    {
                        _previousColorTemperatureValues[monitor.HardwareId] = monitor.ColorTemperature;
                    }

                    return;
                }

                // Get the new selected value
                var newValue = comboBox.SelectedValue as int?;
                if (!newValue.HasValue)
                {
                    return;
                }

                // Get the previous value
                int previousValue;
                if (!_previousColorTemperatureValues.TryGetValue(monitor.HardwareId, out previousValue))
                {
                    previousValue = monitor.ColorTemperature;
                }

                // Show confirmation dialog
                var dialog = new ContentDialog
                {
                    XamlRoot = this.XamlRoot,
                    Title = "Confirm Color Temperature Change",
                    Content = new StackPanel
                    {
                        Spacing = 12,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "⚠️ Warning: This is a potentially dangerous operation!",
                                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
                                TextWrapping = TextWrapping.Wrap,
                            },
                            new TextBlock
                            {
                                Text = "Changing the color temperature setting may cause unpredictable results including:",
                                TextWrapping = TextWrapping.Wrap,
                            },
                            new TextBlock
                            {
                                Text = "• Incorrect display colors\n• Display malfunction\n• Settings that cannot be reverted",
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(20, 0, 0, 0),
                            },
                            new TextBlock
                            {
                                Text = "Are you sure you want to proceed with this change?",
                                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                                TextWrapping = TextWrapping.Wrap,
                            },
                        },
                    },
                    PrimaryButtonText = "Yes, Change Setting",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    // User confirmed, apply the change
                    // Setting the property will trigger save to settings file via OnPropertyChanged
                    monitor.ColorTemperature = newValue.Value;
                    _previousColorTemperatureValues[monitor.HardwareId] = newValue.Value;

                    // Trigger custom action to apply color temperature to hardware
                    // This is separate from the settings save to avoid unwanted hardware updates
                    // when other settings (like RestoreSettingsOnStartup) change
                    ViewModel.TriggerApplyColorTemperature();
                }
                else
                {
                    // User cancelled, revert to previous value
                    // Set flag to prevent recursive event
                    _isUpdatingColorTemperature = true;
                    comboBox.SelectedValue = previousValue;
                    _isUpdatingColorTemperature = false;
                }
            }
        }
    }
}
