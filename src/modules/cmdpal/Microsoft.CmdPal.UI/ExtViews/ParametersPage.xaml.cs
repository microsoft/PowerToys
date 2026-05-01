// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class ParametersPage : Page,
    IRecipient<NavigateNextCommand>,
    IRecipient<NavigatePreviousCommand>,
    IRecipient<NavigatePageDownCommand>,
    IRecipient<NavigatePageUpCommand>,
    IRecipient<ActivateSelectedListItemMessage>
{
    private readonly DispatcherQueue _queue = DispatcherQueue.GetForCurrentThread();

    public ParametersPageViewModel? ViewModel
    {
        get => (ParametersPageViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for ViewModel.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(ParametersPageViewModel), typeof(ParametersPage), new PropertyMetadata(null, OnViewModelChanged));

    public ParametersPage()
    {
        this.InitializeComponent();
        this.Unloaded += OnUnloaded;

        WeakReferenceMessenger.Default.Register<NavigateNextCommand>(this);
        WeakReferenceMessenger.Default.Register<NavigatePreviousCommand>(this);
        WeakReferenceMessenger.Default.Register<NavigatePageDownCommand>(this);
        WeakReferenceMessenger.Default.Register<NavigatePageUpCommand>(this);
        WeakReferenceMessenger.Default.Register<ActivateSelectedListItemMessage>(this);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Unhook from everything to ensure nothing can reach us
        // between this point and our complete and utter destruction.
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is not AsyncNavigationRequest navigationRequest)
        {
            throw new InvalidOperationException($"Invalid navigation parameter: {nameof(e.Parameter)} must be {nameof(AsyncNavigationRequest)}");
        }

        if (navigationRequest.TargetViewModel is not ParametersPageViewModel page)
        {
            throw new InvalidOperationException($"Invalid navigation target: AsyncNavigationRequest.{nameof(AsyncNavigationRequest.TargetViewModel)} must be {nameof(ParametersPageViewModel)}");
        }

        ViewModel = page;

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        base.OnNavigatingFrom(e);

        // Clean-up event listeners
        ViewModel = null;
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ParametersPage @this)
        {
            if (e.OldValue is ParametersPageViewModel old)
            {
                old.PropertyChanged -= @this.ViewModel_PropertyChanged;
            }

            if (e.NewValue is ParametersPageViewModel page)
            {
                page.PropertyChanged += @this.ViewModel_PropertyChanged;
            }
            else if (e.NewValue is null)
            {
                CoreLogger.LogDebug("cleared view model");
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var prop = e.PropertyName;
        if (prop == nameof(ViewModel.ShowCommand))
        {
            Debug.WriteLine($"ViewModel.ShowCommand {ViewModel?.ShowCommand}");
        }
        else if (prop == nameof(ViewModel.ActiveListViewModel))
        {
            if (ViewModel?.HasActiveList == true)
            {
                SelectFirstItem();
            }
        }
    }

    private void ParamItems_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ListItemViewModel item)
        {
            ViewModel?.ActiveListViewModel?.InvokeItemCommand.Execute(item);
        }
    }

    public void Receive(NavigateNextCommand message) => NavigateList(1);

    public void Receive(NavigatePreviousCommand message) => NavigateList(-1);

    public void Receive(NavigatePageDownCommand message) => NavigateList(10);

    public void Receive(NavigatePageUpCommand message) => NavigateList(-10);

    public void Receive(ActivateSelectedListItemMessage message)
    {
        if (ViewModel?.HasActiveList != true)
        {
            return;
        }

        if (ParamItemsList.SelectedItem is ListItemViewModel item)
        {
            ViewModel.ActiveListViewModel?.InvokeItemCommand.Execute(item);
        }
        else if (ViewModel.ActiveListViewModel?.FilteredItems.Count > 0 &&
                 ViewModel.ActiveListViewModel.FilteredItems[0] is ListItemViewModel firstItem)
        {
            ViewModel.ActiveListViewModel.InvokeItemCommand.Execute(firstItem);
        }
    }

    private void NavigateList(int delta)
    {
        if (ViewModel?.HasActiveList != true)
        {
            return;
        }

        var list = ParamItemsList;
        var count = list.Items.Count;
        if (count == 0)
        {
            return;
        }

        var current = list.SelectedIndex;
        var target = Math.Clamp(current + delta, 0, count - 1);
        list.SelectedIndex = target;
        list.ScrollIntoView(list.SelectedItem);
    }

    public void SelectFirstItem()
    {
        // Use TryEnqueue so the ListView has had time to populate from the binding
        _queue.TryEnqueue(() =>
        {
            if (ParamItemsList.Items.Count > 0)
            {
                ParamItemsList.SelectedIndex = 0;
                ParamItemsList.ScrollIntoView(ParamItemsList.SelectedItem);
            }
        });
    }
}
