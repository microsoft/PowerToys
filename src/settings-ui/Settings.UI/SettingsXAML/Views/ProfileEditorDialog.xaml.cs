// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerDisplay.Common.Models;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// Dialog for creating/editing PowerDisplay profiles
    /// </summary>
    public sealed partial class ProfileEditorDialog : ContentDialog
    {
        public ProfileEditorViewModel ViewModel { get; private set; }

        public PowerDisplayProfile? ResultProfile { get; private set; }

        public ProfileEditorDialog(ObservableCollection<MonitorInfo> availableMonitors, string defaultName = "")
        {
            this.InitializeComponent();
            ViewModel = new ProfileEditorViewModel(availableMonitors, defaultName);

            // Set localized strings for ContentDialog
            var resourceLoader = ResourceLoaderInstance.ResourceLoader;
            Title = resourceLoader.GetString("PowerDisplay_ProfileEditor_Title");
            PrimaryButtonText = resourceLoader.GetString("PowerDisplay_Dialog_Save");
            CloseButtonText = resourceLoader.GetString("PowerDisplay_Dialog_Cancel");
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (ViewModel.CanSave)
            {
                ResultProfile = ViewModel.CreateProfile();
            }
        }

        private void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ResultProfile = null;
        }

        /// <summary>
        /// Pre-fill the dialog with existing profile data
        /// </summary>
        public void PreFillProfile(PowerDisplayProfile profile)
        {
            if (profile == null || ViewModel == null)
            {
                return;
            }

            // Set profile name
            ViewModel.ProfileName = profile.Name;

            // Pre-fill monitor settings from existing profile
            foreach (var monitorSetting in profile.MonitorSettings)
            {
                var monitorItem = ViewModel.Monitors.FirstOrDefault(m => m.Monitor.Id == monitorSetting.MonitorId);
                if (monitorItem != null)
                {
                    monitorItem.IsSelected = true;

                    // Set brightness if included in profile
                    if (monitorSetting.Brightness.HasValue)
                    {
                        monitorItem.IncludeBrightness = true;
                        monitorItem.Brightness = monitorSetting.Brightness.Value;
                    }

                    // Set color temperature if included in profile
                    if (monitorSetting.ColorTemperatureVcp.HasValue)
                    {
                        monitorItem.IncludeColorTemperature = true;
                        monitorItem.ColorTemperature = monitorSetting.ColorTemperatureVcp.Value;
                    }

                    // Set contrast if included in profile
                    if (monitorSetting.Contrast.HasValue)
                    {
                        monitorItem.IncludeContrast = true;
                        monitorItem.Contrast = monitorSetting.Contrast.Value;
                    }

                    // Set volume if included in profile
                    if (monitorSetting.Volume.HasValue)
                    {
                        monitorItem.IncludeVolume = true;
                        monitorItem.Volume = monitorSetting.Volume.Value;
                    }
                }
            }
        }
    }
}
