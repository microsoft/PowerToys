// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ListViewModel : ObservableObject
{
    // Observable from MVVM Toolkit will auto create public properties that use INotifyPropertyChange
    [ObservableProperty]
    private ObservableCollection<ListItemViewModel> _items = [];

    // InvokeItemCommand is what this will be in Xaml due to source generator
    [RelayCommand]
    private void InvokeItem(ListItemViewModel item)
    {
        WeakReferenceMessenger.Default.Send<NavigateToDetailsMessage>(new(item));

        Debug.WriteLine("Hello!" + item.Header);
    }
}
