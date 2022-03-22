// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;
using Windows.UI.Core;

namespace Microsoft.PowerToys.Settings.UI.WinUI3.Views
{
    public sealed partial class PowerLauncherPage : Page
    {
        public PowerLauncherViewModel ViewModel { get; set; }

        private readonly ObservableCollection<Tuple<string, string>> searchResultPreferencesOptions;
        private readonly ObservableCollection<Tuple<string, string>> searchTypePreferencesOptions;

        public PowerLauncherPage()
        {
            InitializeComponent();
            var settingsUtils = new SettingsUtils();
            PowerLauncherSettings settings = settingsUtils.GetSettingsOrDefault<PowerLauncherSettings>(PowerLauncherSettings.ModuleName);
            ViewModel = new PowerLauncherViewModel(settings, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage, App.IsDarkTheme);
            DataContext = ViewModel;
            _ = Helper.GetFileWatcher(PowerLauncherSettings.ModuleName, "settings.json", () =>
            {
                PowerLauncherSettings powerLauncherSettings = null;
                try
                {
                    powerLauncherSettings = settingsUtils.GetSettingsOrDefault<PowerLauncherSettings>(PowerLauncherSettings.ModuleName);
                }
                catch (IOException ex)
                {
                    Logger.LogInfo(ex.Message);
                }

                if (powerLauncherSettings != null && !ViewModel.IsUpToDate(powerLauncherSettings))
                {
                    _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                    DataContext = ViewModel = new PowerLauncherViewModel(powerLauncherSettings, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage, App.IsDarkTheme);
                        this.Bindings.Update();
                    });
                }
            });

            var loader = ResourceLoader.GetForViewIndependentUse();

            searchResultPreferencesOptions = new ObservableCollection<Tuple<string, string>>();
            searchResultPreferencesOptions.Add(Tuple.Create(loader.GetString("PowerLauncher_SearchResultPreference_AlphabeticalOrder"), "alphabetical_order"));
            searchResultPreferencesOptions.Add(Tuple.Create(loader.GetString("PowerLauncher_SearchResultPreference_MostRecentlyUsed"), "most_recently_used"));
            searchResultPreferencesOptions.Add(Tuple.Create(loader.GetString("PowerLauncher_SearchResultPreference_RunningProcessesOpenApplications"), "running_processes_open_applications"));

            searchTypePreferencesOptions = new ObservableCollection<Tuple<string, string>>();
            searchTypePreferencesOptions.Add(Tuple.Create(loader.GetString("PowerLauncher_SearchTypePreference_ApplicationName"), "application_name"));
            searchTypePreferencesOptions.Add(Tuple.Create(loader.GetString("PowerLauncher_SearchTypePreference_StringInApplication"), "string_in_application"));
            searchTypePreferencesOptions.Add(Tuple.Create(loader.GetString("PowerLauncher_SearchTypePreference_ExecutableName"), "executable_name"));
        }

        private void OpenColorsSettings_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Helpers.StartProcessHelper.Start(Helpers.StartProcessHelper.ColorsSettings);
        }

        /*
        public Tuple<string, string> SelectedSearchResultPreference
        {
            get
            {
                return searchResultPreferencesOptions.First(item => item.Item2 == ViewModel.SearchResultPreference);
            }

            set
            {
                if (ViewModel.SearchResultPreference != value.Item2)
                {
                    ViewModel.SearchResultPreference = value.Item2;
                }
            }
        }

        public Tuple<string, string> SelectedSearchTypePreference
        {
            get
            {
                return searchTypePreferencesOptions.First(item => item.Item2 == ViewModel.SearchTypePreference);
            }

            set
            {
                if (ViewModel.SearchTypePreference != value.Item2)
                {
                    ViewModel.SearchTypePreference = value.Item2;
                }
            }
        }
        */
    }
}
