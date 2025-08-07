// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Services;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public class SearchResultsViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<SettingEntry> _moduleResults = new();
        private ObservableCollection<SettingsGroup> _groupedSettingsResults = new();
        private bool _hasNoResults;

        public ObservableCollection<SettingEntry> ModuleResults
        {
            get => _moduleResults;
            set
            {
                _moduleResults = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<SettingsGroup> GroupedSettingsResults
        {
            get => _groupedSettingsResults;
            set
            {
                _groupedSettingsResults = value;
                OnPropertyChanged();
            }
        }

        public bool HasNoResults
        {
            get => _hasNoResults;
            set
            {
                _hasNoResults = value;
                OnPropertyChanged();
            }
        }

        public void SetSearchResults(string query, List<SettingEntry> results)
        {
            if (results == null || results.Count == 0)
            {
                HasNoResults = true;
                ModuleResults.Clear();
                GroupedSettingsResults.Clear();
                return;
            }

            HasNoResults = false;

            // Separate modules and settings
            var modules = results.Where(r => r.Type == EntryType.SettingsPage).ToList();
            var settings = results.Where(r => r.Type == EntryType.SettingsCard).ToList();

            // Update module results
            ModuleResults.Clear();
            foreach (var module in modules)
            {
                ModuleResults.Add(module);
            }

            // Group settings by their page/module
            var groupedSettings = settings
                .GroupBy(s => s.Header)
                .Select(g => new SettingsGroup
                {
                    GroupName = g.Key,
                    Settings = new ObservableCollection<SettingEntry>(g),
                })
                .ToList();

            GroupedSettingsResults.Clear();
            foreach (var group in groupedSettings)
            {
                GroupedSettingsResults.Add(group);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
    public class SettingsGroup : INotifyPropertyChanged
#pragma warning restore SA1402 // File may only contain a single type
    {
        private string _groupName;
        private ObservableCollection<SettingEntry> _settings;

        public string GroupName
        {
            get => _groupName;
            set
            {
                _groupName = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<SettingEntry> Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
