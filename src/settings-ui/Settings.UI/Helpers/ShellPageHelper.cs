// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    public static class ShellPageHelper
    {
        /// <summary>
        /// Sort the Navigation-Pane items alphabetically based on their display names
        /// </summary>
        public static void SortNavigationPaneItems(NavigationView navigationView)
        {
            // We want to sort the items after the separator
            int separatorIndex = navigationView.MenuItems.IndexOf(
                navigationView.MenuItems.OfType<NavigationViewItemSeparator>().FirstOrDefault());

            var itemsToSort = navigationView.MenuItems
                .OfType<NavigationViewItem>()
                .Skip(separatorIndex)
                .OrderBy(item => item.Content.ToString())
                .ToList();

            // Arrange the items in the sorted order
            for (int i = navigationView.MenuItems.Count - 1; i > separatorIndex; i--)
            {
                navigationView.MenuItems.RemoveAt(i);
            }

            foreach (var sortedItem in itemsToSort)
            {
                navigationView.MenuItems.Add(sortedItem);
            }
        }
    }
}
