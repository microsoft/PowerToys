// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.State;

public sealed class PinnedAppsManager
{
    private static readonly Lazy<PinnedAppsManager> _instance = new(() => new PinnedAppsManager());
    private readonly string _pinnedAppsFilePath;

    public static PinnedAppsManager Instance => _instance.Value;

    private PinnedApps _pinnedApps = new();

    // Add event for when pinning state changes
    public event EventHandler? PinStateChanged;

    private PinnedAppsManager()
    {
        _pinnedAppsFilePath = GetPinnedAppsFilePath();
        LoadPinnedApps();
    }

    public bool IsAppPinned(string appIdentifier)
    {
        return _pinnedApps.PinnedAppIdentifiers.Contains(appIdentifier, StringComparer.OrdinalIgnoreCase);
    }

    public void PinApp(string appIdentifier)
    {
        if (!IsAppPinned(appIdentifier))
        {
            _pinnedApps.PinnedAppIdentifiers.Add(appIdentifier);
            SavePinnedApps();
            Logger.LogTrace($"Pinned app: {appIdentifier}");
            PinStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void UnpinApp(string appIdentifier)
    {
        var removed = _pinnedApps.PinnedAppIdentifiers.RemoveAll(id =>
            string.Equals(id, appIdentifier, StringComparison.OrdinalIgnoreCase));

        if (removed > 0)
        {
            SavePinnedApps();
            Logger.LogTrace($"Unpinned app: {appIdentifier}");
            PinStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void LoadPinnedApps()
    {
        try
        {
            _pinnedApps = PinnedApps.ReadFromFile(_pinnedAppsFilePath);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load pinned apps: {ex.Message}");
            _pinnedApps = new PinnedApps();
        }
    }

    private void SavePinnedApps()
    {
        try
        {
            PinnedApps.WriteToFile(_pinnedAppsFilePath, _pinnedApps);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to save pinned apps: {ex.Message}");
        }
    }

    private static string GetPinnedAppsFilePath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "apps.pinned.json");
    }
}
