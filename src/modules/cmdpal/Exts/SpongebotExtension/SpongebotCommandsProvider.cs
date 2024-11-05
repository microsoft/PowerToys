// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace SpongebotExtension;

internal sealed partial class SpongebotCommandsProvider : CommandProvider
{
    public SpongebotCommandsProvider()
    {
        DisplayName = "Spongebob, mocking";
    }

    private readonly SpongebotPage mainPage = new();

    private readonly SpongebotSettingsPage settingsPage = new();

    public override IListItem[] TopLevelCommands()
    {
        var settingsPath = SpongebotPage.StateJsonPath();
        if (!File.Exists(settingsPath))
        {
            return [new ListItem(settingsPage) { Title = "Spongebot settings", Subtitle = "Enter your imgflip credentials" }];
        }

        var listItem = new ListItem(mainPage)
        {
            MoreCommands = [
                new CommandContextItem(mainPage.CopyTextAction),
                new CommandContextItem(settingsPage),
            ],
        };
        return [listItem];
    }
}
