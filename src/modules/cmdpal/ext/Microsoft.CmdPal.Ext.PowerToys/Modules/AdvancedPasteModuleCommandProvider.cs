// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Modules;

internal sealed class AdvancedPasteModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var icon = IconHelpers.FromRelativePath("Assets\\AdvancedPaste.png");

        var item = new ListItem(new OpenAdvancedPasteCommand())
        {
            Title = "Open Advanced Paste",
            Subtitle = "Launch the Advanced Paste UI",
            Icon = icon,
        };

        return [item];
    }
}
