// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ShortcutGuide.Helpers;
using ShortcutGuide.Models;

namespace ShortcutGuide.Pages
{
    public sealed partial class OverviewPage : Page, INotifyPropertyChanged
    {
        private ObservableCollection<ShortcutEntry>? _recommendedShortcuts;
        private ObservableCollection<ShortcutEntry>? _pinnedShortcuts;
        private ObservableCollection<ShortcutEntry>? _taskbarShortcuts;

        private int PinnedShortcutsCount => this._pinnedShortcuts?.Count ?? 0;

        private string _appName = string.Empty;
        private ShortcutFile _shortcutFile;

        public OverviewPage()
        {
            this.InitializeComponent();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is ShortcutPageNavParam param)
            {
                this._appName = param.AppName;
                this._shortcutFile = param.ShortcutFile;
                this._recommendedShortcuts = [.. this._shortcutFile.Shortcuts.SelectMany(list => list.Properties.Where(s => s.Recommended))];
                try
                {
                    if (App.PinnedShortcuts.TryGetValue(this._appName, out var shortcuts))
                    {
                        this._pinnedShortcuts = [.. shortcuts];
                    }

                    if (this._appName == ManifestInterpreter.GetIndexYamlFile().DefaultShellName)
                    {
                        this.TaskbarShortcutsPanel.Visibility = Visibility.Visible;
                        this._taskbarShortcuts =
                        [
                            .. this._shortcutFile.Shortcuts.First(x => x.SectionName.StartsWith("<TASKBAR1-9>", StringComparison.InvariantCulture)).Properties,
                        ];
                    }
                }
                catch
                {
                }
            }

            PinnedShortcutsHelper.PinnedShortcutsChanged += this.OnPinnedShortcutsChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            PinnedShortcutsHelper.PinnedShortcutsChanged -= this.OnPinnedShortcutsChanged;
        }

        private void OnPinnedShortcutsChanged(object? sender, string appName)
        {
            if (appName != this._appName)
            {
                return;
            }

            this._pinnedShortcuts = App.PinnedShortcuts.TryGetValue(this._appName, out var updated)
                ? [.. updated]
                : new ObservableCollection<ShortcutEntry>();
            this.PinnedShortcutsListView.ItemsSource = this._pinnedShortcuts;
            this.OnPropertyChanged(nameof(this.PinnedShortcutsCount));
        }

        private void OnPropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
