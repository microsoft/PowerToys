// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ActionBar : UserControl,
    IRecipient<UpdateActionBarMessage>
{
    public ActionBarViewModel ViewModel { get; set; } = new();

    public ActionBar()
    {
        this.InitializeComponent();
        WeakReferenceMessenger.Default.Register<UpdateActionBarMessage>(this);
    }

    private void ActionListViewItem_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // TODO
    }

    private void ActionListViewItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        MoreCommandsButton.Flyout.Hide();

        if (sender is not ListViewItem listItem)
        {
            return;
        }

        if (listItem.DataContext is CommandContextItemViewModel item)
        {
            ViewModel?.InvokeItemCommand.Execute(item);
        }
    }

    public void Receive(UpdateActionBarMessage message) => ViewModel.SelectedItem = message.ViewModel;
}
