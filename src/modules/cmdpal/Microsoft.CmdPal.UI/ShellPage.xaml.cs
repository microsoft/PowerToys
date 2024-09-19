// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ShellPage :
    Page,
    IRecipient<NavigateToDetailsMessage>,
    IRecipient<NavigateBackMessage>
{
    public ShellViewModel ViewModel { get; private set; } = new ShellViewModel();

    public ShellPage()
    {
        this.InitializeComponent();

        // how we are doing navigation around
        WeakReferenceMessenger.Default.RegisterAll(this);

        RootFrame.Navigate(typeof(LoadingPage), ViewModel);
    }

    public void Receive(NavigateToDetailsMessage message)
    {
        RootFrame.Navigate(typeof(ListDetailPage), message.ListItem);
    }

    public void Receive(NavigateBackMessage message)
    {
        if (RootFrame.CanGoBack)
        {
            RootFrame.GoBack();
        }
    }
}
