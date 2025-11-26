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
        DisplayName = "PowerToysExtension";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        _commands = [
            new CommandItem(new PowerToysExtensionPage()) { Title = DisplayName },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }
}
