// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ShortcutGuide.Helpers;
using ShortcutGuide.Models;
using Windows.ApplicationModel.VoiceCommands;

namespace ShortcutGuide.Pages
{
    public sealed partial class ShortcutsPage : Page
    {
        private ObservableCollection<ShortcutEntry>? _shortcuts;
        private string _appName = string.Empty;
        private ShortcutFile _shortcutFile;
        private int _pageIndex;

        public ShortcutsPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is ShortcutPageParam param)
            {
                _appName = param.AppName;
                _shortcutFile = param.ShortcutFile;
                _pageIndex = param.PageIndex;
                _shortcuts = [.. _shortcutFile.Shortcuts[_pageIndex].Properties ?? Enumerable.Empty<ShortcutEntry>()];
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
            }
        }
    }
}
