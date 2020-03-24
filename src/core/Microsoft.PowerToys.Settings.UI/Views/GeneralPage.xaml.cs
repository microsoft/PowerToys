// <copyright file="GeneralPage.xaml.cs" company="Microsoft Corp">
// Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>

namespace Microsoft.PowerToys.Settings.UI.Views
{
    using System;
    using System.IO;
    using Microsoft.PowerToys.Settings.UI.ViewModels;
    using Windows.UI.Popups;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Navigation;

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
            this.InitializeComponent();
        }

        /// <summary>
        /// Get the settings vakey value.
        /// </summary>
        /// <param name="dynSettings">json settings dynamic object.</param>
        /// <returns>string value of the setting.</returns>
        public string GetValue(dynamic dynSettings)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(dynSettings);
        }

        /// <summary>
        /// Get path to the json settings file.
        /// </summary>
        /// <returns>string path.</returns>
        public string GetSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Microsoft\\PowerToys\\settings.json");
        }

        /// <summary>
        /// Get a dynamic object of the json settings.
        /// </summary>
        /// <returns>dynamic json settings object.</returns>
        public dynamic GetGeneralSettings()
        {
            var jsonSettingsString = System.IO.File.ReadAllText(this.GetSettingsPath());
            return Newtonsoft.Json.JsonConvert.DeserializeObject(jsonSettingsString);
        }

        /// <summary>
        /// Save theme change settings.
        /// </summary>
        /// <param name="themeName">theme name.</param>
        public void SaveThemeSettings(string themeName)
        {
            dynamic settings = this.GetGeneralSettings();
            settings.theme = themeName;

            string jsonSettingsStringModified = Newtonsoft.Json.JsonConvert.SerializeObject(settings);
            System.IO.File.WriteAllText(this.GetSettingsPath(), this.FormatJson(jsonSettingsStringModified));
        }

        /// <summary>
        /// Save settings to a json file.
        /// </summary>
        /// <param name="settings">dynamic json settings object.</param>
        public void SaveSettings(dynamic settings)
        {
            string jsonSettingsStringModified = Newtonsoft.Json.JsonConvert.SerializeObject(settings);
            System.IO.File.WriteAllText(
                this.GetSettingsPath(),
                this.FormatJson(jsonSettingsStringModified));
        }

        /// <summary>
        /// remove unwanted chars.
        /// </summary>
        /// <param name="settings">json string settings.</param>
        /// <returns>formatted json string.</returns>
        public string FormatJson(string settings)
        {
            return settings.Replace("\\r\\n", string.Empty)
                           .Replace("\\", string.Empty)
                           .Replace(" ", string.Empty)
                           .Replace("\"{", "{")
                           .Replace("}\"", "}");
        }

        /// <summary>
        /// Update and save theme settings to json file.
        /// </summary>
        /// <param name="themeName">theme name.</param>
        private void UpdateTheme(string themeName)
        {
            switch (themeName.ToLower())
            {
                case "light":
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Light;
                    this.Rodio_Theme_Light.IsChecked = true;
                    this.SaveThemeSettings(themeName.ToLower());
                    break;
                case "dark":
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Dark;
                    this.Rodio_Theme_Dark.IsChecked = true;
                    this.SaveThemeSettings(themeName.ToLower());
                    break;
                case "system":
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Default;
                    this.Rodio_Theme_Default.IsChecked = true;
                    this.SaveThemeSettings(themeName.ToLower());
                    break;
            }
        }

        /// <inheritdoc/>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            this.UpdateTheme(
                this.GetValue(this.GetGeneralSettings().theme)
                .Replace("\"", string.Empty));

            string startup = this.GetValue(
                this.GetGeneralSettings().startup)
                .Replace("\"", string.Empty).ToLower();

            switch (startup)
            {
                case "true":
                    this.ToggleSwitch_RunAtStartUp.IsOn = true;
                    break;
                case "false":
                    this.ToggleSwitch_RunAtStartUp.IsOn = false;
                    break;
            }
        }

        private void ToggleSwitch_RunAtStartUp_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch swt = sender as ToggleSwitch;

            if (swt != null)
            {
                dynamic settings = this.GetGeneralSettings();

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

                this.SaveSettings(settings);

                dynamic baseObject = Newtonsoft.Json.JsonConvert.DeserializeObject("{\"general\":{}}");
                baseObject.general = this.GetValue(settings);

                string message = this.FormatJson(this.GetValue(baseObject));

                if (ShellPage.Run_OnStartUp_Callback != null)
                {
                    ShellPage.Run_OnStartUp_Callback(message);
                }
                else
                {
                }
            }
        }

        private void Restart_Elevated(object sender, RoutedEventArgs e)
        {
            dynamic modifiedSettings = this.GetGeneralSettings();
            modifiedSettings.run_elevated = true;

            dynamic baseObject = Newtonsoft.Json.JsonConvert.DeserializeObject("{\"general\":{}}");
            baseObject.general = this.GetValue(modifiedSettings);

            string message = this.FormatJson(this.GetValue(baseObject));

            if (ShellPage.Restart_Elevated_Callback != null)
            {
                ShellPage.Restart_Elevated_Callback(message);
            }
            else
            {
            }
        }

        private void Theme_Changed(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;

            if (rb != null)
            {
                string themeName = rb.Tag.ToString();
                this.UpdateTheme(themeName);
            }
        }
    }
}
