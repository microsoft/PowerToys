// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using static Common.UI.SettingsDeepLink;

namespace PowerToysExtension.Modules;

internal sealed class TextExtractorModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var title = SettingsWindow.PowerOCR.ModuleDisplayName();
        var icon = SettingsWindow.PowerOCR.ModuleIcon();

        yield return new ListItem(new ToggleTextExtractorCommand())
        {
            Title = "Toggle Text Extractor",
            Subtitle = "Start or close Text Extractor",
            Icon = icon,
        };

        yield return new ListItem(new OpenInSettingsCommand(SettingsWindow.PowerOCR, title))
        {
            Title = title,
            Subtitle = "Open Text Extractor settings",
            Icon = icon,
        };
    }
}
