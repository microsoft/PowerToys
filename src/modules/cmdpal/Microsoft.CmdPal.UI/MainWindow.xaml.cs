// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Microsoft.CmdPal.UI;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window, IRecipient<NavigateToDetailsMessage>, IRecipient<NavigateBackMessage>
{
    public MainWindow()
    {
        InitializeComponent();

        // how we are doing navigation around
        WeakReferenceMessenger.Default.RegisterAll(this);

        RootFrame.Navigate(typeof(MainPage));
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
