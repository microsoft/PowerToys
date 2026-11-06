// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CompatibilityExtension;

internal sealed partial class SearchFilters : BaseObservable, IFilters
{
    private string _currentFilterId = "all";

    public string CurrentFilterId
    {
        get => _currentFilterId;
        set
        {
            if (_currentFilterId != value)
            {
                _currentFilterId = value;
                OnPropertyChanged(nameof(CurrentFilterId));
            }
        }
    }

    // SDK 0.1 and 0.2 called this interface method Filters.
    public IFilterItem[] Filters() => GetFilters();

    public IFilterItem[] GetFilters() =>
    [
        new Filter { Id = "all", Name = "All" },
        new Filter { Id = "fruit", Name = "Fruit" },
        new Filter { Id = "vegetable", Name = "Vegetables" },
    ];
}
