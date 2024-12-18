// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Common;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MarkdownPage : Page
{
    private readonly DispatcherQueue _queue = DispatcherQueue.GetForCurrentThread();

    public MarkdownPageViewModel? ViewModel
    {
        get => (MarkdownPageViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for ViewModel.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(MarkdownPageViewModel), typeof(FormsPage), new PropertyMetadata(null));

    public ViewModelLoadedState LoadedState
    {
        get => (ViewModelLoadedState)GetValue(LoadedStateProperty);
        set => SetValue(LoadedStateProperty, value);
    }

    // Using a DependencyProperty as the backing store for LoadedState.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty LoadedStateProperty =
        DependencyProperty.Register(nameof(LoadedState), typeof(ViewModelLoadedState), typeof(FormsPage), new PropertyMetadata(ViewModelLoadedState.Loading));

    public MarkdownPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        LoadedState = ViewModelLoadedState.Loading;
        if (e.Parameter is MarkdownPageViewModel mdpvm)
        {
            if (!mdpvm.IsInitialized
                && mdpvm.InitializeCommand != null)
            {
                ViewModel = null;

                _ = Task.Run(async () =>
                {
                    // You know, this creates the situation where we wait for
                    // both loading page properties, AND the items, before we
                    // display anything.
                    //
                    // We almost need to do an async await on initialize, then
                    // just a fire-and-forget on FetchItems.
                    mdpvm.InitializeCommand.Execute(null);

                    await mdpvm.InitializeCommand.ExecutionTask!;

                    if (mdpvm.InitializeCommand.ExecutionTask.Status != TaskStatus.RanToCompletion)
                    {
                        // TODO: Handle failure case
                        System.Diagnostics.Debug.WriteLine(mdpvm.InitializeCommand.ExecutionTask.Exception);

                        // TODO GH #239 switch back when using the new MD text block
                        // _ = _queue.EnqueueAsync(() =>
                        _queue.TryEnqueue(new(() =>
                        {
                            LoadedState = ViewModelLoadedState.Error;
                        }));
                    }
                    else
                    {
                        // TODO GH #239 switch back when using the new MD text block
                        // _ = _queue.EnqueueAsync(() =>
                        _queue.TryEnqueue(new(() =>
                        {
                            var result = (bool)mdpvm.InitializeCommand.ExecutionTask.GetResultOrDefault()!;

                            ViewModel = mdpvm;
                            WeakReferenceMessenger.Default.Send<NavigateToPageMessage>(new(result ? mdpvm : null));
                            LoadedState = result ? ViewModelLoadedState.Loaded : ViewModelLoadedState.Error;
                        }));
                    }
                });
            }
            else
            {
                ViewModel = mdpvm;
                WeakReferenceMessenger.Default.Send<NavigateToPageMessage>(new(mdpvm));
                LoadedState = ViewModelLoadedState.Loaded;
            }
        }

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e) => base.OnNavigatingFrom(e);
}
