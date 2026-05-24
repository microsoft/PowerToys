// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ShortcutGuide.Helpers;
using ShortcutGuide.Models;

namespace ShortcutGuide.Controls
{
    public sealed partial class ShortcutItemView : UserControl
    {
        public static readonly DependencyProperty ShortcutProperty = DependencyProperty.Register(
            nameof(Shortcut),
            typeof(ShortcutEntry),
            typeof(ShortcutItemView),
            new PropertyMetadata(default(ShortcutEntry)));

        public ShortcutEntry Shortcut
        {
            get => (ShortcutEntry)this.GetValue(ShortcutProperty);
            set => this.SetValue(ShortcutProperty, value);
        }

        public ShortcutItemView()
        {
            this.InitializeComponent();
        }

        private void PinFlyout_Opening(object sender, object e)
        {
            string appName = App.CurrentAppName;
            bool isPinned = App.PinnedShortcuts.TryGetValue(appName, out var pinned)
                && pinned.Any(x => x.Equals(this.Shortcut));

            this.PinMenuItem.Text = ResourceLoaderInstance.ResourceLoader.GetString(
                isPinned ? "UnpinShortcut" : "PinShortcut");
            this.PinMenuItem.Icon = new SymbolIcon(isPinned ? Symbol.UnPin : Symbol.Pin);
        }

        private void Pin_Click(object sender, RoutedEventArgs e)
        {
            PinnedShortcutsHelper.UpdatePinnedShortcuts(App.CurrentAppName, this.Shortcut);
        }
    }
}
