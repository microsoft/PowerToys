// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// Provides helpers for detaching item sources from a visual tree.
/// </summary>
public static class CleanupHelper
{
    /// <summary>
    /// Clears item sources on the specified element and its visual descendants.
    /// </summary>
    /// <param name="element">The root of the visual subtree to detach.</param>
    /// <remarks>Must be called on the UI thread.</remarks>
    public static void ClearItemsSources(FrameworkElement element)
    {
        var count = VisualTreeHelper.GetChildrenCount(element);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(element, index);
            if (child is FrameworkElement childElement)
            {
                ClearItemsSources(childElement);
            }
        }

        switch (element)
        {
            case ItemsControl itemsControl:
                itemsControl.ItemsSource = null!;
                break;
            case ItemsRepeater itemsRepeater:
                itemsRepeater.ItemsSource = null!;
                break;
            case TabView tabView:
                tabView.TabItemsSource = null!;
                break;
        }
    }
}
