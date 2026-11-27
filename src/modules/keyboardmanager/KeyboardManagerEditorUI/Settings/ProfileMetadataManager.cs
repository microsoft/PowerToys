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
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private static string FilePath => Path.Combine(KeyboardManagerPaths.SettingsDirectory, "profileMetadata.json");

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

            if (Load(forUpdate: false).Profiles.TryGetValue(id, out ProfileMetadataEntry? entry) && !string.IsNullOrWhiteSpace(entry.Name))
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
                ProfileMetadataFile file = Load(forUpdate: true);

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

        /// <summary>
        /// Drops any stored metadata for a deleted (or about to be re-created) profile id. Returns
        /// false when the file could not be updated.
        /// </summary>
        public static bool Remove(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return true;
            }

            try
            {
                ProfileMetadataFile file = Load(forUpdate: true);
                if (file.Profiles.Remove(id))
                {
                    Save(file);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"ProfileMetadataManager.Remove('{id}'): {ex.Message}");
                return false;
            }
        }

        // Written like the other Keyboard Manager files: a whole temporary file, then one replace,
        // so a reader never sees half a file and a failed write leaves the old one as it was.
        private static void Save(ProfileMetadataFile file)
        {
            string directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(directory, $"profileMetadata.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(file, _jsonOptions));
                SettingsFileReplacer.Replace(temporaryPath, FilePath);
            }
            finally
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception)
                {
                }
            }
        }

        // A file that cannot be opened right now (another process has it) must not be taken for an
        // empty one by a caller that writes the result back: every other display name would be
        // lost. With forUpdate that read error is thrown instead, after a short retry. A file that
        // can be read but is not valid JSON has no names left to lose and counts as empty.
        private static ProfileMetadataFile Load(bool forUpdate)
        {
            const int Attempts = 5;
            const int RetryDelayMS = 50;
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    if (!File.Exists(FilePath))
                    {
                        return new ProfileMetadataFile();
                    }

                    ProfileMetadataFile? file = JsonSerializer.Deserialize<ProfileMetadataFile>(File.ReadAllText(FilePath), _jsonOptions);

                    // Ids are compared without case; a deserialized dictionary is not.
                    var result = new ProfileMetadataFile();
                    foreach (KeyValuePair<string, ProfileMetadataEntry> entry in file?.Profiles ?? new())
                    {
                        if (entry.Value != null)
                        {
                            result.Profiles[entry.Key] = entry.Value;
                        }
                    }

                    return result;
                }
                catch (JsonException ex)
                {
                    Logger.LogWarning($"ProfileMetadataManager: profileMetadata.json is not valid JSON: {ex.Message}");
                    return new ProfileMetadataFile();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    if (attempt < Attempts)
                    {
                        System.Threading.Thread.Sleep(RetryDelayMS);
                        continue;
                    }

                    Logger.LogWarning($"ProfileMetadataManager: failed to read profileMetadata.json: {ex.Message}");
                    if (forUpdate)
                    {
                        throw;
                    }

                    return new ProfileMetadataFile();
                }
            }
        }
    }
}
