// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ListPage : Page,
    IRecipient<NavigateNextCommand>,
    IRecipient<NavigatePreviousCommand>
{
    public ListViewModel? ViewModel { get; set;  }

    public ListPage()
    {
        this.InitializeComponent();

        WeakReferenceMessenger.Default.Register<NavigateNextCommand>(this);
        WeakReferenceMessenger.Default.Register<NavigatePreviousCommand>(this);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is ListViewModel lvm)
        {
            ViewModel = lvm;
        }

        base.OnNavigatedTo(e);
    }

    private void ListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ListItemViewModel item)
        {
            ViewModel?.InvokeItemCommand.Execute(item);
        }
    }

    public void Receive(NavigateNextCommand message)
    {
        // Note: We may want to just have the notion of a 'SelectedCommand' in our VM
        // And then have these commands manipulate that state being bound to the UI instead
        // We may want to see how other non-list UIs need to behave to make this decision
        // At least it's decoupled from the SearchBox now :)
        if (ItemsList.SelectedIndex < ItemsList.Items.Count - 1)
        {
            ItemsList.SelectedIndex++;
            ItemsList.ScrollIntoView(ItemsList.SelectedItem);
        }
    }

    public void Receive(NavigatePreviousCommand message)
    {
        if (ItemsList.SelectedIndex > 0)
        {
            ItemsList.SelectedIndex--;
            ItemsList.ScrollIntoView(ItemsList.SelectedItem);
        }
    }
}
