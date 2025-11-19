// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
                var monitorItem = ViewModel.Monitors.FirstOrDefault(m => m.Monitor.HardwareId == monitorSetting.HardwareId);
                if (monitorItem != null)
                {
                    monitorItem.IsSelected = true;
                    monitorItem.Brightness = monitorSetting.Brightness;
                    monitorItem.ColorTemperature = monitorSetting.ColorTemperature;

                    if (monitorSetting.Contrast.HasValue)
                    {
                        monitorItem.Contrast = monitorSetting.Contrast.Value;
                    }

                    if (monitorSetting.Volume.HasValue)
                    {
                        monitorItem.Volume = monitorSetting.Volume.Value;
                    }
                }
            }
        }
    }
}
