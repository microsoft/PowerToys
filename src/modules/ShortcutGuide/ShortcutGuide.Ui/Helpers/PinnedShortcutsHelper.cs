// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using ShortcutGuide.Models;

namespace ShortcutGuide.Helpers
{
    public static class PinnedShortcutsHelper
    {
        /// <summary>
        /// Raised after the pinned-shortcut list for an application has been updated and persisted.
        /// The string argument is the affected application name.
        /// </summary>
        public static event EventHandler<string>? PinnedShortcutsChanged;

        public static void UpdatePinnedShortcuts(string appName, ShortcutEntry shortcutEntry)
        {
            if (!App.PinnedShortcuts.TryGetValue(appName, out var list))
            {
                list = new List<ShortcutEntry>();
                App.PinnedShortcuts[appName] = list;
            }

            if (!list.Remove(shortcutEntry))
            {
                list.Add(shortcutEntry);
            }

            // Persist on a best-effort basis. The in-memory pinned list is the source of truth
            // for the rest of the session; failing to write should not crash the overlay
            // (Pin/Unpin runs from a synchronous UI handler).
            Save();
            PinnedShortcutsChanged?.Invoke(null, appName);
        }

        public static void Save()
        {
            try
            {
                string serialized = JsonSerializer.Serialize(App.PinnedShortcuts);
                string pinnedPath = SettingsUtils.Default.GetSettingsFilePath(ShortcutGuideSettings.ModuleName, "Pinned.json");
                File.WriteAllText(pinnedPath, serialized);
            }
            catch (Exception ex) when (ex is IOException
                                    or UnauthorizedAccessException
                                    or JsonException)
            {
                Logger.LogError("Failed to persist Shortcut Guide pinned shortcuts; keeping in-memory state.", ex);
            }
        }
    }
}
