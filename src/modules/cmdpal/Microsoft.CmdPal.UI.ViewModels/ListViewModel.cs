// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ListViewModel : ObservableObject
{
    // Observable from MVVM Toolkit will auto create public properties that use INotifyPropertyChange change
    // https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/observablegroupedcollections for grouping support
    [ObservableProperty]
    public partial ObservableGroupedCollection<string, ListItemViewModel> Items { get; set; } = [];

    [ObservableProperty]
    public partial bool IsInitialized { get; private set; }

    private readonly ExtensionObject<IListPage> _model;

    public ListViewModel(IListPage model)
    {
        _model = new(model);
    }

    private void Model_ItemsChanged(object sender, ItemsChangedEventArgs args) => FetchItems();

    //// Run on background thread, from InitializeAsync or Model_ItemsChanged
    private void FetchItems()
    {
        ObservableGroup<string, ListItemViewModel> group = new(string.Empty);

        // TEMPORARY: just plop all the items into a single group
        // see 9806fe5d8 for the last commit that had this with sections
        // TODO unsafe
        var newItems = _model.Unsafe!.GetItems();

        Items.Clear();

        foreach (var item in newItems)
        {
            ListItemViewModel viewModel = new(item);
            viewModel.InitializeProperties();
            group.Add(viewModel);
        }

        // Am I really allowed to modify that observable collection on a BG
        // thread and have it just work in the UI??
        Items.AddGroup(group);
    }

    //// Run on background thread from ListPage.xaml.cs
    [RelayCommand]
    private Task<bool> InitializeAsync()
    {
        // TODO: We may want a SemaphoreSlim lock here.

        // TODO: We may want to investigate using some sort of AsyncEnumerable or populating these as they come in to the UI layer
        //       Though we have to think about threading here and circling back to the UI thread with a TaskScheduler.
        FetchItems();

        _model.Unsafe!.ItemsChanged += Model_ItemsChanged;

        IsInitialized = true;

        return Task.FromResult(true);
    }

    // InvokeItemCommand is what this will be in Xaml due to source generator
    [RelayCommand]
    private void InvokeItem(ListItemViewModel item) => WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(item.Command));

    [RelayCommand]
    private void UpdateSelectedItem(ListItemViewModel item) => WeakReferenceMessenger.Default.Send<UpdateActionBarMessage>(new(item));
}
