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
    public partial bool MultipleSelectionsEnabled { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterItems))]
    [NotifyPropertyChangedFor(nameof(ShouldShowFilters))]
    [NotifyPropertyChangedFor(nameof(SelectedFilterIcon))]
    [NotifyPropertyChangedFor(nameof(SelectedFilterName))]
    public partial IFilterItem[] Filters { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedFilterIcon))]
    [NotifyPropertyChangedFor(nameof(SelectedFilterName))]
    public partial string[] CurrentFilterIds { get; set; } = [];

    public IFilterItemViewModel[] FilterItems
    {
        get
        {
            return Filters.Select(item =>
                            {
                                if (item is IFilter filterItem)
                                {
                                    return new FilterItemViewModel(filterItem)
                                    {
                                        IsSelected = CurrentFilterIds.Any(c => c == filterItem.Id),
                                    }

                                    as IFilterItemViewModel;
                                }

                                return new SeparatorViewModel();
                            })
                            .ToArray();
        }
    }

    public bool ShouldShowFilters
    {
        get
        {
            return Filters
                        .OfType<Filter>()
                        .Any();
        }
    }

    public IconInfoViewModel? SelectedFilterIcon
    {
        get
        {
            if (CurrentFilterIds.Length == 1)
            {
                var item = FilterItems
                                .OfType<FilterItemViewModel>()
                                .FirstOrDefault(f => f.Id == CurrentFilterIds[0]);

                return item?.Icon ?? null;
            }

            return null;
        }
    }

    public string SelectedFilterName
    {
        get
        {
            if (CurrentFilterIds.Length == 1)
            {
                var item = Filters
                                .OfType<Filter>()
                                .FirstOrDefault(f => f.Id == CurrentFilterIds[0]);

                return item?.Name ?? string.Empty;
            }
            else
            {
                var selected = Filters
                                .OfType<Filter>()
                                .Where(f => CurrentFilterIds.Any(c => c == f.Id))
                                .Select(item => item.Name)
                                .ToList();
                var label = string.Join(", ", selected);

                if (label.Length > 15)
                {
                    label = $"{selected[0]} & {selected.Count - 1} more";
                }

                return label;
            }
        }
    }

    public FiltersViewModel()
    {
    }

    public FiltersViewModel(IFilters? filters)
    {
        if (filters != null)
        {
            CurrentFilterIds = filters.CurrentFilterIds;
            Filters = filters.GetFilters();
        }
    }
}
