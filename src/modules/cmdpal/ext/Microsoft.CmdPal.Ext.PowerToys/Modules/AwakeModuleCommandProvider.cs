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
        var module = SettingsDeepLink.SettingsWindow.Awake;
        var title = module.ModuleDisplayName();
        var icon = PowerToysResourcesHelper.IconFromSettingsIcon("Awake.png");
        var moduleIcon = module.ModuleIcon();

        items.Add(new ListItem(new OpenInSettingsCommand(module, title))
        {
            Title = title,
            Subtitle = "Open Awake settings",
            Icon = moduleIcon,
        });

        if (!ModuleEnablementService.IsModuleEnabled(module))
        {
            return items;
        }

        // Direct commands surfaced in the PowerToys list page.
        ListItem? statusItem = null;
        var refreshCommand = new RefreshAwakeStatusCommand(subtitle =>
        {
            if (statusItem is not null)
            {
                statusItem.Subtitle = subtitle;
            }
        });

        var statusNoOp = new NoOpCommand();
        statusNoOp.Name = "Awake status";

        statusItem = new ListItem(new CommandItem(statusNoOp))
        {
            Title = "Awake: Current status",
            Subtitle = AwakeStatusService.GetStatusSubtitle(),
            Icon = icon,
            MoreCommands =
            [
                new CommandContextItem(refreshCommand)
                {
                    Title = "Refresh status",
                    Subtitle = "Re-read current Awake state",
                },
            ],
        };
        items.Add(statusItem);

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
