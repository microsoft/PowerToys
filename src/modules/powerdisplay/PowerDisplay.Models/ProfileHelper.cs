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
    /// All compound operations (load → modify → save) are atomic within a single process.
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
                return LoadProfilesCore();
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
                return SaveProfilesCore(profiles);
            }
        }

        /// <summary>
        /// Adds or updates a profile and persists to disk atomically.
        /// </summary>
        /// <param name="profile">The profile to add or update.</param>
        /// <returns>True if the operation was successful, false otherwise.</returns>
        public static bool AddOrUpdateProfile(PowerDisplayProfile profile)
        {
            if (profile == null || !profile.IsValid())
            {
                return false;
            }

            lock (_lock)
            {
                var profiles = LoadProfilesCore();
                profiles.SetProfile(profile);
                return SaveProfilesCore(profiles);
            }
        }

        /// <summary>
        /// Renames and updates a profile atomically (for rename or edit operations).
        /// Removes the old entry by <paramref name="oldName"/> and upserts the updated profile.
        /// </summary>
        /// <param name="oldName">The current name of the profile to replace.</param>
        /// <param name="newProfile">The updated profile.</param>
        /// <returns>True if the operation was successful, false otherwise.</returns>
        public static bool RenameAndUpdateProfile(string oldName, PowerDisplayProfile newProfile)
        {
            if (newProfile == null || !newProfile.IsValid())
            {
                return false;
            }

            lock (_lock)
            {
                var profiles = LoadProfilesCore();
                profiles.RemoveProfile(oldName);
                profiles.SetProfile(newProfile);
                return SaveProfilesCore(profiles);
            }
        }

        /// <summary>
        /// Removes a profile by name and persists to disk atomically.
        /// </summary>
        /// <param name="profileName">The name of the profile to remove.</param>
        /// <returns>True if the profile was found and removed, false otherwise.</returns>
        public static bool RemoveProfile(string profileName)
        {
            lock (_lock)
            {
                var profiles = LoadProfilesCore();
                bool removed = profiles.RemoveProfile(profileName);
                if (removed)
                {
                    SaveProfilesCore(profiles);
                }

                return removed;
            }
        }

        /// <summary>
        /// Gets a profile by name.
        /// </summary>
        /// <param name="profileName">The name of the profile to retrieve.</param>
        /// <returns>The profile if found, null otherwise.</returns>
        public static PowerDisplayProfile? GetProfile(string profileName)
        {
            lock (_lock)
            {
                return LoadProfilesCore().GetProfile(profileName);
            }
        }

        // Lock-free core methods — only call from within a lock (_lock) block.
        private static PowerDisplayProfiles LoadProfilesCore()
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

        private static bool SaveProfilesCore(PowerDisplayProfiles profiles)
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
