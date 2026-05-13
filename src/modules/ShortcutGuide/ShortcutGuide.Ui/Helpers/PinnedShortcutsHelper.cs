// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
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

            Save();
            PinnedShortcutsChanged?.Invoke(null, appName);
        }

        public static void Save()
        {
            string serialized = JsonSerializer.Serialize(App.PinnedShortcuts);

            string pinnedPath = SettingsUtils.Default.GetSettingsFilePath(ShortcutGuideSettings.ModuleName, "Pinned.json");
            File.WriteAllText(pinnedPath, serialized);
        }
    }
}
