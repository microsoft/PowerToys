// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.CmdPal.UI.Pages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Microsoft.CmdPal.UI;

public sealed partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        this.InitializeComponent();
        this.ExtendsContentIntoTitleBar = true;
        this.AppWindow.SetIcon("ms-appx:///Assets/Icons/StoreLogo.png");
        this.AppWindow.Title = "Command Palette Settings";
        this.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        NavView.SelectedItem = NavView.MenuItems[0];
        Navigate("General");
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var selectedItem = args.InvokedItem;

        if (selectedItem is not null)
        {
            Navigate(selectedItem.ToString()!);
        }
    }

    private void Navigate(string page)
    {
        var pageType = page switch
        {
            "General" => typeof(GeneralPage),
            "Extensions" => typeof(ExtensionsPage),
            _ => null,
        };
        if (pageType is not null)
        {
            NavFrame.Navigate(pageType);
        }
    }
}
