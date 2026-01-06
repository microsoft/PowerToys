// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class FiltersViewModel : ExtensionObjectViewModel
{
    private readonly ExtensionObject<IFilters> _filtersModel;

    public string CurrentFilterId { get; private set; } = string.Empty;

    public IFilterItemViewModel? CurrentFilter { get; private set; }

    public IFilterItemViewModel[] Filters { get; private set; } = [];

    public bool ShouldShowFilters => Filters.Length > 0;

    public FiltersViewModel(ExtensionObject<IFilters> filters, WeakReference<IPageContext> context)
        : base(context)
    {
        _filtersModel = filters;
    }

    public override void InitializeProperties()
    {
        try
        {
            if (_filtersModel.Unsafe is not null)
            {
                var filters = _filtersModel.Unsafe.GetFilters();
                var currentFilterId = _filtersModel.Unsafe.CurrentFilterId ?? string.Empty;

                var result = BuildFilters(filters ?? [], currentFilterId);
                Filters = result.Items;
                CurrentFilterId = currentFilterId;
                CurrentFilter = result.Selected;
                UpdateProperty(nameof(Filters), nameof(ShouldShowFilters), nameof(CurrentFilterId), nameof(CurrentFilter));

                return;
            }
        }
        catch (Exception ex)
        {
            ShowException(ex, _filtersModel.Unsafe?.GetType().Name);
        }

        Filters = [];
        CurrentFilterId = string.Empty;
        CurrentFilter = null;
        UpdateProperty(nameof(Filters), nameof(ShouldShowFilters), nameof(CurrentFilterId), nameof(CurrentFilter));
    }

    private (IFilterItemViewModel[] Items, IFilterItemViewModel? Selected) BuildFilters(IFilterItem[] filters, string currentFilterId)
    {
        if (filters is null || filters.Length == 0)
        {
            return ([], null);
        }

        var items = new List<IFilterItemViewModel>(filters.Length);
        FilterItemViewModel? firstFilterItem = null;
        FilterItemViewModel? selectedFilterItem = null;

        foreach (var filter in filters)
        {
            if (filter is IFilter filterItem)
            {
                var filterItemViewModel = new FilterItemViewModel(filterItem, PageContext);
                filterItemViewModel.InitializeProperties();

                if (firstFilterItem is null)
                {
                    firstFilterItem = filterItemViewModel;
                }

                if (selectedFilterItem is null && filterItemViewModel.Id == currentFilterId)
                {
                    selectedFilterItem = filterItemViewModel;
                }

                items.Add(filterItemViewModel);
            }
            else
            {
                items.Add(new SeparatorViewModel());
            }
        }

        return (items.ToArray(), selectedFilterItem ?? firstFilterItem);
    }

    public override void SafeCleanup()
    {
        base.SafeCleanup();

        foreach (var filter in Filters)
        {
            if (filter is FilterItemViewModel filterItemViewModel)
            {
                filterItemViewModel.SafeCleanup();
            }
        }

        Filters = [];
    }
}
