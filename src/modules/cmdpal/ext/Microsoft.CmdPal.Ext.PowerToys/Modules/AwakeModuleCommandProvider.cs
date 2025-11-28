// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Awake.ModuleServices;
using Common.UI;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Modules;

internal sealed class AwakeModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var title = SettingsDeepLink.SettingsWindow.Awake.ModuleDisplayName();
        var icon = SettingsDeepLink.SettingsWindow.Awake.ModuleIcon();

        var more = new List<CommandContextItem>
        {
            new CommandContextItem(new StartAwakeCommand("Awake: Keep awake indefinitely", () => AwakeService.Instance.SetIndefiniteAsync(), "Awake set to indefinite")),
            new CommandContextItem(new StartAwakeCommand("Awake: Keep awake for 30 minutes", () => AwakeService.Instance.SetTimedAsync(30), "Awake set for 30 minutes")),
            new CommandContextItem(new StartAwakeCommand("Awake: Keep awake for 2 hours", () => AwakeService.Instance.SetTimedAsync(120), "Awake set for 2 hours")),
            new CommandContextItem(new StopAwakeCommand()),
        };

        var item = new ListItem(new OpenInSettingsCommand(SettingsDeepLink.SettingsWindow.Awake, title))
        {
            Title = title,
            Subtitle = "Open Awake settings",
            Icon = icon,
            MoreCommands = more.ToArray(),
        };

        return [item];
    }
}
