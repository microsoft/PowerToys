// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Common.UI;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using PowerToysExtension.Properties;
using Workspaces.ModuleServices;
using WorkspacesCsharpLibrary.Data;

namespace PowerToysExtension.Modules;

internal sealed class WorkspacesModuleCommandProvider : ModuleCommandProvider
{
    public override IEnumerable<ListItem> BuildCommands()
    {
        var items = new List<ListItem>();
        var module = SettingsDeepLink.SettingsWindow.Workspaces;
        var title = module.ModuleDisplayName();
        var icon = PowerToysResourcesHelper.IconFromSettingsIcon("Workspaces.png");
        var moduleIcon = module.ModuleIcon();

        items.Add(new ListItem(new OpenInSettingsCommand(module, title))
        {
            Title = title,
            Subtitle = Resources.Workspaces_Settings_Subtitle,
            Icon = moduleIcon,
        });

        if (!ModuleEnablementService.IsModuleEnabled(module))
        {
            return items;
        }

        // Settings entry plus common actions.
        items.Add(new ListItem(new OpenWorkspaceEditorCommand())
        {
            Title = Resources.Workspaces_OpenEditor_Title,
            Subtitle = Resources.Workspaces_OpenEditor_Subtitle,
            Icon = icon,
        });

        // Per-workspace entries via the shared service.
        foreach (var workspace in LoadWorkspaces())
        {
            if (string.IsNullOrWhiteSpace(workspace.Id) || string.IsNullOrWhiteSpace(workspace.Name))
            {
                continue;
            }

            items.Add(new WorkspaceListItem(workspace, icon));
        }

        return items;
    }

    private static IReadOnlyList<ProjectWrapper> LoadWorkspaces()
    {
        var result = WorkspaceService.Instance.GetWorkspacesAsync().GetAwaiter().GetResult();
        return result.Success && result.Value is not null ? result.Value : System.Array.Empty<ProjectWrapper>();
    }
}
