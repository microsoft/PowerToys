// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class FiltersViewModel : ExtensionObjectViewModel
{
    private readonly ExtensionObject<IFilters> _filtersModel;

    public string CurrentFilterId { get; private set; } = string.Empty;

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
                Filters = BuildFilters(filters ?? []);
                UpdateProperty(nameof(Filters), nameof(ShouldShowFilters));

                CurrentFilterId = _filtersModel.Unsafe.CurrentFilterId ?? string.Empty;
                UpdateProperty(nameof(CurrentFilterId));

                return;
            }
        }
        catch (Exception ex)
        {
            ShowException(ex, _filtersModel.Unsafe?.GetType().Name);
        }

        Filters = [];
        UpdateProperty(nameof(Filters), nameof(ShouldShowFilters));

        CurrentFilterId = string.Empty;
        UpdateProperty(nameof(CurrentFilterId));
    }

    private IFilterItemViewModel[] BuildFilters(IFilterItem[] filters)
    {
        return [..filters.Select<IFilterItem, IFilterItemViewModel>(filter =>
        {
            if (filter is IFilter filterItem)
            {
                var filterItemViewModel = new FilterItemViewModel(filterItem!, PageContext);
                filterItemViewModel.InitializeProperties();
                return filterItemViewModel;
            }
            else
            {
                return new SeparatorViewModel();
            }
        })];
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
