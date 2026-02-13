// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WindowsServices;
using Microsoft.CmdPal.Ext.WindowsServices.Properties;

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
            new Filter() { Id = "all", Name = Resources.Filters_All_Name },
            new Separator(),
            new Filter() { Id = "running", Name = Resources.Filters_Running_Name, Icon = Icons.PlayIcon },
            new Filter() { Id = "stopped", Name = Resources.Filters_Stopped_Name, Icon = Icons.StopIcon },
            new Filter() { Id = "paused", Name = Resources.Filters_Paused_Name, Icon = Icons.PauseIcon },
        ];
    }
}
