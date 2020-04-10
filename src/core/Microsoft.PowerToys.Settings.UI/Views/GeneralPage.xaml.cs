// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    /// <summary>
    /// General Settings Page.
    /// </summary>
    public sealed partial class GeneralPage : Page
    {
        /// <summary>
        /// Gets view model.
        /// </summary>
        public GeneralViewModel ViewModel { get; } = new GeneralViewModel();

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneralPage"/> class.
        /// General Settings page constructor.
        /// </summary>
        public GeneralPage()
        {
            InitializeComponent();
        }

        /// <inheritdoc/>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            GeneralSettings settings = null;
            try
            {
                // get settings file if they exist.
                settings = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);

                // load and apply theme settings
                ReLoadTheme(settings.theme);

                // load run on start-up settings value and update the ui state.
                ToggleSwitch_RunAtStartUp.IsOn = settings.startup;
            }
            catch
            {
                // create settings file if one is not found.
                settings = new GeneralSettings();
                SettingsUtils.SaveSettings(settings.ToJsonString(), string.Empty);

                // load and apply theme settings
                ReLoadTheme(settings.theme);

                // load run on start up ui settings value and update the ui state.
                ToggleSwitch_RunAtStartUp.IsOn = settings.startup;
            }
        }

        /// <summary>
        /// Update and save theme settings to json file.
        /// </summary>
        /// <param name="themeName">theme name.</param>
        private void ReLoadTheme(string themeName)
        {
            switch (themeName.ToLower())
            {
                case "light":
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Light;
                    Radio_Theme_Light.IsChecked = true;
                    break;
                case "dark":
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Dark;
                    Radio_Theme_Dark.IsChecked = true;
                    break;
                case "system":
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Default;
                    Radio_Theme_Default.IsChecked = true;
                    break;
            }
        }

        private void ToggleSwitch_RunAtStartUp_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch swt = sender as ToggleSwitch;

            if (swt != null)
            {
                GeneralSettings settings = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);

                string startup = swt.IsOn.ToString().ToLower();
                switch (startup)
                {
                    case "true":
                        settings.startup = true;
                        break;
                    case "false":
                        settings.startup = false;
                        break;
                }

                SettingsUtils.SaveSettings(settings.ToJsonString(), string.Empty);
                OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(settings);

                if (ShellPage.DefaultSndMSGCallback != null)
                {
                    ShellPage.DefaultSndMSGCallback(outsettings.ToString());
                }
            }
        }

        private void Restart_Elevated(object sender, RoutedEventArgs e)
        {
            GeneralSettings settings = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);
            settings.run_elevated = true;
            OutGoingGeneralSettings outsettings = new OutGoingGeneralSettings(settings);

            if (ShellPage.DefaultSndMSGCallback != null)
            {
                ShellPage.DefaultSndMSGCallback(outsettings.ToString());
            }
        }

        private void Theme_Changed(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;

            if (rb != null)
            {
                string themeName = rb.Tag.ToString();
                ReLoadTheme(themeName);

                // update and save settings to file.
                GeneralSettings settings = SettingsUtils.GetSettings<GeneralSettings>(string.Empty);
                settings.theme = themeName;
                SettingsUtils.SaveSettings(settings.ToJsonString(), string.Empty);
            }
        }

        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/microsoft/PowerToys/releases"));
        }
    }
}
