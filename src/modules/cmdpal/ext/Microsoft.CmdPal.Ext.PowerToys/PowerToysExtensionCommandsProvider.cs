// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension;

public partial class PowerToysExtensionCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;

    public PowerToysExtensionCommandsProvider()
    {
        DisplayName = "PowerToys";
        Icon = IconHelpers.FromRelativePath("Assets\\PowerToys.png");
        _commands = [
            new CommandItem(new Pages.PowerToysListPage())
            {
                Title = "PowerToys",
                Subtitle = "PowerToys commands and settings",
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }
}
