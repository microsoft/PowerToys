// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using WorkspacesCsharpLibrary.Data;

namespace WorkspacesCsharpLibrary.Data;

/// <summary>
/// Lightweight reader for persisted workspaces.
/// </summary>
public static class WorkspacesStorage
{
    private static readonly JsonSerializerOptions _loadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [RequiresUnreferencedCode("Workspace file deserialization uses reflection-based JSON serializer.")]
    [RequiresDynamicCode("Workspace file deserialization uses reflection-based JSON serializer.")]
    public static IReadOnlyList<ProjectWrapper> Load()
    {
        var filePath = GetDefaultFilePath();
        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<WorkspacesFile>(json, _loadOptions);

            if (data?.Workspaces == null)
            {
                return [];
            }

            return data.Workspaces
                .Where(ws => !string.IsNullOrWhiteSpace(ws.Id) && !string.IsNullOrWhiteSpace(ws.Name))
                .Select(ws => new ProjectWrapper
                {
                    Id = ws.Id!,
                    Name = ws.Name!,
                    Applications = ws.Applications ?? new List<ApplicationWrapper>(),
                    CreationTime = ws.CreationTime,
                    LastLaunchedTime = ws.LastLaunchedTime,
                    IsShortcutNeeded = ws.IsShortcutNeeded,
                    MoveExistingWindows = ws.MoveExistingWindows,
                    MonitorConfiguration = ws.MonitorConfiguration ?? new List<MonitorConfigurationWrapper>(),
                })
                .ToList()
                .AsReadOnly();
        }
        catch
        {
            return Array.Empty<ProjectWrapper>();
        }
    }

    public static string GetDefaultFilePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Microsoft", "PowerToys", "Workspaces", "workspaces.json");
    }

    private sealed class WorkspacesFile
    {
        public List<WorkspaceProject> Workspaces { get; set; } = new();
    }

    private sealed class WorkspaceProject
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public List<ApplicationWrapper> Applications { get; set; } = new();

        public List<MonitorConfigurationWrapper> MonitorConfiguration { get; set; } = new();

        public long CreationTime { get; set; }

        public long LastLaunchedTime { get; set; }

        public bool IsShortcutNeeded { get; set; }

        public bool MoveExistingWindows { get; set; }
    }
}
