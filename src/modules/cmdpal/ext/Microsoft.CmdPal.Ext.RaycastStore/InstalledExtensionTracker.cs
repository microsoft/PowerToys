// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ManagedCommon;

namespace Microsoft.CmdPal.Ext.RaycastStore;

internal sealed class InstalledExtensionTracker
{
    private readonly string _jsExtensionsDir;

    private Dictionary<string, InstalledRaycastExtension> _installed = new(StringComparer.OrdinalIgnoreCase);

    private bool _scanned;

    public InstalledExtensionTracker()
    {
        _jsExtensionsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys",
            "CommandPalette",
            "JSExtensions");
    }

    public IReadOnlyList<InstalledRaycastExtension> GetInstalledExtensions()
    {
        EnsureScanned();
        List<InstalledRaycastExtension> list = new(_installed.Count);
        foreach (KeyValuePair<string, InstalledRaycastExtension> item in _installed)
        {
            list.Add(item.Value);
        }

        return list;
    }

    public bool IsInstalled(string raycastDirectoryName)
    {
        EnsureScanned();

        if (_installed.ContainsKey(raycastDirectoryName))
        {
            return true;
        }

        if (_installed.ContainsKey("raycast-" + raycastDirectoryName))
        {
            return true;
        }

        foreach (KeyValuePair<string, InstalledRaycastExtension> item in _installed)
        {
            if (string.Equals(item.Value.RaycastName, raycastDirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public void Refresh()
    {
        _scanned = false;
        _installed = new Dictionary<string, InstalledRaycastExtension>(StringComparer.OrdinalIgnoreCase);
        ScanDirectory();
    }

    private void EnsureScanned()
    {
        if (!_scanned)
        {
            ScanDirectory();
        }
    }

    private void ScanDirectory()
    {
        _scanned = true;
        if (!Directory.Exists(_jsExtensionsDir))
        {
            return;
        }

        string[] directories = Directory.GetDirectories(_jsExtensionsDir);
        foreach (string dir in directories)
        {
            string cmdpalJsonPath = Path.Combine(dir, "cmdpal.json");
            string compatJsonPath = Path.Combine(dir, "raycast-compat.json");
            if (!File.Exists(cmdpalJsonPath) || !File.Exists(compatJsonPath))
            {
                continue;
            }

            try
            {
                string compatJson = File.ReadAllText(compatJsonPath);
                using JsonDocument compatDoc = JsonDocument.Parse(compatJson);
                JsonElement root = compatDoc.RootElement;

                if (root.TryGetProperty("installedBy", out var installedBy) && installedBy.GetString() != "raycast-pipeline")
                {
                    continue;
                }

                string raycastName = root.TryGetProperty("raycastOriginalName", out var origName)
                    ? origName.GetString() ?? Path.GetFileName(dir)
                    : Path.GetFileName(dir);

                string cmdpalJson = File.ReadAllText(cmdpalJsonPath);
                using JsonDocument cmdpalDoc = JsonDocument.Parse(cmdpalJson);
                JsonElement cmdpalRoot = cmdpalDoc.RootElement;

                string name = cmdpalRoot.TryGetProperty("name", out var nameVal)
                    ? nameVal.GetString() ?? string.Empty
                    : string.Empty;

                string displayName = cmdpalRoot.TryGetProperty("displayName", out var displayVal)
                    ? displayVal.GetString() ?? name
                    : name;

                string version = cmdpalRoot.TryGetProperty("version", out var versionVal)
                    ? versionVal.GetString() ?? "unknown"
                    : "unknown";

                InstalledRaycastExtension ext = new()
                {
                    Name = name,
                    RaycastName = raycastName,
                    DisplayName = displayName,
                    Version = version,
                    Path = dir,
                };
                _installed[raycastName] = ext;
            }
            catch (Exception ex)
            {
                Logger.LogDebug("Skipping directory " + dir + ": " + ex.Message);
            }
        }

        Logger.LogDebug($"InstalledExtensionTracker: found {_installed.Count} Raycast extension(s)");
    }
}
