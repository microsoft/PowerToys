// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class PowerLauncherPage : Page
    {
        public PowerLauncherViewModel ViewModel { get; } = new PowerLauncherViewModel();

        private readonly ObservableCollection<Tuple<string, string>> searchResultPreferencesOptions;
        private readonly ObservableCollection<Tuple<string, string>> searchTypePreferencesOptions;

        public PowerLauncherPage()
        {
            this.InitializeComponent();

            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();

            this.searchResultPreferencesOptions = new ObservableCollection<Tuple<string, string>>();
            this.searchResultPreferencesOptions.Add(Tuple.Create(loader.GetString("PowerLauncher_SearchResultPreference_AlphabeticalOrder"), "alphabetical_order"));
            this.searchResultPreferencesOptions.Add(Tuple.Create(loader.GetString("PowerLauncher_SearchResultPreference_MostRecentlyUsed"), "most_recently_used"));
            this.searchResultPreferencesOptions.Add(Tuple.Create(loader.GetString("PowerLauncher_SearchResultPreference_RunningProcessesOpenApplications"), "running_processes_open_applications"));

            this.searchTypePreferencesOptions = new ObservableCollection<Tuple<string, string>>();
            this.searchTypePreferencesOptions.Add(Tuple.Create(loader.GetString("PowerLauncher_SearchTypePreference_ApplicationName"), "application_name"));
            this.searchTypePreferencesOptions.Add(Tuple.Create(loader.GetString("PowerLauncher_SearchTypePreference_StringInApplication"), "string_in_application"));
            this.searchTypePreferencesOptions.Add(Tuple.Create(loader.GetString("PowerLauncher_SearchTypePreference_ExecutableName"), "executable_name"));
        }

        public Tuple<string, string> SelectedSearchResultPreference
        {
            get
            {
                return this.searchResultPreferencesOptions.First(item => item.Item2 == this.ViewModel.SearchResultPreference);
            }

            set
            {
                if (this.ViewModel.SearchResultPreference != value.Item2)
                {
                    this.ViewModel.SearchResultPreference = value.Item2;
                }
            }
        }

        public Tuple<string, string> SelectedSearchTypePreference
        {
            get
            {
                return this.searchTypePreferencesOptions.First(item => item.Item2 == this.ViewModel.SearchTypePreference);
            }

            set
            {
                if (this.ViewModel.SearchTypePreference != value.Item2)
                {
                    this.ViewModel.SearchTypePreference = value.Item2;
                }
            }
        }
    }
}
