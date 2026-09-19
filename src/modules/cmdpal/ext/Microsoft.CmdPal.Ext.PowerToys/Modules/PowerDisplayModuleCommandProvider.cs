// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using PowerToysExtension.Pages;
using PowerToysExtension.Properties;

namespace PowerToysExtension.Modules;

internal sealed class PowerDisplayModuleCommandProvider : ModuleCommandProvider
{
    private readonly IPowerDisplayCliService _cliService;

    internal PowerDisplayModuleCommandProvider()
        : this(new PowerDisplayCliService())
    {
    }

    internal PowerDisplayModuleCommandProvider(IPowerDisplayCliService cliService)
    {
        _cliService = cliService ?? throw new ArgumentNullException(nameof(cliService));
    }

    public override IEnumerable<ListItem> BuildCommands()
    {
        var icon = PowerToysResourcesHelper.IconFromSettingsIcon("PowerDisplay.png");

        if (ModuleEnablementService.IsKeyEnabled("PowerDisplay"))
        {
            yield return new ListItem(new PowerDisplayProfilesPage(_cliService))
            {
                Title = Resources.PowerDisplay_Profiles_Title,
                Subtitle = Resources.PowerDisplay_Profiles_Subtitle,
                Icon = icon,
            };
        }

        yield return new ListItem(new OpenPowerToysSettingsCommand(Resources.PowerDisplay_DisplayName, "PowerDisplay")
        {
            Id = "com.microsoft.powertoys.powerDisplay.openSettings",
        })
        {
            Title = Resources.PowerDisplay_DisplayName,
            Subtitle = Resources.PowerDisplay_Settings_Subtitle,
            Icon = icon,
        };
    }
}
