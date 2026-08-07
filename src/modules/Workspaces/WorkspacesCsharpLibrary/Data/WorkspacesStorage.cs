// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using WorkspacesCsharpLibrary.SettingsService;

namespace WorkspacesCsharpLibrary.Data;

/// <summary>
/// Reader/writer for persisted workspaces.  All access goes through the
/// PTSettingsSvc service: the service stores opaque
/// bytes and this class owns the JSON shape and defensive parsing.  It is
/// protected-store-only — there is NO fallback to the user-writable legacy
/// %LocalAppData% file (that file is only the one-time migration source).
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
                // Protected-store-only: the service isn't up yet.  Do NOT read the
                // user-writable legacy file (it is stale once migrated, and
                // tamperable) — return empty until protection is provisioned.
                // Migration (which reads the legacy file exactly once) is what
                // seeds the protected store.
                return Array.Empty<ProjectWrapper>();

            default:
                // AuthRejected / Protocol / IoError → fail safe to empty.
                return Array.Empty<ProjectWrapper>();
        }
    }

    /// <summary>Outcome of a strict-mode save.</summary>
    public enum SaveOutcome
    {
        /// <summary>Persisted through the protected service.</summary>
        Saved,

        /// <summary>The service is unavailable or rejected this caller (e.g. a
        /// pending upgrade re-point).  The caller should prompt to (re-)enable
        /// protection (elevation) rather than write unprotected — we deliberately
        /// do NOT silently fall back to plaintext.</summary>
        ProtectionUnavailable,

        /// <summary>A protocol/IO error; the save did not happen.</summary>
        Failed,
    }

    /// <summary>
    /// Persists the workspaces through the protected service (STRICT mode).
    /// Unlike the old best-effort behaviour, a missing/incompatible service does
    /// NOT silently write plaintext to %LocalAppData%; it returns
    /// <see cref="SaveOutcome.ProtectionUnavailable"/> so the caller can prompt
    /// the user to enable protection (elevation).
    /// </summary>
    public static SaveOutcome Save(IReadOnlyList<ProjectWrapper> workspaces)
    {
        byte[] bytes = Serialise(workspaces);

        var rc = PTSettingsClient.PutBlob(bytes);
        switch (rc)
        {
            case PTSettingsClient.Result.Ok:
                return SaveOutcome.Saved;

            case PTSettingsClient.Result.Unavailable:
            case PTSettingsClient.Result.AuthRejected:
                // No usable protected writer (not installed / declined elevation /
                // pending upgrade re-point).  Do NOT write plaintext silently.
                return SaveOutcome.ProtectionUnavailable;

            default:
                return SaveOutcome.Failed;
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
