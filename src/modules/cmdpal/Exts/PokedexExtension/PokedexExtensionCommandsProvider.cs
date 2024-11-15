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

    private readonly IListItem[] _commands = [
        new ListItem(new PokedexExtensionPage()) { Subtitle = "Search your favorite pocket monsters" },
    ];

    public override IListItem[] TopLevelCommands()
    {
        return _commands;
    }
}
