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
            this.InitializeComponent();
        }

        /// <inheritdoc/>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            PowerRenameSettings settings;
            try
            {
                settings = SettingsUtils.GetSettings<PowerRenameSettings>(POWERTOYNAME);
                this.UpdateView(settings);
            }
            catch
            {
                settings = new PowerRenameSettings(POWERTOYNAME);
                SettingsUtils.SaveSettings(settings.ToJsonString(), POWERTOYNAME);
                this.UpdateView(settings);
            }
        }

        private void UpdateView(PowerRenameSettings settings)
        {
            this.Toggle_PowerRename_Enable.IsOn = settings.properties.bool_mru_enabled.value;
            this.Toggle_PowerRename_EnableOnExtendedContextMenu.IsOn = settings.properties.bool_show_extended_menu.value;
            this.Toggle_PowerRename_MaxDispListNum.Value = settings.properties.int_max_mru_size.value;
            this.Toggle_PowerRename_EnableOnContextMenu.IsOn = settings.properties.bool_show_icon_on_menu.value;
            this.Toggle_PowerRename_RestoreFlagsOnLaunch.IsOn = settings.properties.bool_persist_input.value;
        }

        private void Toggle_PowerRename_Enable_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch swt = sender as ToggleSwitch;

            if (swt != null)
            {
                PowerRenameSettings settings = SettingsUtils.GetSettings<PowerRenameSettings>(POWERTOYNAME);
                settings.properties.bool_mru_enabled.value = swt.IsOn;

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
                settings.properties.bool_show_icon_on_menu.value = swt.IsOn;

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
                settings.properties.bool_show_extended_menu.value = swt.IsOn;

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
                settings.properties.bool_persist_input.value = swt.IsOn;

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
                settings.properties.int_max_mru_size.value = Convert.ToInt32(sender.Value);

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
