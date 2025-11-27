// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension;

public sealed partial class PowerToysCommandsProvider : CommandProvider
{
    public PowerToysCommandsProvider()
    {
        DisplayName = "PowerToys";
        Icon = IconHelpers.FromRelativePath("Assets\\PowerToys.png");
    }

    public override ICommandItem[] TopLevelCommands() =>
    [
        new CommandItem(new Pages.PowerToysListPage())
        {
            Title = "PowerToys",
            Subtitle = "PowerToys commands and settings",
        }
    ];
}
