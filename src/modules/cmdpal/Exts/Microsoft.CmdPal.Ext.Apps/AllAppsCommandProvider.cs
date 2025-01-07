// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

public partial class AllAppsCommandProvider : CommandProvider
{
    public static readonly AllAppsPage Page = new();

    private readonly CommandItem _listItem;

    public AllAppsCommandProvider()
    {
        Id = "AllApps";
        DisplayName = "Installed apps";
        Icon = new("\ue71d");

        _listItem = new(Page) { Subtitle = "Search installed apps" };
    }

    public override ICommandItem[] TopLevelCommands() => [_listItem];
}
