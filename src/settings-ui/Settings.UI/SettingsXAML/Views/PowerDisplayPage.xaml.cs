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
            DataContext = ViewModel;
            InitializeComponent();
            Loaded += (s, e) => ViewModel.OnPageLoaded();
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

        // Flag to prevent reentrant Click/Toggled handling while we programmatically restore
        // a control after the user cancels a dangerous-feature confirmation dialog.
        private bool _isRestoringDangerousFeatureControl;

        private async void EnableColorTemperature_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not MonitorInfo monitor)
            {
                return;
            }

            await HandleDangerousFeatureClickAsync(
                sender,
                "PowerDisplay_ColorTemperature",
                value => monitor.EnableColorTemperature = value);
        }

        private async void EnablePowerState_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not MonitorInfo monitor)
            {
                return;
            }

            await HandleDangerousFeatureClickAsync(
                sender,
                "PowerDisplay_PowerState",
                value => monitor.EnablePowerState = value);
        }

        private async void EnableInputSource_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not MonitorInfo monitor)
            {
                return;
            }

            await HandleDangerousFeatureClickAsync(
                sender,
                "PowerDisplay_InputSource",
                value => monitor.EnableInputSource = value);
        }

        private async void MaxCompatibilityMode_Toggled(object sender, RoutedEventArgs e)
        {
            // Guard against re-entry from the cancel path's programmatic IsOn revert,
            // which fires Toggled a second time. See HandleDangerousFeatureClickAsync.
            if (_isRestoringDangerousFeatureControl)
            {
                return;
            }

            bool before = ViewModel.MaxCompatibilityMode;

            await HandleDangerousFeatureClickAsync(
                sender,
                "PowerDisplay_MaxCompatibility",
                value => ViewModel.MaxCompatibilityMode = value);

            if (ViewModel.MaxCompatibilityMode != before)
            {
                ViewModel.SignalRescanRequest();
            }
        }

        // The bound CheckBoxes/ToggleSwitch use Mode=OneWay, so the control's new state is NOT
        // pushed to the ViewModel/model automatically. This handler is the sole commit path:
        // confirmed enable -> setter(true); any disable -> setter(false); cancelled enable ->
        // revert the control's UI (the model was never touched).
        private async Task HandleDangerousFeatureClickAsync(
            object sender,
            string resourceKeyPrefix,
            Action<bool> setter)
        {
            if (_isRestoringDangerousFeatureControl)
            {
                return;
            }

            bool newValue;
            Action revertUi;
            switch (sender)
            {
                case CheckBox cb:
                    newValue = cb.IsChecked == true;
                    revertUi = () => cb.IsChecked = !newValue;
                    break;
                case ToggleSwitch ts:
                    newValue = ts.IsOn;
                    revertUi = () => ts.IsOn = !newValue;
                    break;
                default:
                    return;
            }

            // Disabling a dangerous feature is always safe and needs no confirmation.
            if (!newValue)
            {
                setter(false);
                return;
            }

            var dialog = new DangerousFeatureWarningDialog(resourceKeyPrefix)
            {
                XamlRoot = this.XamlRoot,
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                setter(true);
            }
            else
            {
                _isRestoringDangerousFeatureControl = true;
                try
                {
                    revertUi();
                }
                finally
                {
                    _isRestoringDangerousFeatureControl = false;
                }
            }
        }
    }
}
