// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ShellPage :
    Page,
    IRecipient<NavigateBackMessage>,
    IRecipient<NavigateToDetailsMessage>,
    IRecipient<NavigateToListMessage>,
    IRecipient<PerformCommandMessage>
{
    private readonly DrillInNavigationTransitionInfo _drillInNavigationTransitionInfo = new();

    private readonly SlideNavigationTransitionInfo _slideRightTransition = new() { Effect = SlideNavigationTransitionEffect.FromRight };

    public ShellViewModel ViewModel { get; private set; } = App.Current.Services.GetService<ShellViewModel>()!;

    public ShellPage()
    {
        this.InitializeComponent();

        // how we are doing navigation around
        WeakReferenceMessenger.Default.Register<NavigateBackMessage>(this);
        WeakReferenceMessenger.Default.Register<NavigateToDetailsMessage>(this);
        WeakReferenceMessenger.Default.Register<NavigateToListMessage>(this);
        WeakReferenceMessenger.Default.Register<PerformCommandMessage>(this);

        RootFrame.Navigate(typeof(LoadingPage), ViewModel);
    }

    public void Receive(NavigateToDetailsMessage message) => RootFrame.Navigate(typeof(ListDetailPage), message.ListItem, _drillInNavigationTransitionInfo);

    public void Receive(NavigateBackMessage message)
    {
        if (RootFrame.CanGoBack)
        {
            RootFrame.GoBack();
        }
    }

    public void Receive(NavigateToListMessage message)
    {
        RootFrame.Navigate(typeof(ListPage), message.ViewModel, _slideRightTransition);
        SearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }

    public void Receive(PerformCommandMessage message)
    {
        var command = message.Command;
        if (command == null)
        {
            return;
        }

        // TODO: Actually loading up the page, or invoking the command -
        // that might belong in the model, not the view?
        // Especially considering the try/catch concerns around the fact that the
        // COM call might just fail.
        // Or the command may be a stub. Future us problem.
        try
        {
            if (command is IListPage listPage)
            {
                var pageViewModel = new ListViewModel(listPage);
                RootFrame.Navigate(typeof(ListPage), pageViewModel, _slideRightTransition);
                SearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            }

            // else if markdown, forms, TODO
            else if (command is IInvokableCommand invokable)
            {
                invokable.Invoke();
            }
        }
        catch (Exception)
        {
            // TODO logging
        }
    }
}
