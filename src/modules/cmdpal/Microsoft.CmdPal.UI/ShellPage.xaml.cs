// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.System;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ShellPage :
    Page,
    IRecipient<NavigateBackMessage>,
    IRecipient<PerformCommandMessage>,
    IRecipient<ShowDetailsMessage>,
    IRecipient<HideDetailsMessage>,
    IRecipient<HandleCommandResultMessage>,
    IRecipient<LaunchUriMessage>
{
    private readonly SlideNavigationTransitionInfo _slideRightTransition = new() { Effect = SlideNavigationTransitionEffect.FromRight };

    public ShellViewModel ViewModel { get; private set; } = App.Current.Services.GetService<ShellViewModel>()!;

    public ShellPage()
    {
        this.InitializeComponent();

        // how we are doing navigation around
        WeakReferenceMessenger.Default.Register<NavigateBackMessage>(this);
        WeakReferenceMessenger.Default.Register<PerformCommandMessage>(this);
        WeakReferenceMessenger.Default.Register<HandleCommandResultMessage>(this);

        WeakReferenceMessenger.Default.Register<ShowDetailsMessage>(this);
        WeakReferenceMessenger.Default.Register<HideDetailsMessage>(this);

        WeakReferenceMessenger.Default.Register<LaunchUriMessage>(this);

        RootFrame.Navigate(typeof(LoadingPage), ViewModel);
    }

    public void Receive(NavigateBackMessage message)
    {
        if (RootFrame.CanGoBack)
        {
            GoBack();
        }
        else
        {
            // If we can't go back then we must be at the top and thus escape again should quit.
            WeakReferenceMessenger.Default.Send<DismissMessage>();
        }
    }

    public void Receive(PerformCommandMessage message)
    {
        var command = message.Command.Unsafe;
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
                _ = DispatcherQueue.TryEnqueue(() =>
                {
                    // Also hide our details pane about here, if we had one
                    HideDetails();
                    var pageViewModel = new ListViewModel(listPage, TaskScheduler.FromCurrentSynchronizationContext());
                    RootFrame.Navigate(typeof(ListPage), pageViewModel, _slideRightTransition);
                    SearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                    if (command is MainListPage)
                    {
                        // todo bodgy
                        RootFrame.BackStack.Clear();
                    }

                    ViewModel.CurrentPage = pageViewModel;
                });
            }
            else if (command is IFormPage formsPage)
            {
                _ = DispatcherQueue.TryEnqueue(() =>
                {
                    // Also hide our details pane about here, if we had one
                    HideDetails();
                    var pageViewModel = new FormsPageViewModel(formsPage, TaskScheduler.FromCurrentSynchronizationContext());
                    RootFrame.Navigate(typeof(FormsPage), pageViewModel, _slideRightTransition);
                    SearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                    WeakReferenceMessenger.Default.Send<NavigateToPageMessage>(new(pageViewModel));
                });
            }
            else if (command is IMarkdownPage markdownPage)
            {
                _ = DispatcherQueue.TryEnqueue(() =>
                {
                    // Also hide our details pane about here, if we had one
                    HideDetails();
                    var pageViewModel = new MarkdownPageViewModel(markdownPage, TaskScheduler.FromCurrentSynchronizationContext());
                    RootFrame.Navigate(typeof(MarkdownPage), pageViewModel, _slideRightTransition);
                    SearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                    WeakReferenceMessenger.Default.Send<NavigateToPageMessage>(new(pageViewModel));
                });
            }
            else if (command is IInvokableCommand invokable)
            {
                // TODO Handle results
                var result = invokable.Invoke();
                HandleCommandResult(result);
            }
        }
        catch (Exception)
        {
            // TODO logging
        }
    }

    private void HandleCommandResult(ICommandResult? result)
    {
        try
        {
            if (result != null)
            {
                var kind = result.Kind;
                switch (kind)
                {
                    case CommandResultKind.Dismiss:
                        {
                            // Reset the palette to the main page and dismiss
                            GoHome();
                            WeakReferenceMessenger.Default.Send<DismissMessage>();
                            break;
                        }

                    case CommandResultKind.GoHome:
                        {
                            // Go back to the main page, but keep it open
                            GoHome();
                            break;
                        }

                    case CommandResultKind.Hide:
                        {
                            // Keep this page open, but hide the palette.
                            WeakReferenceMessenger.Default.Send<DismissMessage>();

                            break;
                        }

                    case CommandResultKind.KeepOpen:
                        {
                            // Do nothing.
                            break;
                        }
                }
            }
        }
        catch
        {
        }
    }

    public void Receive(ShowDetailsMessage message)
    {
        ViewModel.Details = message.Details;
        ViewModel.IsDetailsVisible = true;
    }

    public void Receive(HideDetailsMessage message) => HideDetails();

    public void Receive(LaunchUriMessage message) => _ = Launcher.LaunchUriAsync(message.Uri);

    public void Receive(HandleCommandResultMessage message) => HandleCommandResult(message.Result.Unsafe);

    private void HideDetails() => ViewModel.IsDetailsVisible = false;

    private void GoBack()
    {
        HideDetails();
        RootFrame.GoBack();
        RootFrame.ForwardStack.Clear();
        SearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }

    private void GoHome()
    {
        while (RootFrame.CanGoBack)
        {
            GoBack();
        }

        WeakReferenceMessenger.Default.Send<GoHomeMessage>();
    }
}
