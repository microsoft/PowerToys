// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ListViewModel : ObservableObject
{
    // Observable from MVVM Toolkit will auto create public properties that use INotifyPropertyChange change
    // https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/observablegroupedcollections for grouping support
    [ObservableProperty]
    public partial ObservableGroupedCollection<string, ListItemViewModel> Items { get; set; } = [];

    public ListViewModel(IListPage model)
    {
        // TEMPORARY: just plop all the items into a single group
        // see 9806fe5d8 for the last commit that had this with sections
        ObservableGroup<string, ListItemViewModel> group = new(string.Empty);

        foreach (var item in model.GetItems())
        {
            group.Add(new(item));
        }

        Items.AddGroup(group);
    }

    // InvokeItemCommand is what this will be in Xaml due to source generator
    [RelayCommand]
    private void InvokeItem(ListItemViewModel item)
    {
        // TODO: we should probably just have the shell handle a "NavigateToCommand" message
        if (item.Command is IListPage listPage)
        {
            WeakReferenceMessenger.Default.Send<NavigateToListMessage>(new(new(listPage)));
        }
        else
        {
            WeakReferenceMessenger.Default.Send<NavigateToDetailsMessage>(new(item));
        }
    }
}
