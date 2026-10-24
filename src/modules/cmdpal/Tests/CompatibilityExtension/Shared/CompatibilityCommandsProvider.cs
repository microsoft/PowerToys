// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CompatibilityExtension;

internal sealed partial class CompatibilityCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;

    public CompatibilityCommandsProvider()
    {
        Id = $"CmdPal.Compatibility.{Baseline.SdkVersion}";
        DisplayName = $"Compatibility SDK {Baseline.SdkVersion}";
        Icon = new IconInfo("\uE9D9");
        _commands = [new CommandItem(new HomePage()) { Title = DisplayName, Subtitle = "Published SDK UI regression fixtures" }];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
}
