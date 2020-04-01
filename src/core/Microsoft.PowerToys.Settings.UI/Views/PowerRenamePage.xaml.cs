using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.ServiceModel.Channels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class PowerRenamePage : Page
    {
        public PowerRenameViewModel ViewModel { get; } = new PowerRenameViewModel();
        private const string POWERTOY_NAME = "PowerRename";

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
                settings = SettingsUtils.GetSettings<PowerRenameSettings>(POWERTOY_NAME);
                UpdateView(settings);
            }
            catch (Exception exp)
            {
                settings = new PowerRenameSettings(POWERTOY_NAME);
                SettingsUtils.SaveSettings<PowerRenameSettings>(settings, POWERTOY_NAME);
                UpdateView(settings);
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
                PowerRenameSettings settings = SettingsUtils.GetSettings<PowerRenameSettings>(POWERTOY_NAME);
                settings.properties.bool_mru_enabled.value = swt.IsOn;

                if (ShellPage.Default_SndMSG_Callback != null)
                {
                    ShellPage.Default_SndMSG_Callback(settings.IPCOutMessage());
                }
            }
        }
        
        private void Toggle_PowerRename_EnableOnContextMenu_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch swt = sender as ToggleSwitch;

            if (swt != null)
            {
                PowerRenameSettings settings = SettingsUtils.GetSettings<PowerRenameSettings>(POWERTOY_NAME);
                settings.properties.bool_show_icon_on_menu.value = swt.IsOn;

                if (ShellPage.Default_SndMSG_Callback != null)
                {
                    ShellPage.Default_SndMSG_Callback(settings.IPCOutMessage());
                }
            }
        }

        private void Toggle_PowerRename_EnableOnExtendedContextMenu_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch swt = sender as ToggleSwitch;

            if (swt != null)
            {
                PowerRenameSettings settings = SettingsUtils.GetSettings<PowerRenameSettings>(POWERTOY_NAME);
                settings.properties.bool_show_extended_menu.value = swt.IsOn;

                if (ShellPage.Default_SndMSG_Callback != null)
                {
                    ShellPage.Default_SndMSG_Callback(settings.IPCOutMessage());
                }
            }
        }

        private void Toggle_PowerRename_RestoreFlagsOnLaunch_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch swt = sender as ToggleSwitch;

            if (swt != null)
            {
                PowerRenameSettings settings = SettingsUtils.GetSettings<PowerRenameSettings>(POWERTOY_NAME);
                settings.properties.bool_persist_input.value = swt.IsOn;

                if (ShellPage.Default_SndMSG_Callback != null)
                {
                    ShellPage.Default_SndMSG_Callback(settings.IPCOutMessage());
                }
            }
        }

        private void Toggle_PowerRename_MaxDispListNum_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (sender != null)
            {
                PowerRenameSettings settings = SettingsUtils.GetSettings<PowerRenameSettings>(POWERTOY_NAME);
                settings.properties.int_max_mru_size.value = Convert.ToInt32(sender.Value);

                if (ShellPage.Default_SndMSG_Callback != null)
                {
                    ShellPage.Default_SndMSG_Callback(settings.IPCOutMessage());
                }
            }
        }
    }
}

