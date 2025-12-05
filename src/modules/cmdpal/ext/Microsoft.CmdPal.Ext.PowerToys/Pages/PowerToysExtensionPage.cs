// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Awake.ModuleServices;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;

namespace PowerToysExtension;

internal sealed partial class PowerToysExtensionPage : ListPage
{
    public PowerToysExtensionPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\PowerToys.png");
        Title = "PowerToys";
        Name = "PowerToys commands";
    }

    public override IListItem[] GetItems()
    {
        return [
            new ListItem(new LaunchModuleCommand("PowerToys", executableName: "PowerToys.exe", displayName: "Open PowerToys"))
            {
                Title = "Open PowerToys",
                Subtitle = "Launch the PowerToys shell",
            },
            new ListItem(new OpenPowerToysSettingsCommand("PowerToys", "General"))
            {
                Title = "Open PowerToys settings",
                Subtitle = "Open the main PowerToys settings window",
            },
            new ListItem(new OpenPowerToysSettingsCommand("Workspaces", "Workspaces"))
            {
                Title = "Open Workspaces settings",
                Subtitle = "Jump directly to Workspaces settings",
            },
            new ListItem(new OpenWorkspaceEditorCommand())
            {
                Title = "Open Workspaces editor",
                Subtitle = "Launch the Workspaces editor",
            },
        ];
    }
}
