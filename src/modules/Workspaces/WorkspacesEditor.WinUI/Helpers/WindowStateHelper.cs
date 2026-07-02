// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;

using ManagedCommon;

namespace WorkspacesEditor.Helpers
{
    internal static class WindowStateHelper
    {
        private static readonly string StateFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys",
            "Workspaces",
            "editor-window-state.json");

        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        public static WindowStateData Load()
        {
            try
            {
                if (File.Exists(StateFilePath))
                {
                    string json = File.ReadAllText(StateFilePath);
                    return JsonSerializer.Deserialize<WindowStateData>(json);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load editor window state", ex);
            }

            return null;
        }

        public static void Save(WindowStateData state)
        {
            try
            {
                string directory = Path.GetDirectoryName(StateFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(state, SerializerOptions);
                File.WriteAllText(StateFilePath, json);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save editor window state", ex);
            }
        }
    }
}
