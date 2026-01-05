// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Awake.ModuleServices;
using Common.UI;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using PowerToysExtension.Pages;
using PowerToysExtension.Properties;

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
            Subtitle = Resources.Awake_Settings_Subtitle,
            Icon = moduleIcon,
        });

        if (!ModuleEnablementService.IsModuleEnabled(module))
        {
            return items;
        }

        // Direct commands surfaced in the PowerToys list page.
        ListItem? statusItem = null;
        Action refreshStatus = () =>
        {
            if (statusItem is not null)
            {
                statusItem.Subtitle = AwakeStatusService.GetStatusSubtitle();
            }
        };

        var refreshCommand = new RefreshAwakeStatusCommand(refreshStatus);

        statusItem = new ListItem(new CommandItem(refreshCommand))
        {
            Title = Resources.Awake_Status_Title,
            Subtitle = AwakeStatusService.GetStatusSubtitle(),
            Icon = icon,
        };
        items.Add(statusItem);

        items.Add(new ListItem(new StartAwakeCommand(Resources.Awake_KeepIndefinite_Title, () => AwakeService.Instance.SetIndefiniteAsync(), Resources.Awake_SetIndefinite_Toast, refreshStatus))
        {
            Title = Resources.Awake_KeepIndefinite_Title,
            Subtitle = Resources.Awake_KeepIndefinite_Subtitle,
            Icon = icon,
        });
        items.Add(new ListItem(new StartAwakeCommand(Resources.Awake_Keep30Min_Title, () => AwakeService.Instance.SetTimedAsync(30), Resources.Awake_Set30Min_Toast, refreshStatus))
        {
            Title = Resources.Awake_Keep30Min_Title,
            Subtitle = Resources.Awake_Keep30Min_Subtitle,
            Icon = icon,
        });
        items.Add(new ListItem(new StartAwakeCommand(Resources.Awake_Keep1Hour_Title, () => AwakeService.Instance.SetTimedAsync(60), Resources.Awake_Set1Hour_Toast, refreshStatus))
        {
            Title = Resources.Awake_Keep1Hour_Title,
            Subtitle = Resources.Awake_Keep1Hour_Subtitle,
            Icon = icon,
        });
        items.Add(new ListItem(new StartAwakeCommand(Resources.Awake_Keep2Hours_Title, () => AwakeService.Instance.SetTimedAsync(120), Resources.Awake_Set2Hours_Toast, refreshStatus))
        {
            Title = Resources.Awake_Keep2Hours_Title,
            Subtitle = Resources.Awake_Keep2Hours_Subtitle,
            Icon = icon,
        });
        items.Add(new ListItem(new StopAwakeCommand(refreshStatus))
        {
            Title = Resources.Awake_TurnOff_Title,
            Subtitle = Resources.Awake_TurnOff_Subtitle,
            Icon = icon,
        });

        return items;
    }
}
