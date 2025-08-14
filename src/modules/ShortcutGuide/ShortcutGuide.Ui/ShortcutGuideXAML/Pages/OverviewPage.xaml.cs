// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ShortcutGuide.Helpers;
using ShortcutGuide.Models;

namespace ShortcutGuide.Pages
{
    public sealed partial class OverviewPage : Page
    {
        private ObservableCollection<ShortcutEntry>? _recommendedShortcuts;
        private ObservableCollection<ShortcutEntry>? _pinnedShortcuts;
        private ObservableCollection<ShortcutEntry>? _taskbarShortcuts;
        private string _appName = string.Empty;
        private ShortcutFile _shortcutFile;

        public OverviewPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is ShortcutPageParam param)
            {
                _appName = param.AppName;
                _shortcutFile = param.ShortcutFile;
                _recommendedShortcuts = [.. _shortcutFile.Shortcuts.SelectMany(list => list.Properties.Where(s => s.Recommended))];
                _pinnedShortcuts = [.. App.PinnedShortcuts[_appName]];

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
            if (sender is not MenuFlyout menu || menu.Target is not Grid parentGrid || parentGrid.DataContext is not ShortcutEntry dataObject || menu.Items[0] is not MenuFlyoutItem pinItem)
            {
                return;
            }

            bool isItemPinned = App.PinnedShortcuts[_appName].Any(x => x.Equals(dataObject));
            pinItem.Text = isItemPinned ? ResourceLoaderInstance.ResourceLoader.GetString("UnpinShortcut") : ResourceLoaderInstance.ResourceLoader.GetString("PinShortcut");
            pinItem.Icon = new SymbolIcon(isItemPinned ? Symbol.UnPin : Symbol.Pin);
        }

        private void Pin_Click(object sender, RoutedEventArgs e)
        {
            if (((MenuFlyoutItem)sender).DataContext is ShortcutEntry shortcutEntry)
            {
                PinnedShortcutsHelper.UpdatePinnedShortcuts(_appName, shortcutEntry);

                // Update ListView to reflect changes
                _pinnedShortcuts = [.. App.PinnedShortcuts[_appName]];
                PinnedShortcutsListView.ItemsSource = _pinnedShortcuts;
            }
        }
    }
}
