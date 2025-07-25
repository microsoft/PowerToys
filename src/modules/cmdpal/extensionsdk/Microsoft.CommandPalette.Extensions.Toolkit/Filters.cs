// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public abstract partial class Filters : BaseObservable, IFilters
{
    public string CurrentFilterId
    {
        get => field;
        set
        {
            field = value;
            OnPropertyChanged(nameof(CurrentFilterId));
        }
    }

    = string.Empty;

    // This method should be overridden in derived classes to provide the actual filters.
    public abstract IFilterItem[] GetFilters();
}
