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
        public static void UpdatePinnedShortcuts(string appName, ShortcutEntry shortcutEntry)
        {
            if (!App.PinnedShortcuts[appName].Remove(shortcutEntry))
            {
                App.PinnedShortcuts[appName].Add(shortcutEntry);
            }

            Save();
        }

        public static void Save()
        {
            string serialized = JsonSerializer.Serialize(App.PinnedShortcuts);

            string pinnedPath = SettingsUtils.Default.GetSettingsFilePath(ShortcutGuideSettings.ModuleName, "Pinned.json");
            File.WriteAllText(pinnedPath, serialized);
        }
    }
}
