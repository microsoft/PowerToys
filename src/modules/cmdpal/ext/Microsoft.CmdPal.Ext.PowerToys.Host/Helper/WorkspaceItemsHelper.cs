// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Common.Search.FuzzSearch;
using ManagedCommon;
using Microsoft.CmdPal.Ext.PowerToys.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerToys.Helper;

// A helper class for workspace items.
internal static class WorkspaceItemsHelper
{
    // Simple classes to deserialize workspace data
    internal sealed class WorkspaceProject
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public List<WorkspaceApplication> Applications { get; set; } = new();
    }

    internal sealed class WorkspaceApplication
    {
        public string Application { get; set; } = string.Empty;
    }

    internal sealed class WorkspacesData
    {
        public List<WorkspaceProject> Workspaces { get; set; } = new();
    }

    public static List<ListItem> AllWorkspaces()
    {
        var items = new List<ListItem>();

        try
        {
            var workspacesFilePath = GetWorkspacesFilePath();

            if (!File.Exists(workspacesFilePath))
            {
                return items;
            }

            var jsonContent = File.ReadAllText(workspacesFilePath);

            var workspacesData = JsonSerializer.Deserialize(jsonContent, PowerToysJsonContext.Default.WorkspacesData);

            if (workspacesData?.Workspaces == null)
            {
                return items;
            }

            foreach (var project in workspacesData.Workspaces)
            {
                if (string.IsNullOrEmpty(project.Id) || string.IsNullOrEmpty(project.Name))
                {
                    continue;
                }

                var item = new ListItem(new LaunchWorkspaceCommand(project.Id))
                {
                    // Icon = IconHelpers.FromRelativePath("Assets\\Workspaces.svg"),
                    Title = project.Name,
                    Subtitle = GetWorkspaceSubtitle(project),
                };

                items.Add(item);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error loading workspaces: {ex.Message}");
        }

        return items;
    }

    public static IListItem[] FilteredItems(string query)
    {
        var allItems = AllWorkspaces();

        if (string.IsNullOrWhiteSpace(query))
        {
            return [.. allItems];
        }

        var matched = new List<Tuple<int, ListItem>>();

        foreach (var item in allItems)
        {
            var matchResult = StringMatcher.FuzzyMatch(query, item.Title);
            if (matchResult.Success)
            {
                matched.Add(new Tuple<int, ListItem>(matchResult.Score, item));
            }
        }

        matched.Sort((x, y) => y.Item1.CompareTo(x.Item1));
        return [.. matched.Select(x => x.Item2)];
    }

    private static string GetWorkspacesFilePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Microsoft", "PowerToys", "Workspaces", "workspaces.json");
    }

    private static string GetWorkspaceSubtitle(WorkspaceProject project)
    {
        var appCount = project.Applications?.Count ?? 0;
        if (appCount == 0)
        {
            return "No applications";
        }
        else if (appCount == 1)
        {
            return "1 application";
        }
        else
        {
            return $"{appCount} applications";
        }
    }
}
