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

namespace VirtualDesktopExtension;

public partial class VirtualDesktopExtensionActionsProvider : CommandProvider
{
    public VirtualDesktopExtensionActionsProvider()
    {
        DisplayName = "Virtual Desktop Manager for Command Palette Commands";
    }

    private readonly ICommandItem[] _commands = [
        new CommandItem(new VirtualDesktopExtensionPage()),
    ];

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }
}
