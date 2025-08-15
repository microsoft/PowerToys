// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class FiltersViewModel : ExtensionObjectViewModel
{
    private readonly ExtensionObject<IFilters> _filtersModel = new(null);

    [ObservableProperty]
    public partial string CurrentFilterId { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShouldShowFilters))]
    public partial IFilterItemViewModel[] Filters { get; set; } = [];

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
                Filters = filters.Select<IFilterItem, IFilterItemViewModel>(filter =>
                {
                    var filterItem = filter as IFilter;
                    if (filterItem != null)
                    {
                        var filterVM = new FilterItemViewModel(filterItem!, PageContext);
                        filterVM.InitializeProperties();

                        return filterVM;
                    }
                    else
                    {
                        return new SeparatorViewModel();
                    }
                }).ToArray();

                CurrentFilterId = _filtersModel.Unsafe.CurrentFilterId;

                return;
            }
        }
        catch (Exception ex)
        {
            ShowException(ex, _filtersModel.Unsafe?.GetType().Name);
        }

        Filters = [];
        CurrentFilterId = string.Empty;
    }

    public override void SafeCleanup()
    {
        base.SafeCleanup();

        foreach (var filter in Filters)
        {
            if (filter is FilterItemViewModel filterVM)
            {
                filterVM.SafeCleanup();
            }
        }

        Filters = [];
    }
}
