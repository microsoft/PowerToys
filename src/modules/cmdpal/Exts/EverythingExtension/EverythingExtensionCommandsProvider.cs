// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace EverythingExtension;

public partial class EverythingExtensionActionsProvider : CommandProvider
{
    public EverythingExtensionActionsProvider()
    {
        DisplayName = "Everything extension for cmdpal";
    }

    private readonly IListItem[] _commands = [
        new ListItem(new EverythingExtensionPage())
        {
            Title = "Search Everything",
            Subtitle = "Search files with Everything",
        },
    ];

    public override IListItem[] TopLevelCommands()
    {
        return _commands;
    }
}
