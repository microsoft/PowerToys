// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Microsoft.CmdPal.UI;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainPage : Page
{
    public ListViewModel ViewModel { get; set;  } = new();

    public MainPage()
    {
        this.InitializeComponent();
        ViewModel.Items.Add(new ListItemViewModel { Header = "Hello", Subheader = "World" });
        ViewModel.Items.Add(new ListItemViewModel { Header = "Clint", Subheader = "Rutkas" });
        ViewModel.Items.Add(new ListItemViewModel { Header = "Michael", Subheader = "Hawker" });
    }

    private void MyButton_Click(object sender, RoutedEventArgs e)
    {
        // myButton.Content = "Clicked";
    }

    private void ItemsView_ItemInvoked(ItemsView sender, ItemsViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is ListItemViewModel item)
        {
            ViewModel.InvokeItemCommand.Execute(item);
        }
    }
}
