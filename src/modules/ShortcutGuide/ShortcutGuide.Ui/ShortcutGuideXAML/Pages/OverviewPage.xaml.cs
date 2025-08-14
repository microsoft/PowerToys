// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Linq;
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

        public OverviewPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is ShortcutPageParam param)
            {
                if (param.ShortcutFile is ShortcutFile file)
                {
                    _recommendedShortcuts = [.. file.Shortcuts.SelectMany(list => list.Properties.Where(s => s.Recommended))];
                }

                if (param.AppName is string appName)
                {
                    _pinnedShortcuts = [.. App.PinnedShortcuts[appName]];
                }
            }
        }
    }
}
