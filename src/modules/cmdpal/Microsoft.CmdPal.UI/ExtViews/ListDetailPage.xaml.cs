// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ListDetailPage : Page
{
    public ListItemViewModel? ViewModel { get; set; }

    public ListDetailPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = (ListItemViewModel)e.Parameter;

        base.OnNavigatedTo(e);
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send<NavigateBackMessage>();
    }
}
