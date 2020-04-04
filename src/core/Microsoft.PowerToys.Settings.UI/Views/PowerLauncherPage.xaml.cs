// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
﻿using Microsoft.PowerToys.Settings.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.PowerToys.Settings.UI.Controls;
using System.Collections.ObjectModel;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class PowerLauncherPage : Page
    {
        public PowerLauncherViewModel ViewModel { get; } = new PowerLauncherViewModel();
        ObservableCollection<Tuple<string, string>> SearchResultPreferencesOptions;
        ObservableCollection<Tuple<string, string>> SearchTypePreferencesOptions;

        public PowerLauncherPage()
        {
            this.InitializeComponent();

            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
            
            SearchResultPreferencesOptions = new ObservableCollection<Tuple<string, string>>();
            SearchResultPreferencesOptions.Add(Tuple.Create(loader.GetString("PowerLauncher_SearchResultPreference_AlphabeticalOrder"), "alphabetical_order"));
            SearchResultPreferencesOptions.Add(Tuple.Create(loader.GetString("PowerLauncher_SearchResultPreference_MostRecentlyUsed"), "most_recently_used"));
            SearchResultPreferencesOptions.Add(Tuple.Create(loader.GetString("PowerLauncher_SearchResultPreference_RunningProcessesOpenApplications"), "running_processes_open_applications"));

            SearchTypePreferencesOptions = new ObservableCollection<Tuple<string, string>>();
            SearchTypePreferencesOptions.Add(Tuple.Create(loader.GetString("PowerLauncher_SearchTypePreference_ApplicationName"), "application_name"));
            SearchTypePreferencesOptions.Add(Tuple.Create(loader.GetString("PowerLauncher_SearchTypePreference_StringInApplication"), "string_in_application"));
            SearchTypePreferencesOptions.Add(Tuple.Create(loader.GetString("PowerLauncher_SearchTypePreference_ExecutableName"), "executable_name"));

        }

        public Tuple<string, string> SelectedSearchResultPreference
        {
            get
            {
                return SearchResultPreferencesOptions.First(item => item.Item2 == ViewModel.SearchResultPreference);
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
                return SearchTypePreferencesOptions.First(item => item.Item2 == ViewModel.SearchTypePreference);
            }
            set
            {
                if (ViewModel.SearchTypePreference != value.Item2)
                {
                    ViewModel.SearchTypePreference = value.Item2;
                }
            }
        }
    }
}
