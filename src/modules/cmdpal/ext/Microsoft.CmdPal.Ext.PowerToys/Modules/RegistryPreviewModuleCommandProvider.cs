// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using static Common.UI.SettingsDeepLink;

namespace PowerToysExtension.Modules;

internal sealed class RegistryPreviewModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var title = SettingsWindow.RegistryPreview.ModuleDisplayName();
        var icon = SettingsWindow.RegistryPreview.ModuleIcon();

        yield return new ListItem(new OpenRegistryPreviewCommand())
        {
            Title = "Open Registry Preview",
            Subtitle = "Launch Registry Preview",
            Icon = icon,
        };

        yield return new ListItem(new OpenInSettingsCommand(SettingsWindow.RegistryPreview, title))
        {
            Title = title,
            Subtitle = "Open Registry Preview settings",
            Icon = icon,
        };
    }
}
