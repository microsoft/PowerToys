// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SpongebotExtension;

internal sealed partial class SpongebotCommandsProvider : CommandProvider
{
    public SpongebotCommandsProvider()
    {
        DisplayName = "Spongebob, mocking";
        Frozen = false;
    }

    private readonly SpongebotPage mainPage = new();

    private readonly SpongebotSettingsPage settingsPage = new();

    public override ICommandItem[] TopLevelCommands()
    {
        var settingsPath = SpongebotPage.StateJsonPath();
        return !File.Exists(settingsPath)
            ? [new CommandItem(settingsPage) { Title = "Spongebot settings", Subtitle = "Enter your imgflip credentials" }]
            : [];
    }

    public override IFallbackCommandItem[] FallbackCommands()
    {
        var settingsPath = SpongebotPage.StateJsonPath();
        if (!File.Exists(settingsPath))
        {
            return null;
        }

        var listItem = new FallbackCommandItem(mainPage)
        {
            MoreCommands = [
                new CommandContextItem(mainPage.CopyCommand),
                new CommandContextItem(settingsPage),
            ],
        };
        return [listItem];
    }
}
