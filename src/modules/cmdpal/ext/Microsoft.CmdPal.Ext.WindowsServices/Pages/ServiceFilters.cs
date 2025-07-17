// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WindowsServices;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ServiceFilters : BaseObservable, IFilters
{
    public virtual string CurrentFilterId
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(CurrentFilterId));
        }
    }

= "all";

    public IFilterItem[] Filters()
    {
        return [
            new Filter() { Id = "all", Name = "All Services" },
            new Separator(),
            new Filter() { Id = "running", Name = "Running", Icon = Icons.GreenCircleIcon },
            new Filter() { Id = "stopped", Name = "Stopped", Icon = Icons.RedCircleIcon },
        ];
    }
}
