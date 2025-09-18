// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using ManagedCommon;

namespace PowerToys.WorkspacesMCP.Services.Workspaces;

/// <summary>
/// Loads workspace entries from the standard workspaces.json file.
/// Now reads from file on every access to ensure fresh data.
/// </summary>
public sealed class WorkspaceCatalogService : IWorkspaceCatalog
{
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string WorkspacesDirectory = Path.Combine(LocalAppData, "Microsoft", "PowerToys", "Workspaces");
    private static readonly string WorkspacesFilePath = Path.Combine(WorkspacesDirectory, "workspaces.json");

    public WorkspaceCatalogService()
    {
        // No longer pre-loading at startup - read on demand
    }

    public IReadOnlyList<WorkspaceEntry> Workspaces => LoadFromFile();

    public DateTime LoadedAtUtc => DateTime.UtcNow; // Always current time since we read on demand

    private List<WorkspaceEntry> LoadFromFile()
    {
        var workspaces = new List<WorkspaceEntry>();

        if (!File.Exists(WorkspacesFilePath))
        {
            Logger.LogDebug($"Workspaces file not found at {WorkspacesFilePath}");
            return workspaces;
        }

        try
        {
            using var stream = File.OpenRead(WorkspacesFilePath);
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("workspaces", out var workspacesElement) || workspacesElement.ValueKind != JsonValueKind.Array)
            {
                Logger.LogWarning("workspaces.json missing 'workspaces' array.");
                return workspaces;
            }

            foreach (var wsElement in workspacesElement.EnumerateArray())
            {
                if (!wsElement.TryGetProperty("id", out var idProp) || idProp.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var id = idProp.GetString();
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                string? name = null;
                if (wsElement.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                {
                    name = nameProp.GetString();
                }

                workspaces.Add(new WorkspaceEntry(id.Trim(), name?.Trim()));
            }
        }
        catch (JsonException ex)
        {
            Logger.LogWarning($"Failed to parse workspaces.json at {WorkspacesFilePath}: {ex}");
        }
        catch (IOException ex)
        {
            Logger.LogWarning($"Failed to read workspaces.json at {WorkspacesFilePath}: {ex}");
        }

        return workspaces;
    }
}
