// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    public static class NavHelper
    {
        // This helper class allows to specify the page that will be shown when you click on a NavigationViewItem
        //
        // Usage in xaml:
        // <winui:NavigationViewItem x:Uid="Shell_Main" Icon="Document" helpers:NavHelper.NavigateTo="views:MainPage" />
        //
        // Usage in code:
        // NavHelper.SetNavigateTo(navigationViewItem, typeof(MainPage));
        public static Type GetNavigateTo(NavigationViewItem item)
        {
            return (Type)item?.GetValue(NavigateToProperty);
        }

        public static void SetNavigateTo(NavigationViewItem item, Type value)
        {
            item?.SetValue(NavigateToProperty, value);
        }

        public static readonly DependencyProperty NavigateToProperty =
            DependencyProperty.RegisterAttached("NavigateTo", typeof(Type), typeof(NavHelper), new PropertyMetadata(null));
    }
}
