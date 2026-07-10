// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PowerDisplay.Models
{
    /// <summary>
    /// Helper for loading and saving PowerDisplay profiles from/to disk.
    /// Provides shared file I/O logic used by both Settings UI and PowerDisplay module.
    /// Thread-safe across processes and AOT-compatible.
    /// All compound operations (load → modify → save) are atomic.
    /// </summary>
    public static class ProfileHelper
    {
        private const string ProfilesMutexName = @"Local\PowerToys_PowerDisplay_Profiles";

        private static readonly Lazy<string> _profilesFilePath = new Lazy<string>(() =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "PowerToys",
                "PowerDisplay",
                "profiles.json"));

        private static readonly Lazy<ProfileStore> _profileStore = new Lazy<ProfileStore>(() =>
            new ProfileStore(ProfilesFilePath, ProfilesMutexName, TimeSpan.FromSeconds(5)));

        /// <summary>
        /// Gets the full path to the profiles JSON file.
        /// </summary>
        public static string ProfilesFilePath => _profilesFilePath.Value;

        /// <summary>
        /// Loads PowerDisplay profiles from disk.
        /// Thread-safe operation.
        /// </summary>
        /// <returns>A new empty collection when the file does not exist; otherwise the deserialized profiles.</returns>
        public static PowerDisplayProfiles LoadProfiles()
        {
            return _profileStore.Value.LoadProfiles();
        }

        public static Task<PowerDisplayProfiles> LoadProfilesAsync(CancellationToken cancellationToken = default)
            => _profileStore.Value.LoadProfilesAsync(cancellationToken);

        /// <summary>
        /// Saves PowerDisplay profiles to disk.
        /// Thread-safe operation with automatic timestamp update.
        /// </summary>
        /// <param name="profiles">The profiles collection to save.</param>
        /// <returns>True if save was successful, false otherwise.</returns>
        public static bool SaveProfiles(PowerDisplayProfiles profiles)
        {
            if (profiles == null)
            {
                return false;
            }

            _profileStore.Value.SaveProfiles(profiles);
            return true;
        }

        /// <summary>
        /// Loads profiles and, atomically, back-fills a stable id for any legacy profile still missing
        /// one (<see cref="PowerDisplayProfiles.EnsureIds"/>), persisting only when something changed.
        /// Returns the loaded (and possibly healed) profiles. Idempotent once ids are assigned.
        /// </summary>
        /// <returns>The loaded profiles, with stable ids guaranteed.</returns>
        public static PowerDisplayProfiles LoadProfilesEnsuringIds()
        {
            return _profileStore.Value.LoadProfilesEnsuringIds();
        }

        public static Task<PowerDisplayProfiles> LoadProfilesEnsuringIdsAsync(CancellationToken cancellationToken = default)
            => _profileStore.Value.LoadProfilesEnsuringIdsAsync(cancellationToken);

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

            _profileStore.Value.AddOrUpdateProfile(profile);
            return true;
        }

        public static Task AddOrUpdateProfileAsync(
            PowerDisplayProfile profile,
            CancellationToken cancellationToken = default)
            => _profileStore.Value.AddOrUpdateProfileAsync(profile, cancellationToken);

        /// <summary>
        /// Removes a profile by its stable id and persists to disk atomically.
        /// </summary>
        public static bool RemoveProfileById(int id)
        {
            return _profileStore.Value.RemoveProfileById(id);
        }

        public static Task<bool> RemoveProfileByIdAsync(int id, CancellationToken cancellationToken = default)
            => _profileStore.Value.RemoveProfileByIdAsync(id, cancellationToken);

        /// <summary>
        /// Loads, conditionally updates, and saves profiles under one cross-process lock.
        /// </summary>
        /// <param name="update">Returns true when the profiles changed and must be saved.</param>
        /// <returns>True when the profiles changed and were saved; otherwise false.</returns>
        public static bool UpdateProfiles(Func<PowerDisplayProfiles, bool> update)
        {
            return _profileStore.Value.UpdateProfiles(update);
        }

        public static Task<bool> UpdateProfilesAsync(
            Func<PowerDisplayProfiles, bool> update,
            CancellationToken cancellationToken = default)
            => _profileStore.Value.UpdateProfilesAsync(update, cancellationToken);
    }
}
