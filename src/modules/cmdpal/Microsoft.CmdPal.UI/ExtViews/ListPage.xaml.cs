// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
public sealed partial class ListPage : Page,
    IRecipient<NavigateNextCommand>,
    IRecipient<NavigatePreviousCommand>,
    IRecipient<ActivateSelectedListItemMessage>
{
    private readonly DispatcherQueue _queue = DispatcherQueue.GetForCurrentThread();

    public ListViewModel? ViewModel
    {
        get => (ListViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for ViewModel.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(ListViewModel), typeof(ListPage), new PropertyMetadata(null));

    public ViewModelLoadedState LoadedState
    {
        get => (ViewModelLoadedState)GetValue(LoadedStateProperty);
        set => SetValue(LoadedStateProperty, value);
    }

    // Using a DependencyProperty as the backing store for LoadedState.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty LoadedStateProperty =
        DependencyProperty.Register(nameof(LoadedState), typeof(ViewModelLoadedState), typeof(ListPage), new PropertyMetadata(ViewModelLoadedState.Loading));

    public ListPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        LoadedState = ViewModelLoadedState.Loading;
        if (e.Parameter is ListViewModel lvm)
        {
            if (!lvm.IsInitialized
                && lvm.InitializeCommand != null)
            {
                ViewModel = null;
                lvm.InitializeCommand.Execute(null);

                _ = Task.Run(async () =>
                {
                    await lvm.InitializeCommand.ExecutionTask!;

                    if (lvm.InitializeCommand.ExecutionTask.Status != TaskStatus.RanToCompletion)
                    {
                        // TODO: Handle failure case
                        System.Diagnostics.Debug.WriteLine(lvm.InitializeCommand.ExecutionTask.Exception);

                        _ = _queue.EnqueueAsync(() =>
                        {
                            LoadedState = ViewModelLoadedState.Error;
                        });
                    }
                    else
                    {
                        _ = _queue.EnqueueAsync(() =>
                        {
                            ViewModel = lvm;
                            LoadedState = ViewModelLoadedState.Loaded;
                        });
                    }
                });
            }
            else
            {
                ViewModel = lvm;
                LoadedState = ViewModelLoadedState.Loaded;
            }
        }

        // RegisterAll isn't AOT compatible
        WeakReferenceMessenger.Default.Register<NavigateNextCommand>(this);
        WeakReferenceMessenger.Default.Register<NavigatePreviousCommand>(this);
        WeakReferenceMessenger.Default.Register<ActivateSelectedListItemMessage>(this);

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);

        WeakReferenceMessenger.Default.Unregister<NavigateNextCommand>(this);
        WeakReferenceMessenger.Default.Unregister<NavigatePreviousCommand>(this);
        WeakReferenceMessenger.Default.Unregister<ActivateSelectedListItemMessage>(this);
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

    public void Receive(ActivateSelectedListItemMessage message)
    {
        if (ItemsList.SelectedItem is ListItemViewModel item)
        {
            ViewModel?.InvokeItemCommand.Execute(item);
        }
    }

    private void ItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ItemsList.SelectedItem is ListItemViewModel item)
        {
            ViewModel?.UpdateSelectedItemCommand.Execute(item);
        }
    }
}

public enum ViewModelLoadedState
{
    Loaded,
    Loading,
    Error,
}
