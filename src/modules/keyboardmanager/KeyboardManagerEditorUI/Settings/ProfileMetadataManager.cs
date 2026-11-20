// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManagedCommon;

namespace KeyboardManagerEditorUI.Settings
{
    /// <summary>
    /// Editor-only side store for per-profile display metadata, keyed by the profile's stable id
    /// (which is its config filename stem). Kept deliberately separate from settings.json so the
    /// profile <b>identity</b> — the id in activeConfiguration / keyboardConfigurations, which both
    /// the engine and the PowerToys Settings app read — never changes shape: renaming a profile only
    /// edits the display name here, while the id, the {id}.json file, and every reference to it stay
    /// put. When no metadata exists for an id (e.g. an existing "default" from before this feature),
    /// callers fall back to showing the id itself.
    /// </summary>
    internal static class ProfileMetadataManager
    {
        private static readonly string _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys",
            "Keyboard Manager",
            "profileMetadata.json");

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private sealed class ProfileMetadataFile
        {
            // Keyed by profile id (config filename stem). Room to grow: description, icon, etc.
            public Dictionary<string, ProfileMetadataEntry> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class ProfileMetadataEntry
        {
            public string Name { get; set; } = string.Empty;
        }

        /// <summary>
        /// Returns the display name for <paramref name="id"/>, or the id itself when no metadata
        /// (or a blank name) is stored — so a profile always has something to show.
        /// </summary>
        public static string GetDisplayName(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return id;
            }

            if (Load().Profiles.TryGetValue(id, out ProfileMetadataEntry? entry) && !string.IsNullOrWhiteSpace(entry.Name))
            {
                return entry.Name;
            }

            return id;
        }

        /// <summary>Sets (or clears, when <paramref name="name"/> equals the id) the display name for an id.</summary>
        public static bool SetDisplayName(string id, string name)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            try
            {
                ProfileMetadataFile file = Load();

                // Storing a name identical to the id is just the fallback, so drop the entry to keep
                // the file minimal rather than persisting a redundant record.
                if (string.IsNullOrWhiteSpace(name) || string.Equals(name, id, StringComparison.Ordinal))
                {
                    if (!file.Profiles.Remove(id))
                    {
                        return true; // nothing stored, nothing to do
                    }
                }
                else
                {
                    file.Profiles[id] = new ProfileMetadataEntry { Name = name };
                }

                Save(file);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"ProfileMetadataManager.SetDisplayName('{id}'): {ex.Message}");
                return false;
            }
        }

        /// <summary>Drops any stored metadata for a deleted profile id.</summary>
        public static void Remove(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            try
            {
                ProfileMetadataFile file = Load();
                if (file.Profiles.Remove(id))
                {
                    Save(file);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"ProfileMetadataManager.Remove('{id}'): {ex.Message}");
            }
        }

        private static void Save(ProfileMetadataFile file)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(file, _jsonOptions));
        }

        private static ProfileMetadataFile Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    return JsonSerializer.Deserialize<ProfileMetadataFile>(File.ReadAllText(_filePath), _jsonOptions)
                           ?? new ProfileMetadataFile();
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"ProfileMetadataManager: failed to read profileMetadata.json: {ex.Message}");
            }

            return new ProfileMetadataFile();
        }
    }
}
