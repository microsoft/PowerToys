// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Common;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
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
public sealed partial class FormsPage : Page
{
    private readonly DispatcherQueue _queue = DispatcherQueue.GetForCurrentThread();

    public FormsPageViewModel? ViewModel
    {
        get => (FormsPageViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for ViewModel.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(FormsPageViewModel), typeof(FormsPage), new PropertyMetadata(null));

    public ViewModelLoadedState LoadedState
    {
        get => (ViewModelLoadedState)GetValue(LoadedStateProperty);
        set => SetValue(LoadedStateProperty, value);
    }

    // Using a DependencyProperty as the backing store for LoadedState.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty LoadedStateProperty =
        DependencyProperty.Register(nameof(LoadedState), typeof(ViewModelLoadedState), typeof(FormsPage), new PropertyMetadata(ViewModelLoadedState.Loading));

    public FormsPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        LoadedState = ViewModelLoadedState.Loading;
        if (e.Parameter is FormsPageViewModel fpvm)
        {
            if (!fpvm.IsInitialized
                && fpvm.InitializeCommand != null)
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
                    fpvm.InitializeCommand.Execute(null);

                    await fpvm.InitializeCommand.ExecutionTask!;

                    if (fpvm.InitializeCommand.ExecutionTask.Status != TaskStatus.RanToCompletion)
                    {
                        // TODO: Handle failure case
                        System.Diagnostics.Debug.WriteLine(fpvm.InitializeCommand.ExecutionTask.Exception);

                        _ = _queue.EnqueueAsync(() =>
                        {
                            LoadedState = ViewModelLoadedState.Error;
                        });
                    }
                    else
                    {
                        _ = _queue.EnqueueAsync(() =>
                        {
                            var result = (bool)fpvm.InitializeCommand.ExecutionTask.GetResultOrDefault()!;

                            ViewModel = fpvm;
                            WeakReferenceMessenger.Default.Send<NavigateToPageMessage>(new(result ? fpvm : null));
                            LoadedState = result ? ViewModelLoadedState.Loaded : ViewModelLoadedState.Error;
                        });
                    }
                });
            }
            else
            {
                ViewModel = fpvm;
                WeakReferenceMessenger.Default.Send<NavigateToPageMessage>(new(fpvm));
                LoadedState = ViewModelLoadedState.Loaded;
            }
        }

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e) => base.OnNavigatingFrom(e);
}
