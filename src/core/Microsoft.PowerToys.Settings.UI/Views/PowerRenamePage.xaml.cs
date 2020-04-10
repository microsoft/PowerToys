// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class PowerRenamePage : Page
    {
        public PowerRenameViewModel ViewModel { get; } = new PowerRenameViewModel();

        private const string POWERTOYNAME = "PowerRename";

        public PowerRenamePage()
        {
            InitializeComponent();
        }

        /// <inheritdoc/>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            PowerRenameSettings settings;
            try
            {
                settings = SettingsUtils.GetSettings<PowerRenameSettings>(POWERTOYNAME);
                UpdateView(settings);
            }
            catch
            {
                settings = new PowerRenameSettings(POWERTOYNAME);
                SettingsUtils.SaveSettings(settings.ToJsonString(), POWERTOYNAME);
                UpdateView(settings);
            }
        }

        private void UpdateView(PowerRenameSettings settings)
        {
            Toggle_PowerRename_Enable.IsOn = settings.properties.MruEnabled.value;
            Toggle_PowerRename_EnableOnExtendedContextMenu.IsOn = settings.properties.ShowExtendedMenu.value;
            Toggle_PowerRename_MaxDispListNum.Value = settings.properties.MaxMruSize.value;
            Toggle_PowerRename_EnableOnContextMenu.IsOn = settings.properties.ShowIconInMenu.value;
            Toggle_PowerRename_RestoreFlagsOnLaunch.IsOn = settings.properties.PersistInput.value;
        }

        private void Toggle_PowerRename_Enable_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch swt = sender as ToggleSwitch;

            if (swt != null)
            {
                PowerRenameSettings settings = SettingsUtils.GetSettings<PowerRenameSettings>(POWERTOYNAME);
                settings.properties.MruEnabled.value = swt.IsOn;

                if (ShellPage.DefaultSndMSGCallback != null)
                {
                    SndPowerRenameSettings snd = new SndPowerRenameSettings(settings);
                    SndModuleSettings<SndPowerRenameSettings> ipcMessage = new SndModuleSettings<SndPowerRenameSettings>(snd);
                    ShellPage.DefaultSndMSGCallback(ipcMessage.ToJsonString());
                }
            }
        }

        private void Toggle_PowerRename_EnableOnContextMenu_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch swt = sender as ToggleSwitch;

            if (swt != null)
            {
                PowerRenameSettings settings = SettingsUtils.GetSettings<PowerRenameSettings>(POWERTOYNAME);
                settings.properties.ShowIconInMenu.value = swt.IsOn;

                if (ShellPage.DefaultSndMSGCallback != null)
                {
                    SndPowerRenameSettings snd = new SndPowerRenameSettings(settings);
                    SndModuleSettings<SndPowerRenameSettings> ipcMessage = new SndModuleSettings<SndPowerRenameSettings>(snd);
                    ShellPage.DefaultSndMSGCallback(ipcMessage.ToJsonString());
                }
            }
        }

        private void Toggle_PowerRename_EnableOnExtendedContextMenu_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch swt = sender as ToggleSwitch;

            if (swt != null)
            {
                PowerRenameSettings settings = SettingsUtils.GetSettings<PowerRenameSettings>(POWERTOYNAME);
                settings.properties.ShowExtendedMenu.value = swt.IsOn;

                if (ShellPage.DefaultSndMSGCallback != null)
                {
                    SndPowerRenameSettings snd = new SndPowerRenameSettings(settings);
                    SndModuleSettings<SndPowerRenameSettings> ipcMessage = new SndModuleSettings<SndPowerRenameSettings>(snd);
                    ShellPage.DefaultSndMSGCallback(ipcMessage.ToJsonString());
                }
            }
        }

        private void Toggle_PowerRename_RestoreFlagsOnLaunch_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch swt = sender as ToggleSwitch;

            if (swt != null)
            {
                PowerRenameSettings settings = SettingsUtils.GetSettings<PowerRenameSettings>(POWERTOYNAME);
                settings.properties.PersistInput.value = swt.IsOn;

                if (ShellPage.DefaultSndMSGCallback != null)
                {
                    SndPowerRenameSettings snd = new SndPowerRenameSettings(settings);
                    SndModuleSettings<SndPowerRenameSettings> ipcMessage = new SndModuleSettings<SndPowerRenameSettings>(snd);
                    ShellPage.DefaultSndMSGCallback(ipcMessage.ToJsonString());
                }
            }
        }

        private void Toggle_PowerRename_MaxDispListNum_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (sender != null)
            {
                PowerRenameSettings settings = SettingsUtils.GetSettings<PowerRenameSettings>(POWERTOYNAME);
                settings.properties.MaxMruSize.value = Convert.ToInt32(sender.Value);

                if (ShellPage.DefaultSndMSGCallback != null)
                {
                    SndPowerRenameSettings snd = new SndPowerRenameSettings(settings);
                    SndModuleSettings<SndPowerRenameSettings> ipcMessage = new SndModuleSettings<SndPowerRenameSettings>(snd);
                    ShellPage.DefaultSndMSGCallback(ipcMessage.ToJsonString());
                }
            }
        }
    }
}
