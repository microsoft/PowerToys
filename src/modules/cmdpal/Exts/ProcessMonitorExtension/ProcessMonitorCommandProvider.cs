// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ProcessMonitorExtension;

internal sealed partial class ProcessMonitorCommandProvider : CommandProvider
{
    public ProcessMonitorCommandProvider()
    {
        DisplayName = "Process Monitor Commands";
    }

    private readonly ICommandItem[] _commands = [
        new CommandItem(new ProcessListPage())
        {
            Title = "Process Manager",
            Subtitle = "Kill processes",
        },
    ];

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }
}
