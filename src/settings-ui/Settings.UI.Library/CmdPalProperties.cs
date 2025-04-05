// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class CmdPalProperties
    {
        // Default shortcut - Win + Alt + Space
        public static readonly HotkeySettings DefaultHotkeyValue = new HotkeySettings(true, false, true, false, 32);

#pragma warning disable SA1401 // Fields should be private
#pragma warning disable CA1051 // Do not declare visible instance fields
        public HotkeySettings Hotkey;
#pragma warning restore CA1051 // Do not declare visible instance fields
#pragma warning restore SA1401 // Fields should be private

        private string _settingsFilePath;

        public CmdPalProperties()
        {
            var localAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

#if DEBUG
            _settingsFilePath = Path.Combine(localAppDataDir, "Packages", "Microsoft.CommandPalette.Dev_8wekyb3d8bbwe", "LocalState", "settings.json");
#else
            _settingsFilePath = Path.Combine(localAppDataDir, "Packages", "Microsoft.CommandPalette_8wekyb3d8bbwe", "LocalState", "settings.json");
#endif

            InitializeHotkey();
        }

        public void InitializeHotkey()
        {
            try
            {
                string json = File.ReadAllText(_settingsFilePath); // Read JSON file
                using JsonDocument doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty(nameof(Hotkey), out JsonElement hotkeyElement))
                {
                    Hotkey = JsonSerializer.Deserialize<HotkeySettings>(hotkeyElement.GetRawText());

                    if (Hotkey == null)
                    {
                        Hotkey = DefaultHotkeyValue;
                    }
                }
            }
            catch (Exception)
            {
                Hotkey = DefaultHotkeyValue;
            }
        }
    }
}
