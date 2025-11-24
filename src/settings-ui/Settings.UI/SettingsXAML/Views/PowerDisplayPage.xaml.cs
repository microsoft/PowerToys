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
using PowerDisplay.Common.Models;
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

                    // Send IPC message to PowerDisplay with monitor ID and new color temperature value
                    // PowerDisplay will apply it directly to the specified monitor only
                    ViewModel.ApplyColorTemperatureToMonitor(monitor.InternalName, newValue.Value);
                }
                else
                {
                    // User cancelled, revert to previous value
                    // Set flag to prevent recursive event, using try-finally for safety
                    _isUpdatingColorTemperature = true;
                    try
                    {
                        comboBox.SelectedValue = previousValue;
                    }
                    finally
                    {
                        _isUpdatingColorTemperature = false;
                    }
                }
            }
        }

        // Profile button event handlers
        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PowerDisplayProfile profile)
            {
                ViewModel.ApplyProfile(profile);
            }
        }

        private async void AddProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Monitors == null || ViewModel.Monitors.Count == 0)
            {
                return;
            }

            var defaultName = GenerateDefaultProfileName();
            var dialog = new ProfileEditorDialog(ViewModel.Monitors, defaultName);
            dialog.XamlRoot = this.XamlRoot;

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && dialog.ResultProfile != null)
            {
                ViewModel.CreateProfile(dialog.ResultProfile);
            }
        }

        private async void EditProfile_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuFlyoutItem;
            if (menuItem?.Tag is PowerDisplayProfile profile)
            {
                var dialog = new ProfileEditorDialog(ViewModel.Monitors, profile.Name);
                dialog.XamlRoot = this.XamlRoot;

                // Pre-fill with existing profile settings
                dialog.PreFillProfile(profile);

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary && dialog.ResultProfile != null)
                {
                    ViewModel.UpdateProfile(profile.Name, dialog.ResultProfile);
                }
            }
        }

        private async void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuFlyoutItem;
            if (menuItem?.Tag is PowerDisplayProfile profile)
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = this.XamlRoot,
                    Title = "Delete Profile",
                    Content = $"Are you sure you want to delete '{profile.Name}'?",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    ViewModel.DeleteProfile(profile.Name);
                }
            }
        }

        private string GenerateDefaultProfileName()
        {
            var existingNames = new HashSet<string>();
            foreach (var profile in ViewModel.Profiles)
            {
                existingNames.Add(profile.Name);
            }

            int counter = 1;
            string name;
            do
            {
                name = $"Profile {counter}";
                counter++;
            }
            while (existingNames.Contains(name));

            return name;
        }
    }
}
