using Microsoft.PowerToys.Settings.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
    public sealed partial class GeneralPage : Page
    {
        public GeneralViewModel ViewModel { get; } = new GeneralViewModel();

        public GeneralPage()
        {
            this.InitializeComponent();
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            UpdateTheme(GetSettinValue(GetGeneralSettings().theme).Replace("\"", ""));

            string startup = GetSettinValue(GetGeneralSettings().startup).Replace("\"", "").ToLower();

            switch (startup)
            {
                case "true":
                    ToggleSwitch_RunAtStartUp.IsOn = true;
                    break;
                case "false":
                    ToggleSwitch_RunAtStartUp.IsOn = false;
                    break;
            }
        }

        private void ToggleSwitch_RunAtStartUp_Toggled(object sender, RoutedEventArgs e)
        {
            ToggleSwitch swt = sender as ToggleSwitch;

            if(swt != null)
            {
                dynamic settings = GetGeneralSettings();
                settings.startup = swt.IsOn.ToString().ToLower();
                SaveSettings(settings);

                dynamic baseObject = Newtonsoft.Json.JsonConvert.DeserializeObject("{\"general\":{}}");
                baseObject.general = GetSettinValue(settings);

                string message = FormatJson(GetSettinValue(baseObject));

                if (ShellPage.Run_OnStartUp_Callback != null)
                {
                    ShellPage.Run_OnStartUp_Callback(message);
                }
                else
                {
                    new MessageDialog("Callback function not yet defined").ShowAsync();
                }
            }
        }

        private void Restart_Elevated(object sender, RoutedEventArgs e)
        {
            dynamic modifiedSettings = GetGeneralSettings();
            modifiedSettings.run_elevated = true;

            dynamic baseObject = Newtonsoft.Json.JsonConvert.DeserializeObject("{\"general\":{}}");
            baseObject.general = GetSettinValue(modifiedSettings);

            string message = FormatJson(GetSettinValue(baseObject));

            //show message generated.
            //new MessageDialog(message).ShowAsync();

            if (ShellPage.Restart_Elevated_Callback != null)
            {
                ShellPage.Restart_Elevated_Callback(message);
            }
            else
            {
                new MessageDialog("Callback function not yet defined").ShowAsync();
            }

        }

        private void Theme_Changed(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;

            if (rb != null)
            {
                string themeName = rb.Tag.ToString();
                UpdateTheme(themeName);
            }
        }

        public void UpdateTheme(string themeName)
        {
            switch (themeName)
            {
                case "Light":
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Light;
                    Rodio_Theme_Light.IsChecked = true;
                    SaveThemeSettings(themeName);
                    break;
                case "Dark":
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Dark;
                    Rodio_Theme_Dark.IsChecked = true;
                    SaveThemeSettings(themeName);
                    break;
                case "System":
                    ShellPage.ShellHandler.RequestedTheme = ElementTheme.Default;
                    Rodio_Theme_Default.IsChecked = true;
                    SaveThemeSettings(themeName);
                    break;
            }
        }

        public string GetSettinValue(dynamic dynSettings)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(dynSettings);
        }

        public string GetSettingsPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\PowerToys\\settings.json");
        }

        public dynamic GetGeneralSettings()
        {
            var jsonSettingsString = System.IO.File.ReadAllText(GetSettingsPath());
            return Newtonsoft.Json.JsonConvert.DeserializeObject(jsonSettingsString);
        }

        public void SaveThemeSettings(string themeName)
        {
            dynamic settings = GetGeneralSettings();
            settings.theme = themeName;

            string jsonSettingsStringModified = Newtonsoft.Json.JsonConvert.SerializeObject(settings);
            System.IO.File.WriteAllText(GetSettingsPath(), this.FormatJson(jsonSettingsStringModified));
        }

        public void SaveSettings(dynamic settings)
        {
            string jsonSettingsStringModified = Newtonsoft.Json.JsonConvert.SerializeObject(settings);
            System.IO.File.WriteAllText(GetSettingsPath(), this.FormatJson(jsonSettingsStringModified));
        }

        public string FormatJson(string settings)
        {
            return settings.Replace("\\r\\n", "").Replace("\\", "").Replace(" ", "").Replace("\"{", "{").Replace("}\"", "}");
        }

    }
}
