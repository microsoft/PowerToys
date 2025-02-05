// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PokedexExtension;

public partial class PokedexExtensionCommandsProvider : CommandProvider
{
    public PokedexExtensionCommandsProvider()
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
