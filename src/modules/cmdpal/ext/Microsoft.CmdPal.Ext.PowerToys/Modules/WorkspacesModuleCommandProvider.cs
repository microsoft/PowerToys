// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Common.UI;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
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
            Subtitle = "Open Workspaces settings",
            Icon = moduleIcon,
        });

        if (!ModuleEnablementService.IsModuleEnabled(module))
        {
            return items;
        }

        // Settings entry plus common actions.
        items.Add(new ListItem(new OpenWorkspaceEditorCommand())
        {
            Title = "Workspaces: Open editor",
            Subtitle = "Create or edit workspaces",
            Icon = icon,
        });

        // Per-workspace entries via the shared service.
        foreach (var workspace in LoadWorkspaces())
        {
            if (string.IsNullOrWhiteSpace(workspace.Id) || string.IsNullOrWhiteSpace(workspace.Name))
            {
                continue;
            }

            items.Add(new ListItem(new LaunchWorkspaceCommand(workspace.Id))
            {
                Title = workspace.Name,
                Subtitle = BuildSubtitle(workspace),
                Icon = icon,
                Details = BuildDetails(workspace),
            });
        }

        return items;
    }

    private static IReadOnlyList<ProjectWrapper> LoadWorkspaces()
    {
        var result = WorkspaceService.Instance.GetWorkspacesAsync().GetAwaiter().GetResult();
        return result.Success && result.Value is not null ? result.Value : System.Array.Empty<ProjectWrapper>();
    }

    private static string BuildSubtitle(ProjectWrapper workspace)
    {
        var appCount = workspace.Applications?.Count ?? 0;
        var monitorCount = workspace.MonitorConfiguration?.Count ?? 0;
        var appsText = appCount switch
        {
            0 => "No applications",
            1 => "1 application",
            _ => string.Format(CultureInfo.CurrentCulture, "{0} applications", appCount),
        };

        var monitorsText = monitorCount switch
        {
            0 => "No monitors",
            1 => "1 monitor",
            _ => string.Format(CultureInfo.CurrentCulture, "{0} monitors", monitorCount),
        };

        var lastLaunched = workspace.LastLaunchedTime > 0
            ? $"Last launched {FormatRelativeTime(workspace.LastLaunchedTime)}"
            : "Never launched";

        return $"{appsText} • {monitorsText} • {lastLaunched}";
    }

    private static Details BuildDetails(ProjectWrapper workspace)
    {
        var appCount = workspace.Applications?.Count ?? 0;
        var monitorCount = workspace.MonitorConfiguration?.Count ?? 0;
        var lastLaunched = workspace.LastLaunchedTime > 0
            ? FormatRelativeTime(workspace.LastLaunchedTime)
            : "Never launched";

        return new Details
        {
            HeroImage = PowerToysResourcesHelper.IconFromSettingsIcon("Workspaces.png"),
            Title = workspace.Name,
            Metadata = BuildAppMetadata(workspace),
        };
    }

    private static IDetailsElement[] BuildAppMetadata(ProjectWrapper workspace)
    {
        if (workspace.Applications is null || workspace.Applications.Count == 0)
        {
            return Array.Empty<IDetailsElement>();
        }

        var elements = new List<IDetailsElement>();
        foreach (var app in workspace.Applications)
        {
            var tags = new List<ITag>();

            if (!string.IsNullOrWhiteSpace(app.ApplicationPath))
            {
                tags.Add(new Tag(app.ApplicationPath));
            }

            tags.Add(new Tag(string.IsNullOrWhiteSpace(app.Application) ? "App" : app.Application));

            if (app.Monitor > 0)
            {
                tags.Add(new Tag($"Monitor {app.Monitor}"));
            }

            elements.Add(new DetailsElement
            {
                Key = string.IsNullOrWhiteSpace(app.Title) ? (app.Application ?? "Application") : app.Title,
                Data = new DetailsTags { Tags = tags.ToArray() },
            });
        }

        return elements.ToArray();
    }

    private static string FormatRelativeTime(long unixSeconds)
    {
        var lastLaunch = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        var delta = DateTime.UtcNow - lastLaunch;

        if (delta.TotalMinutes < 1)
        {
            return "just now";
        }

        if (delta.TotalMinutes < 60)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0} min ago", (int)delta.TotalMinutes);
        }

        if (delta.TotalHours < 24)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0} hr ago", (int)delta.TotalHours);
        }

        return string.Format(CultureInfo.CurrentCulture, "{0} days ago", (int)delta.TotalDays);
    }
}
