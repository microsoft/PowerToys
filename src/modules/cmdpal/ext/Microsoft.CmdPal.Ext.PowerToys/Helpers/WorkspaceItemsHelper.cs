// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;

namespace PowerToysExtension.Helpers;

internal static class WorkspaceItemsHelper
{
    private const string WorkspacesDirectory = "Microsoft\\PowerToys\\Workspaces";
    private const string WorkspacesFileName = "workspaces.json";

    internal sealed class WorkspaceApplication
    {
        public string? Application { get; set; }
    }

    internal sealed class WorkspaceProject
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public List<WorkspaceApplication>? Applications { get; set; }
    }

    internal sealed class WorkspacesData
    {
        public List<WorkspaceProject>? Workspaces { get; set; }
    }

    internal static ListItem CreateOpenEditorItem()
    {
        return new ListItem(new OpenWorkspaceEditorCommand())
        {
            Title = "Open Workspaces editor",
            Subtitle = "Launch the PowerToys Workspaces editor",
            Icon = IconHelpers.FromRelativePath("Assets\\Workspaces.png"),
        };
    }

    internal static IListItem[] GetWorkspaceItems(string? searchText)
    {
        var workspaces = LoadWorkspaces();
        var filtered = string.IsNullOrWhiteSpace(searchText)
            ? workspaces
            : workspaces.Where(ws => Contains(ws.Name, searchText)).ToList();

        var items = new List<IListItem>(filtered.Count + 1)
        {
            CreateOpenEditorItem(),
        };

        foreach (var workspace in filtered)
        {
            if (string.IsNullOrEmpty(workspace.Id) || string.IsNullOrEmpty(workspace.Name))
            {
                continue;
            }

            items.Add(new ListItem(new LaunchWorkspaceCommand(workspace.Id))
            {
                Title = workspace.Name,
                Subtitle = BuildSubtitle(workspace),
                Icon = IconHelpers.FromRelativePath("Assets\\Workspaces.png"),
            });
        }

        return items.ToArray();
    }

    internal static IListItem[] FilteredItems(string searchText) => GetWorkspaceItems(searchText);

    private static List<WorkspaceProject> LoadWorkspaces()
    {
        try
        {
            var path = GetWorkspacesFilePath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return [];
            }

            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            var data = JsonSerializer.Deserialize(json, PowerToysJsonContext.Default.WorkspacesData);
            return data?.Workspaces ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string? GetWorkspacesFilePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localAppData))
        {
            return null;
        }

        return Path.Combine(localAppData, WorkspacesDirectory, WorkspacesFileName);
    }

    private static string BuildSubtitle(WorkspaceProject workspace)
    {
        var count = workspace.Applications?.Count ?? 0;
        if (count == 0)
        {
            return "No applications";
        }

        if (count == 1)
        {
            return "1 application";
        }

        return string.Format(CultureInfo.CurrentCulture, "{0} applications", count);
    }

    private static bool Contains(string? source, string needle)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        return source.Contains(needle, StringComparison.CurrentCultureIgnoreCase);
    }
}
