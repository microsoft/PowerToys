// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;
using WorkspacesCsharpLibrary.Data;

namespace PowerToysExtension.Commands;

internal sealed partial class WorkspaceListItem : ListItem
{
    public WorkspaceListItem(ProjectWrapper workspace, IconInfo icon)
        : base(new LaunchWorkspaceCommand(workspace.Id))
    {
        Title = workspace.Name;
        Subtitle = BuildSubtitle(workspace);
        Icon = icon;
        Details = BuildDetails(workspace, icon);
    }

    private static string BuildSubtitle(ProjectWrapper workspace)
    {
        var appCount = workspace.Applications?.Count ?? 0;
        var appsText = appCount switch
        {
            0 => "No applications",
            _ => string.Format(CultureInfo.CurrentCulture, "{0} applications", appCount),
        };

        var lastLaunched = workspace.LastLaunchedTime > 0
            ? $"Last launched {FormatRelativeTime(workspace.LastLaunchedTime)}"
            : "Never launched";

        return $"{appsText} \u2022 {lastLaunched}";
    }

    private static Details BuildDetails(ProjectWrapper workspace, IconInfo icon)
    {
        var appCount = workspace.Applications?.Count ?? 0;
        var body = appCount switch
        {
            0 => "No applications in this workspace",
            1 => "1 application",
            _ => $"{appCount} applications",
        };

        return new Details
        {
            HeroImage = icon,
            Title = workspace.Name ?? "Workspace",
            Body = body,
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
            var appName = string.IsNullOrWhiteSpace(app.Application) ? "App" : app.Application;
            var title = string.IsNullOrWhiteSpace(app.Title) ? appName : app.Title;

            var tags = new List<ITag>();

            if (!string.IsNullOrWhiteSpace(app.ApplicationPath))
            {
                tags.Add(new Tag(app.ApplicationPath));
            }
            else
            {
                tags.Add(new Tag(appName));
            }

            elements.Add(new DetailsElement
            {
                Key = title,
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
