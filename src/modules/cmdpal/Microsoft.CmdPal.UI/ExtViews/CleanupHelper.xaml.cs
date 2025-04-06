// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI;

public static class CleanupHelper
{
    public static void Cleanup(FrameworkElement element)
    {
        var count = VisualTreeHelper.GetChildrenCount(element);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(element, index);
            if (child is FrameworkElement childElement)
            {
                Cleanup(childElement);
            }
        }

        switch (element)
        {
            case ItemsControl itemsControl:
                itemsControl.ItemsSource = null;
                break;
            case ItemsRepeater itemsRepeater:
                itemsRepeater.ItemsSource = null;
                break;
            case TabView tabView:
                tabView.TabItemsSource = null;
                break;
        }
    }
}
