// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Common.UI;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Modules;

/// <summary>
/// Provides open-settings commands for modules without specialized commands.
/// </summary>
internal sealed class DefaultSettingsModuleCommandProvider : ModuleCommandProvider
{
    private static readonly SettingsDeepLink.SettingsWindow[] _excluded =
    [
        SettingsDeepLink.SettingsWindow.Dashboard,
        SettingsDeepLink.SettingsWindow.Workspaces,
        SettingsDeepLink.SettingsWindow.Awake,
        SettingsDeepLink.SettingsWindow.ColorPicker,
    ];

    public override IEnumerable<ListItem> BuildCommands()
    {
        foreach (var module in Enum.GetValues<SettingsDeepLink.SettingsWindow>())
        {
            if (_excluded.Contains(module))
            {
                continue;
            }

            var title = module.ModuleDisplayName();
            yield return new ListItem(new OpenInSettingsCommand(module, title))
            {
                Title = title,
                Subtitle = "Open module settings",
                Icon = module.ModuleIcon(),
            };
        }
    }
}
