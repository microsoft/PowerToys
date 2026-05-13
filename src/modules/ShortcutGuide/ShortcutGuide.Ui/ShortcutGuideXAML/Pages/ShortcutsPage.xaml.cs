// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ShortcutGuide.Models;

namespace ShortcutGuide.Pages
{
    public sealed partial class ShortcutsPage : Page
    {
        private ObservableCollection<ShortcutEntry>? _shortcuts;
        private ShortcutFile _shortcutFile;
        private int _pageIndex;

        public ShortcutsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is ShortcutPageNavParam param)
            {
                this._shortcutFile = param.ShortcutFile;
                this._pageIndex = param.PageIndex;
                this._shortcuts = [.. this._shortcutFile.Shortcuts[this._pageIndex].Properties ?? Enumerable.Empty<ShortcutEntry>()];
            }
        }
    }
}
