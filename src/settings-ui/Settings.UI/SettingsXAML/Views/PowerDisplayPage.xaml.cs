// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Controls;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class PowerDisplayPage : NavigablePage, IRefreshablePage
    {
        private PowerDisplayViewModel ViewModel { get; set; }

        // Flag to prevent reentrant SelectionChanged handling during programmatic selection
        private bool _isRestoringSelection;

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
            // Skip if we're programmatically restoring a selection (prevents reentrant handling)
            if (_isRestoringSelection)
            {
                return;
            }

            // Skip if no new selection
            if (e.AddedItems.Count == 0)
            {
                return;
            }

            if (sender is not ComboBox comboBox || comboBox.Tag is not MonitorInfo monitor)
            {
                return;
            }

            // Get new selected item
            if (e.AddedItems[0] is not ColorPresetItem newItem)
            {
                return;
            }

            // Skip if selected value equals current property value (this is a restore operation or no change)
            if (newItem.VcpValue == monitor.ColorTemperatureVcp)
            {
                return;
            }

            // Get old value: from RemovedItems if available; otherwise from current property
            int oldValue = (e.RemovedItems.Count > 0 && e.RemovedItems[0] is ColorPresetItem oldItem)
                ? oldItem.VcpValue
                : monitor.ColorTemperatureVcp;

            // Show confirmation dialog
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = resourceLoader.GetString("PowerDisplay_ColorTemperature_WarningTitle"),
                Content = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = resourceLoader.GetString("PowerDisplay_ColorTemperature_WarningHeader"),
                            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
                            TextWrapping = TextWrapping.Wrap,
                        },
                        new TextBlock
                        {
                            Text = resourceLoader.GetString("PowerDisplay_ColorTemperature_WarningDescription"),
                            TextWrapping = TextWrapping.Wrap,
                        },
                        new TextBlock
                        {
                            Text = resourceLoader.GetString("PowerDisplay_ColorTemperature_WarningList"),
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(20, 0, 0, 0),
                        },
                        new TextBlock
                        {
                            Text = resourceLoader.GetString("PowerDisplay_ColorTemperature_WarningConfirm"),
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            TextWrapping = TextWrapping.Wrap,
                        },
                    },
                },
                PrimaryButtonText = resourceLoader.GetString("PowerDisplay_ColorTemperature_PrimaryButton"),
                CloseButtonText = resourceLoader.GetString("PowerDisplay_Dialog_Cancel"),
                DefaultButton = ContentDialogButton.Close,
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                // User confirmed: update property and apply to hardware
                monitor.ColorTemperatureVcp = newItem.VcpValue;
                ViewModel.ApplyColorTemperatureToMonitor(monitor.InternalName, newItem.VcpValue);
            }
            else
            {
                // User cancelled: revert ComboBox to previous selection (property unchanged)
                // Use flag to prevent reentrant event handling
                _isRestoringSelection = true;
                try
                {
                    comboBox.SelectedValue = oldValue;
                }
                finally
                {
                    _isRestoringSelection = false;
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
                var resourceLoader = ResourceLoaderInstance.ResourceLoader;
                var dialog = new ContentDialog
                {
                    XamlRoot = this.XamlRoot,
                    Title = resourceLoader.GetString("PowerDisplay_DeleteProfile_Title"),
                    Content = string.Format(System.Globalization.CultureInfo.CurrentCulture, resourceLoader.GetString("PowerDisplay_DeleteProfile_Content"), profile.Name),
                    PrimaryButtonText = resourceLoader.GetString("PowerDisplay_DeleteProfile_PrimaryButton"),
                    CloseButtonText = resourceLoader.GetString("PowerDisplay_Dialog_Cancel"),
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
            // Use shared ProfileHelper for consistent profile name generation
            var existingNames = ViewModel.Profiles.Select(p => p.Name).ToHashSet();
            return ProfileHelper.GenerateUniqueProfileName(existingNames);
        }
    }
}
