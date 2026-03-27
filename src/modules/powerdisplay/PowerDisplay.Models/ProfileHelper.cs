// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;

namespace PowerDisplay.Models
{
    /// <summary>
    /// Helper for loading and saving PowerDisplay profiles from/to disk.
    /// Provides shared file I/O logic used by both Settings UI and PowerDisplay module.
    /// Thread-safe and AOT-compatible.
    /// </summary>
    public static class ProfileHelper
    {
        private static readonly object _lock = new object();

        private static readonly Lazy<string> _profilesFilePath = new Lazy<string>(() =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "PowerToys",
                "PowerDisplay",
                "profiles.json"));

        /// <summary>
        /// Gets the full path to the profiles JSON file.
        /// </summary>
        public static string ProfilesFilePath => _profilesFilePath.Value;

        /// <summary>
        /// Loads PowerDisplay profiles from disk.
        /// Thread-safe operation.
        /// </summary>
        /// <returns>PowerDisplayProfiles object, or a new empty instance if file doesn't exist or load fails.</returns>
        public static PowerDisplayProfiles LoadProfiles()
        {
            lock (_lock)
            {
                try
                {
                    EnsureFolderExists();

                    if (File.Exists(ProfilesFilePath))
                    {
                        var json = File.ReadAllText(ProfilesFilePath);
                        var profiles = JsonSerializer.Deserialize(json, ProfileSerializationContext.Default.PowerDisplayProfiles);

                        if (profiles != null)
                        {
                            return profiles;
                        }
                    }

                    return new PowerDisplayProfiles();
                }
                catch (Exception)
                {
                    return new PowerDisplayProfiles();
                }
            }
        }

        /// <summary>
        /// Saves PowerDisplay profiles to disk.
        /// Thread-safe operation with automatic timestamp update.
        /// </summary>
        /// <param name="profiles">The profiles collection to save.</param>
        /// <returns>True if save was successful, false otherwise.</returns>
        public static bool SaveProfiles(PowerDisplayProfiles profiles)
        {
            lock (_lock)
            {
                try
                {
                    if (profiles == null)
                    {
                        return false;
                    }

                    EnsureFolderExists();

                    profiles.LastUpdated = DateTime.UtcNow;

                    var json = JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles);
                    File.WriteAllText(ProfilesFilePath, json);

                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        private static void EnsureFolderExists()
        {
            var folder = Path.GetDirectoryName(ProfilesFilePath);
            if (folder != null && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }
    }
}
