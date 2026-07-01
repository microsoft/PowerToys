// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using WorkspacesCsharpLibrary.SettingsService;

namespace WorkspacesCsharpLibrary.Data;

/// <summary>
/// Reader/writer for persisted workspaces.  All access goes through the
/// PTSettingsSvc service (Design-v6-Final.md §10): the service stores opaque
/// bytes, this class owns the JSON shape, defensive parsing and the
/// no-service last-resort fallback to the legacy %LocalAppData% file.
/// </summary>
public static class WorkspacesStorage
{
    public static IReadOnlyList<ProjectWrapper> Load()
    {
        var rc = PTSettingsClient.GetBlob(out var blob);
        switch (rc)
        {
            case PTSettingsClient.Result.Ok:
                return ParseDefensive(blob);

            case PTSettingsClient.Result.NotFound:
                // Service is up but this user has no blob yet (first run /
                // pre-migration).  Not an error.
                return Array.Empty<ProjectWrapper>();

            case PTSettingsClient.Result.Unavailable:
                // No service installed (no-admin install / declined elevation).
                // Last resort: read the legacy file directly (Design §10/§11).
                return ParseDefensive(ReadLegacyBytes());

            default:
                // AuthRejected / Protocol / IoError → fail safe to empty.
                return Array.Empty<ProjectWrapper>();
        }
    }

    /// <summary>
    /// Persists the workspaces through the service.  Returns true on success.
    /// Falls back to a direct legacy-file write only when no service exists.
    /// </summary>
    public static bool Save(IReadOnlyList<ProjectWrapper> workspaces)
    {
        byte[] bytes = Serialise(workspaces);

        var rc = PTSettingsClient.PutBlob(bytes);
        switch (rc)
        {
            case PTSettingsClient.Result.Ok:
                return true;

            case PTSettingsClient.Result.Unavailable:
                return WriteLegacyBytes(bytes);

            default:
                return false;
        }
    }

    private static IReadOnlyList<ProjectWrapper> ParseDefensive(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return Array.Empty<ProjectWrapper>();
        }

        try
        {
            var data = JsonSerializer.Deserialize(bytes, WorkspacesStorageJsonContext.Default.WorkspacesFile);
            if (data?.Workspaces == null)
            {
                return Array.Empty<ProjectWrapper>();
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
        catch (JsonException)
        {
            return Array.Empty<ProjectWrapper>();
        }
        catch (NotSupportedException)
        {
            return Array.Empty<ProjectWrapper>();
        }
    }

    private static byte[] Serialise(IReadOnlyList<ProjectWrapper> workspaces)
    {
        var file = new WorkspacesFile
        {
            Workspaces = (workspaces ?? new List<ProjectWrapper>())
                .Select(ws => new WorkspaceProject
                {
                    Id = ws.Id,
                    Name = ws.Name,
                    Applications = ws.Applications ?? new List<ApplicationWrapper>(),
                    MonitorConfiguration = ws.MonitorConfiguration ?? new List<MonitorConfigurationWrapper>(),
                    CreationTime = ws.CreationTime,
                    LastLaunchedTime = ws.LastLaunchedTime,
                    IsShortcutNeeded = ws.IsShortcutNeeded,
                    MoveExistingWindows = ws.MoveExistingWindows,
                })
                .ToList(),
        };

        return JsonSerializer.SerializeToUtf8Bytes(file, WorkspacesStorageJsonContext.Default.WorkspacesFile);
    }

    private static byte[] ReadLegacyBytes()
    {
        try
        {
            var legacy = SettingsPaths.LegacyWorkspacesFile();
            return File.Exists(legacy) ? File.ReadAllBytes(legacy) : Array.Empty<byte>();
        }
        catch (IOException)
        {
            return Array.Empty<byte>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<byte>();
        }
    }

    private static bool WriteLegacyBytes(byte[] bytes)
    {
        try
        {
            var legacy = SettingsPaths.LegacyWorkspacesFile();
            var dir = Path.GetDirectoryName(legacy);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(legacy, bytes);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal sealed class WorkspacesFile
    {
        public List<WorkspaceProject> Workspaces { get; set; } = new();
    }

    internal sealed class WorkspaceProject
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("applications")]
        public List<ApplicationWrapper> Applications { get; set; } = new();

        [JsonPropertyName("monitor-configuration")]
        public List<MonitorConfigurationWrapper> MonitorConfiguration { get; set; } = new();

        [JsonPropertyName("creation-time")]
        public long CreationTime { get; set; }

        [JsonPropertyName("last-launched-time")]
        public long LastLaunchedTime { get; set; }

        [JsonPropertyName("is-shortcut-needed")]
        public bool IsShortcutNeeded { get; set; }

        [JsonPropertyName("move-existing-windows")]
        public bool MoveExistingWindows { get; set; }
    }
}
