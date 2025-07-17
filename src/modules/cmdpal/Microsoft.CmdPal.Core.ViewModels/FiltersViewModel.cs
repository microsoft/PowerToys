// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class FiltersViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string CurrentFilterId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IFilterItemViewModel[] Filters { get; set; } = [];

    public bool ShouldShowFilters
    {
        get
        {
            return Filters.Length > 0;
        }
    }

    public string SelectedFilter
    {
        get
        {
            var item = Filters
                               .OfType<FilterItemViewModel>()
                               .FirstOrDefault(f => f.Id == CurrentFilterId);

            return item?.Name ?? string.Empty;
        }
    }

    public FiltersViewModel()
    {
    }

    public FiltersViewModel(IFilters? filters)
    {
        if (filters != null)
        {
            CurrentFilterId = filters.CurrentFilterId;
            Filters = filters.Filters()
                                .Select(item =>
                                {
                                    if (item is IFilter filterItem)
                                    {
                                        return new FilterItemViewModel(filterItem)
                                        {
                                            IsSelected = CurrentFilterId == filterItem.Id,
                                        }

                                        as IFilterItemViewModel;
                                    }

                                    return new SeparatorViewModel();
                                })
                                .ToArray();
        }
    }
}
