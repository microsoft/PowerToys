// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using ManagedCommon;

namespace PowerToysExtension.Helpers;

/// <summary>
/// Watches the global PowerToys settings.json and notifies listeners when it changes.
/// </summary>
internal static class SettingsChangeNotifier
{
    private static readonly object Sync = new();
    private static FileSystemWatcher? _watcher;
    private static Timer? _debounceTimer;

    internal static event Action? SettingsChanged;

    static SettingsChangeNotifier()
    {
        TryStartWatcher();
    }

    private static void TryStartWatcher()
    {
        try
        {
            var filePath = ModuleEnablementService.SettingsFilePath;
            var directory = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            {
                return;
            }

            _watcher = new FileSystemWatcher(directory)
            {
                Filter = fileName,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
            };

            _watcher.Changed += (_, _) => ScheduleRaise();
            _watcher.Created += (_, _) => ScheduleRaise();
            _watcher.Deleted += (_, _) => ScheduleRaise();
            _watcher.Renamed += (_, _) => ScheduleRaise();
        }
        catch (Exception ex)
        {
            Logger.LogError($"SettingsChangeNotifier failed to start: {ex.Message}");
        }
    }

    private static void ScheduleRaise()
    {
        lock (Sync)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(
                _ => SettingsChanged?.Invoke(),
                null,
                200,
                Timeout.Infinite);
        }
    }
}
