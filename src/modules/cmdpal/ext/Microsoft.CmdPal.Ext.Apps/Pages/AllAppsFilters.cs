// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Pages;

/// <summary>
/// Defines the visible, application-type, and explicitly hidden projections available on the All Apps page.
/// </summary>
internal sealed partial class AllAppsFilters : Filters
{
    internal const string AllFilterId = "all";
    internal const string Win32FilterId = "win32";
    internal const string PackagedFilterId = "packaged";
    internal const string WebFilterId = "web";
    internal const string HiddenFilterId = "hidden";

    /// <summary>Initializes a new instance of the <see cref="AllAppsFilters"/> class. Initializes the application filters with All apps selected.</summary>
    public AllAppsFilters()
    {
        CurrentFilterId = AllFilterId;
    }

    /// <inheritdoc />
    public override IFilterItem[] GetFilters()
    {
        return
        [
            new Filter() { Id = AllFilterId, Name = Resources.filter_all_apps, Icon = Icons.AllAppsFilterIcon },
            new Separator(),
            new Filter() { Id = Win32FilterId, Name = Resources.filter_win32_apps, Icon = Icons.Win32AppsFilterIcon },
            new Filter() { Id = PackagedFilterId, Name = Resources.filter_packaged_apps, Icon = Icons.PackagedAppsFilterIcon },
            new Filter() { Id = WebFilterId, Name = Resources.filter_web_apps, Icon = Icons.WebAppsFilterIcon },
            new Separator(),
            new Filter() { Id = HiddenFilterId, Name = Resources.filter_hidden_apps, Icon = Icons.Hide },
        ];
    }
}
