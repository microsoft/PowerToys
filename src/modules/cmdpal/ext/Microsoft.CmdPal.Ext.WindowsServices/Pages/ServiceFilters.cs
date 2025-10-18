// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WindowsServices;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ServiceFilters : Filters
{
    public ServiceFilters()
    {
        CurrentFilterId = "all";
    }

    public override IFilterItem[] GetFilters()
    {
        return [
            new Filter() { Id = "all", Name = "All Services" },
            new Separator(),
            new Filter() { Id = "running", Name = "Running", Icon = Icons.PlayIcon },
            new Filter() { Id = "stopped", Name = "Stopped", Icon = Icons.StopIcon },
            new Filter() { Id = "paused", Name = "Paused", Icon = Icons.PauseIcon },
        ];
    }
}
