// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ShortcutGuide.Helpers;
using ShortcutGuide.Models;

namespace ShortcutGuide.Pages
{
    public sealed partial class ShortcutsPage : Page
    {
        private ObservableCollection<ShortcutEntry>? _shortcuts;

        public ShortcutsPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is ShortcutPageParam param && param.ShortcutFile is ShortcutFile file && param.PageIndex is int index)
            {
                _shortcuts = [.. file.Shortcuts[index].Properties ?? Enumerable.Empty<ShortcutEntry>()];
            }
        }
    }
}
