// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
            this.Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ClearNestedItemsControls(this.OuterItemsControl);

            this.OuterItemsControl.ItemsSource = null;

            if (this.ContextFlyout is MenuFlyout flyout)
            {
                flyout.Opening -= PinFlyout_Opening!;
            }

            this.ContextFlyout = null;
            this.ClearValue(ShortcutProperty);
        }

        /// <summary>
        /// Recursively clears ItemsSource for all nested ItemsControl instances
        /// to break circular references and release converter-generated collections
        /// </summary>
        private void ClearNestedItemsControls(ItemsControl parentControl)
        {
            if (parentControl == null || parentControl.Items == null)
            {
                return;
            }

            // Iterate through realized items
            for (int i = 0; i < parentControl.Items.Count; i++)
            {
                // Get the container for this item
                var container = parentControl.ContainerFromIndex(i) as FrameworkElement;
                if (container == null)
                {
                    continue;
                }

                // Find any nested ItemsControl in the visual tree
                var nestedItemsControls = FindVisualChildren<ItemsControl>(container);
                foreach (var nestedControl in nestedItemsControls)
                {
                    ClearNestedItemsControls(nestedControl);
                    nestedControl.ItemsSource = null;
                }
            }
        }

        /// <summary>
        /// Finds all visual children of a specific type in the visual tree
        /// </summary>
        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
            where T : DependencyObject
        {
            if (parent == null)
            {
                yield break;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                {
                    yield return typedChild;
                }

                foreach (var descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
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
