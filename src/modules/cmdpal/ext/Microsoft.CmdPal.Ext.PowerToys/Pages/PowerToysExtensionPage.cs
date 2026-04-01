// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Awake.ModuleServices;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Properties;

namespace PowerToysExtension;

internal sealed partial class PowerToysExtensionPage : ListPage
{
    public PowerToysExtensionPage()
    {
        Icon = Helpers.PowerToysResourcesHelper.ProviderIcon();
        Title = Resources.PowerToys_DisplayName;
        Name = Resources.PowerToysExtension_CommandsName;
    }

    public override IListItem[] GetItems()
    {
        return [
            new ListItem(new LaunchModuleCommand("PowerToys", executableName: "PowerToys.exe", displayName: Resources.PowerToysExtension_OpenPowerToys_Title))
            {
                Title = Resources.PowerToysExtension_OpenPowerToys_Title,
                Subtitle = Resources.PowerToysExtension_OpenPowerToys_Subtitle,
            },
            new ListItem(new OpenPowerToysSettingsCommand("PowerToys", "General"))
            {
                Title = Resources.PowerToysExtension_OpenSettings_Title,
                Subtitle = Resources.PowerToysExtension_OpenSettings_Subtitle,
            },
            new ListItem(new OpenPowerToysSettingsCommand("Workspaces", "Workspaces"))
            {
                Title = Resources.PowerToysExtension_OpenWorkspacesSettings_Title,
                Subtitle = Resources.PowerToysExtension_OpenWorkspacesSettings_Subtitle,
            },
            new ListItem(new OpenWorkspaceEditorCommand())
            {
                Title = Resources.PowerToysExtension_OpenWorkspacesEditor_Title,
                Subtitle = Resources.PowerToysExtension_OpenWorkspacesEditor_Subtitle,
            },
        ];
    }
}
