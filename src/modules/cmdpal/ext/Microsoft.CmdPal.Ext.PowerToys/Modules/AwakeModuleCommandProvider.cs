// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Awake.ModuleServices;
using Common.UI;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using PowerToysExtension.Pages;

namespace PowerToysExtension.Modules;

internal sealed class AwakeModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var items = new List<ListItem>();
        var icon = IconHelpers.FromRelativePath("Assets\\Awake.png");

        // Settings entry with quick actions in MoreCommands.
        var settingsTitle = SettingsDeepLink.SettingsWindow.Awake.ModuleDisplayName();
        items.Add(new ListItem(new OpenInSettingsCommand(SettingsDeepLink.SettingsWindow.Awake, settingsTitle))
        {
            Title = settingsTitle,
            Subtitle = "Open Awake settings",
            Icon = SettingsDeepLink.SettingsWindow.Awake.ModuleIcon(),
            MoreCommands =
            [
                new CommandContextItem(new StartAwakeCommand("Awake: Keep awake indefinitely", () => AwakeService.Instance.SetIndefiniteAsync(), "Awake set to indefinite")),
                new CommandContextItem(new StartAwakeCommand("Awake: Keep awake for 30 minutes", () => AwakeService.Instance.SetTimedAsync(30), "Awake set for 30 minutes")),
                new CommandContextItem(new StartAwakeCommand("Awake: Keep awake for 1 hour", () => AwakeService.Instance.SetTimedAsync(60), "Awake set for 1 hour")),
                new CommandContextItem(new StartAwakeCommand("Awake: Keep awake for 2 hours", () => AwakeService.Instance.SetTimedAsync(120), "Awake set for 2 hours")),
                new CommandContextItem(new StopAwakeCommand()),
                new CommandContextItem(new AwakeProcessListPage()),
            ],
        });

        // Direct commands surfaced in the PowerToys list page.
        items.Add(new ListItem(new StartAwakeCommand("Awake: Keep awake indefinitely", () => AwakeService.Instance.SetIndefiniteAsync(), "Awake set to indefinite"))
        {
            Title = "Awake: Keep awake indefinitely",
            Subtitle = "Run Awake in indefinite mode",
            Icon = icon,
        });
        items.Add(new ListItem(new StartAwakeCommand("Awake: Keep awake for 30 minutes", () => AwakeService.Instance.SetTimedAsync(30), "Awake set for 30 minutes"))
        {
            Title = "Awake: Keep awake for 30 minutes",
            Subtitle = "Run Awake timed for 30 minutes",
            Icon = icon,
        });
        items.Add(new ListItem(new StartAwakeCommand("Awake: Keep awake for 1 hour", () => AwakeService.Instance.SetTimedAsync(60), "Awake set for 1 hour"))
        {
            Title = "Awake: Keep awake for 1 hour",
            Subtitle = "Run Awake timed for 1 hour",
            Icon = icon,
        });
        items.Add(new ListItem(new StartAwakeCommand("Awake: Keep awake for 2 hours", () => AwakeService.Instance.SetTimedAsync(120), "Awake set for 2 hours"))
        {
            Title = "Awake: Keep awake for 2 hours",
            Subtitle = "Run Awake timed for 2 hours",
            Icon = icon,
        });
        items.Add(new ListItem(new StopAwakeCommand())
        {
            Title = "Awake: Turn off",
            Subtitle = "Switch Awake back to Off",
            Icon = icon,
        });
        items.Add(new ListItem(new CommandItem(new AwakeProcessListPage()))
        {
            Title = "Bind Awake to another process",
            Subtitle = "Stop automatically when the target process exits",
            Icon = icon,
        });

        return items;
    }
}
