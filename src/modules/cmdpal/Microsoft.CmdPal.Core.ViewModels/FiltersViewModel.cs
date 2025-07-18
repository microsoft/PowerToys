// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class FiltersViewModel : ObservableObject,
    IRecipient<UpdateFiltersMessage>,
    IRecipient<UpdateCurrentFilterIdsMessage>
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShouldShowFilters))]
    public partial IFilterItemViewModel[] Filters { get; set; } = [];

    [ObservableProperty]
    public partial string[] CurrentFilterIds { get; set; } = [];

    [ObservableProperty]
    public partial bool MultipleSelectionsEnabled { get; set; }

    public bool ShouldShowFilters => Filters is not null && Filters.Length > 0;

    public FiltersViewModel()
    {
        WeakReferenceMessenger.Default.Register<UpdateFiltersMessage>(this);
        WeakReferenceMessenger.Default.Register<UpdateCurrentFilterIdsMessage>(this);
    }

    public void Receive(UpdateFiltersMessage message)
    {
        IFilterItemViewModel[] newFilters = [];
        string[] newSelectedFilterIds = [];

        var multiSelectEnabled = false;

        if (message.Filters is not null)
        {
            var filters = message.Filters.GetFilters();

            if (message.Filters is MultiSelectFilters)
            {
                multiSelectEnabled = true;
            }

            if (filters is not null)
            {
                newFilters = filters.Select(filter =>
                                    {
                                        if (filter is IFilter filterItem)
                                        {
                                            return new FilterItemViewModel(filterItem) as IFilterItemViewModel;
                                        }
                                        else
                                        {
                                            return new SeparatorViewModel();
                                        }
                                    })
                                    .ToArray();
            }

            newSelectedFilterIds = message.Filters.CurrentFilterIds;
        }

        Filters = newFilters;
        CurrentFilterIds = newSelectedFilterIds;
        MultipleSelectionsEnabled = multiSelectEnabled;
    }

    public void Receive(UpdateCurrentFilterIdsMessage message)
    {
        CurrentFilterIds = message.CurrentFilterIds;
    }
}
