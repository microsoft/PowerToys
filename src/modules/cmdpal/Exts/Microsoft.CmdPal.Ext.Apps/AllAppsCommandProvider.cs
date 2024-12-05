// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

public partial class AllAppsCommandProvider : CommandProvider
{
    private readonly AllAppsPage _page = new();

    private readonly CommandItem _listItem;

    public AllAppsCommandProvider()
    {
        DisplayName = "All Apps";
        _listItem = new(_page) { Subtitle = "Search installed apps" };
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return [_listItem];
    }
}
