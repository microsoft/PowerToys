// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ListPage : Page
{
    public ListViewModel ViewModel { get; set;  } = new();

    public ListPage()
    {
        this.InitializeComponent();
        ViewModel.Items.Add(new ListItemViewModel { Header = "Hello", SubHeader = "World" });
        ViewModel.Items.Add(new ListItemViewModel { Header = "Clint", SubHeader = "Rutkas" });
        ViewModel.Items.Add(new ListItemViewModel { Header = "Michael", SubHeader = "Hawker" });
    }

    private void ItemsView_ItemInvoked(ItemsView sender, ItemsViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is ListItemViewModel item)
        {
            ViewModel.InvokeItemCommand.Execute(item);
        }
    }
}
