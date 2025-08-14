// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        private ShortcutPageParam _shortcutPageParam = null!;

        public OverviewPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is ShortcutPageParam param)
            {
                _shortcutPageParam = param;
            }

            if (_shortcutPageParam.ShortcutFile is ShortcutFile file)
            {
                _recommendedShortcuts = [.. file.Shortcuts.SelectMany(list => list.Properties.Where(s => s.Recommended))];
            }

            if (_shortcutPageParam.AppName is string appName)
            {
                _pinnedShortcuts = [.. App.PinnedShortcuts[appName]];
            }
        }

        private void PinFlyout_Opening(object sender, object e)
        {
            if (sender is not MenuFlyout menu || menu.Target is not Grid parentGrid || parentGrid.DataContext is not ShortcutEntry dataObject || menu.Items[0] is not MenuFlyoutItem pinItem)
            {
                return;
            }

            if (_shortcutPageParam.AppName is string appName)
            {
                bool isItemPinned = App.PinnedShortcuts[appName].Any(x => x.Equals(dataObject));
                pinItem.Text = isItemPinned ? ResourceLoaderInstance.ResourceLoader.GetString("UnpinShortcut") : ResourceLoaderInstance.ResourceLoader.GetString("PinShortcut");
                pinItem.Icon = new SymbolIcon(isItemPinned ? Symbol.UnPin : Symbol.Pin);
            }
        }

        private void Pin_Click(object sender, RoutedEventArgs e)
        {
            if (_shortcutPageParam.AppName is string appName && ((MenuFlyoutItem)sender).DataContext is ShortcutEntry shortcutEntry)
            {
                PinnedShortcutsHelper.UpdatePinnedShortcuts(appName, shortcutEntry);

                // TO DO: Data on this page should be refreshed to reflect the change.
            }
        }
    }
}
