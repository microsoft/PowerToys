// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.RaycastStore.GitHub;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RaycastStore.Pages;

internal sealed partial class InstalledExtensionListItem : ListItem
{
    public InstalledExtensionListItem(InstalledRaycastExtension extension, InstalledExtensionTracker tracker, Action? onUninstallComplete)
        : base(new NoOpCommand())
    {
        Title = extension.DisplayName;
        Subtitle = "v" + extension.Version + " — " + extension.RaycastName;
        Icon = Icons.InstalledIcon;

        Tags = new ITag[]
        {
            new Tag
            {
                Text = "Installed",
                Foreground = ColorHelpers.FromRgb(0, 128, 0),
            },
            new Tag
            {
                Text = "v" + extension.Version,
            },
        };

        RaycastExtensionInfo extInfo = new()
        {
            Name = extension.Name,
            Title = extension.DisplayName,
            DirectoryName = extension.RaycastName,
        };

        MoreCommands = new IContextItem[]
        {
            new CommandContextItem(new UninstallExtensionCommand(extInfo, tracker, onUninstallComplete))
            {
                Title = "Uninstall",
                Subtitle = "Remove " + extension.DisplayName + " from Command Palette",
            },
        };
    }
}
