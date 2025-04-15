// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.IO;
using Common.UI;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class PowerLauncherPage : Page, IRefreshablePage
    {
        public PowerLauncherViewModel ViewModel { get; set; }

        private readonly ObservableCollection<Tuple<string, string>> searchResultPreferencesOptions;
        private readonly ObservableCollection<Tuple<string, string>> searchTypePreferencesOptions;

        private int _lastIPCMessageSentTick;

        // Keep track of the last IPC Message that was sent.
        private int SendDefaultIPCMessageTimed(string msg)
        {
            _lastIPCMessageSentTick = Environment.TickCount;
            return ShellPage.SendDefaultIPCMessage(msg);
        }

        public PowerLauncherPage()
        {
            InitializeComponent();
            var settingsUtils = new SettingsUtils();
            _lastIPCMessageSentTick = Environment.TickCount;

            PowerLauncherSettings settings = SettingsRepository<PowerLauncherSettings>.GetInstance(settingsUtils)?.SettingsConfig;
            ViewModel = new PowerLauncherViewModel(settings, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), SendDefaultIPCMessageTimed, App.IsDarkTheme);
            DataContext = ViewModel;
            _ = Helper.GetFileWatcher(PowerLauncherSettings.ModuleName, "settings.json", () =>
            {
                if (Environment.TickCount < _lastIPCMessageSentTick + 500)
                {
                    // Don't try to update data from the file if we tried to write to it through IPC in the last 500 milliseconds.
                    return;
                }

                PowerLauncherSettings powerLauncherSettings = null;
                try
                {
                    powerLauncherSettings = SettingsRepository<PowerLauncherSettings>.GetInstance(settingsUtils)?.SettingsConfig;
                }
                catch (IOException ex)
                {
                    Logger.LogInfo(ex.Message);
                }

                if (powerLauncherSettings != null && !ViewModel.IsUpToDate(powerLauncherSettings))
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        DataContext = ViewModel = new PowerLauncherViewModel(powerLauncherSettings, SettingsRepository<GeneralSettings>.GetInstance(settingsUtils), ShellPage.SendDefaultIPCMessage, App.IsDarkTheme);
                        this.Bindings.Update();
                    });
                }
            });

            var loader = Helpers.ResourceLoaderInstance.ResourceLoader;

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

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }

        private void NavigateCmdPalSettings_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            SettingsDeepLink.OpenSettings(SettingsDeepLink.SettingsWindow.CmdPal, true);
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
