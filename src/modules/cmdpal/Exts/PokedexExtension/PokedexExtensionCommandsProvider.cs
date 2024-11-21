// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace PokedexExtension;

public partial class PokedexExtensionActionsProvider : CommandProvider
{
    public PokedexExtensionActionsProvider()
    {
        DisplayName = "Pocket Monsters for the Command Palette";
    }

    private readonly ICommandItem[] _commands = [
        new CommandItem(new PokedexExtensionPage()) { Subtitle = "Search your favorite pocket monsters" },
    ];

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }
}
