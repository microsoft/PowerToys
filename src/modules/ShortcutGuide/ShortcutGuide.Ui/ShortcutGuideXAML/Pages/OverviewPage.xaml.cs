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

        private int PinnedShortcutsCount => _pinnedShortcuts?.Count ?? 0;

        private string _appName = string.Empty;
        private ShortcutFile _shortcutFile;

        public OverviewPage()
        {
            InitializeComponent();
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is ShortcutPageNavParam param)
            {
                _appName = param.AppName;
                _shortcutFile = param.ShortcutFile;
                _recommendedShortcuts = [.. _shortcutFile.Shortcuts.SelectMany(list => list.Properties.Where(s => s.Recommended))];
                if (App.PinnedShortcuts.TryGetValue(_appName, out var shortcuts))
                {
                    _pinnedShortcuts = [.. shortcuts];
                }

                if (_appName == ManifestInterpreter.GetIndexYamlFile().DefaultShellName)
                {
                    TaskbarShortcutsPanel.Visibility = Visibility.Visible;
                    _taskbarShortcuts =
                    [
                        .. _shortcutFile.Shortcuts.First(x => x.SectionName.StartsWith("<TASKBAR1-9>", StringComparison.InvariantCulture)).Properties,
                    ];
                }
            }
        }

        private void PinFlyout_Opening(object sender, object e)
        {
            if (sender is MenuFlyout fl && fl.Target is Grid g && g.Tag is ShortcutEntry dataObject && fl.Items[0] is MenuFlyoutItem pinItem)
            {
                bool isItemPinned = App.PinnedShortcuts[_appName].Any(x => x.Equals(dataObject));
                pinItem.Text = isItemPinned ? ResourceLoaderInstance.ResourceLoader.GetString("UnpinShortcut") : ResourceLoaderInstance.ResourceLoader.GetString("PinShortcut");
                pinItem.Icon = new SymbolIcon(isItemPinned ? Symbol.UnPin : Symbol.Pin);
            }
        }

        private void Pin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem { CommandParameter: ShortcutEntry shortcutEntry })
            {
                PinnedShortcutsHelper.UpdatePinnedShortcuts(_appName, shortcutEntry);

                // Update ListView to reflect changes
                _pinnedShortcuts = [.. App.PinnedShortcuts[_appName]];
                PinnedShortcutsListView.ItemsSource = _pinnedShortcuts;
                OnPropertyChanged(nameof(PinnedShortcutsCount));
            }
        }
    }
}
