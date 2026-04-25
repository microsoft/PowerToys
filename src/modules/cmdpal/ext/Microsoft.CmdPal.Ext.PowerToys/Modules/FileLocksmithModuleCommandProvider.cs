// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using PowerToysExtension.Properties;
using static Common.UI.SettingsDeepLink;

namespace PowerToysExtension.Modules;

internal sealed class FileLocksmithModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var title = SettingsWindow.FileLocksmith.ModuleDisplayName();
        var icon = SettingsWindow.FileLocksmith.ModuleIcon();

        yield return new ListItem(new OpenInSettingsCommand(SettingsWindow.FileLocksmith, title) { Id = "com.microsoft.powertoys.fileLocksmith.openSettings" })
        {
            Title = title,
            Subtitle = Resources.FileLocksmith_Settings_Subtitle,
            Icon = icon,
        };
    }
}
