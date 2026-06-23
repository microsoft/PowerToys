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
using PowerDisplay.Models;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class PowerDisplayPage : NavigablePage, IRefreshablePage
    {
        private PowerDisplayViewModel ViewModel { get; set; }

        public PowerDisplayPage()
        {
            var settingsUtils = SettingsUtils.Default;
            ViewModel = new PowerDisplayViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                SettingsRepository<PowerDisplaySettings>.GetInstance(settingsUtils),
                ShellPage.SendDefaultIPCMessage);
            ViewModel.ConfirmDangerousFeatureAsync = ShowDangerousFeatureDialogAsync;
            DataContext = ViewModel;
            InitializeComponent();
            Loaded += (s, e) => ViewModel.OnPageLoaded();
        }

        private async Task<bool> ShowDangerousFeatureDialogAsync(PowerDisplayWarningKind kind)
        {
            var dialog = new PowerDisplayWarningDialog(kind) { XamlRoot = XamlRoot };
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
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

        private void CopyMonitorDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is MonitorInfo monitor)
            {
                var diagnosticsText = monitor.GetDiagnosticsAsText();

                var dataPackage = new DataPackage();
                dataPackage.SetText(diagnosticsText);
                Clipboard.SetContent(dataPackage);
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
            var existingNames = ViewModel.Profiles.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            var baseName = resourceLoader.GetString("PowerDisplay_Profile_DefaultBaseName");
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = "Profile";
            }

            if (!existingNames.Contains(baseName))
            {
                return baseName;
            }

            for (int i = 2; i < 1000; i++)
            {
                var candidate = $"{baseName} {i}";
                if (!existingNames.Contains(candidate))
                {
                    return candidate;
                }
            }

            return $"{baseName} {DateTime.Now.Ticks}";
        }

        // Custom VCP Mapping event handlers
        private async void AddCustomMapping_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CustomVcpMappingEditorDialog(ViewModel.Monitors);
            dialog.XamlRoot = this.XamlRoot;

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && dialog.ResultMapping != null)
            {
                ViewModel.AddCustomVcpMapping(dialog.ResultMapping);
            }
        }

        private async void EditCustomMapping_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not CustomVcpValueMapping mapping)
            {
                return;
            }

            var dialog = new CustomVcpMappingEditorDialog(ViewModel.Monitors);
            dialog.XamlRoot = this.XamlRoot;
            dialog.PreFillMapping(mapping);

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && dialog.ResultMapping != null)
            {
                ViewModel.UpdateCustomVcpMapping(mapping, dialog.ResultMapping);
            }
        }

        private async void DeleteCustomMapping_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not CustomVcpValueMapping mapping)
            {
                return;
            }

            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = resourceLoader.GetString("PowerDisplay_CustomMapping_Delete_Title"),
                Content = resourceLoader.GetString("PowerDisplay_CustomMapping_Delete_Message"),
                PrimaryButtonText = resourceLoader.GetString("Yes"),
                CloseButtonText = resourceLoader.GetString("No"),
                DefaultButton = ContentDialogButton.Close,
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                ViewModel.DeleteCustomVcpMapping(mapping);
            }
        }

        private async void EnableColorTemperature_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not MonitorInfo monitor)
            {
                return;
            }

            await TryCommitDangerousChangeAsync(
                cb,
                cb.IsChecked == true,
                monitor.EnableColorTemperature,
                v => monitor.EnableColorTemperature = v,
                PowerDisplayWarningKind.ColorTemperature);
        }

        private async void EnablePowerState_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not MonitorInfo monitor)
            {
                return;
            }

            await TryCommitDangerousChangeAsync(
                cb,
                cb.IsChecked == true,
                monitor.EnablePowerState,
                v => monitor.EnablePowerState = v,
                PowerDisplayWarningKind.PowerState);
        }

        private async void EnableInputSource_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not MonitorInfo monitor)
            {
                return;
            }

            await TryCommitDangerousChangeAsync(
                cb,
                cb.IsChecked == true,
                monitor.EnableInputSource,
                v => monitor.EnableInputSource = v,
                PowerDisplayWarningKind.InputSource);
        }

        // Per-monitor CheckBoxes use OneWay binding + Click (Click only fires for real user
        // input, so the binding-driven event problem the ToggleSwitch had does not apply).
        // The "no gesture" check still appears here because Click fires for keyboard space-
        // bar even when the IsChecked didn't move, and to keep the cancel-revert path safe.
        private async Task<bool> TryCommitDangerousChangeAsync(
            CheckBox control,
            bool desiredValue,
            bool currentValue,
            Action<bool> commit,
            PowerDisplayWarningKind kind)
        {
            if (desiredValue == currentValue)
            {
                return false;
            }

            if (!desiredValue)
            {
                commit(false);
                return true;
            }

            if (await ShowDangerousFeatureDialogAsync(kind))
            {
                commit(true);
                return true;
            }

            control.IsChecked = currentValue;
            return false;
        }
    }
}
