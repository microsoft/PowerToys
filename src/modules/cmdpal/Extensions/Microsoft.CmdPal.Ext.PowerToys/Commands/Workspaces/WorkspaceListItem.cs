// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;
using PowerToysExtension.Properties;
using WorkspacesCsharpLibrary.Data;

namespace PowerToysExtension.Commands;

internal sealed partial class WorkspaceListItem : ListItem
{
    private static readonly CompositeFormat ApplicationsFormat = CompositeFormat.Parse(Resources.Workspaces_Applications_Format);
    private static readonly CompositeFormat LastLaunchedFormat = CompositeFormat.Parse(Resources.Workspaces_LastLaunched_Format);
    private static readonly CompositeFormat ApplicationsCountFormat = CompositeFormat.Parse(Resources.Workspaces_ApplicationsCount_Format);
    private static readonly CompositeFormat MinAgoFormat = CompositeFormat.Parse(Resources.Workspaces_MinAgo_Format);
    private static readonly CompositeFormat HrAgoFormat = CompositeFormat.Parse(Resources.Workspaces_HrAgo_Format);
    private static readonly CompositeFormat DaysAgoFormat = CompositeFormat.Parse(Resources.Workspaces_DaysAgo_Format);

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
            0 => Resources.Workspaces_NoApplications,
            _ => string.Format(CultureInfo.CurrentCulture, ApplicationsFormat, appCount),
        };

        var lastLaunched = workspace.LastLaunchedTime > 0
            ? string.Format(CultureInfo.CurrentCulture, LastLaunchedFormat, FormatRelativeTime(workspace.LastLaunchedTime))
            : Resources.Workspaces_NeverLaunched;

        return $"{appsText} \u2022 {lastLaunched}";
    }

    private static Details BuildDetails(ProjectWrapper workspace, IconInfo icon)
    {
        var appCount = workspace.Applications?.Count ?? 0;
        var body = appCount switch
        {
            0 => Resources.Workspaces_NoApplicationsInWorkspace,
            1 => Resources.Workspaces_OneApplication,
            _ => string.Format(CultureInfo.CurrentCulture, ApplicationsCountFormat, appCount),
        };

        return new Details
        {
            HeroImage = icon,
            Title = workspace.Name ?? Resources.Workspaces_Workspace,
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
            var appName = string.IsNullOrWhiteSpace(app.Application) ? Resources.Workspaces_App : app.Application;
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
            return Resources.Workspaces_JustNow;
        }

        if (delta.TotalMinutes < 60)
        {
            return string.Format(CultureInfo.CurrentCulture, MinAgoFormat, (int)delta.TotalMinutes);
        }

        if (delta.TotalHours < 24)
        {
            return string.Format(CultureInfo.CurrentCulture, HrAgoFormat, (int)delta.TotalHours);
        }

        return string.Format(CultureInfo.CurrentCulture, DaysAgoFormat, (int)delta.TotalDays);
    }
}
