// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace ProcessMonitorExtension;

internal sealed partial class ProcessMonitorCommandProvider : CommandProvider
{
    public ProcessMonitorCommandProvider()
    {
        DisplayName = "Process Monitor Commands";
    }

    private readonly IListItem[] _actions = [
        new ListItem(new ProcessListPage())
        {
            Title = "Process Manager",
            Subtitle = "Kill processes",
        },
    ];

    public override IListItem[] TopLevelCommands()
    {
        return _actions;
    }
}
