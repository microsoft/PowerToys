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

        public static Task<PowerDisplayProfiles> LoadProfilesAsync(CancellationToken cancellationToken = default)
            => _profileStore.Value.LoadProfilesAsync(cancellationToken);

        public static Task AddOrUpdateProfileAsync(
            PowerDisplayProfile profile,
            CancellationToken cancellationToken = default)
            => _profileStore.Value.AddOrUpdateProfileAsync(profile, cancellationToken);

        public static Task<bool> RemoveProfileByIdAsync(int id, CancellationToken cancellationToken = default)
            => _profileStore.Value.RemoveProfileByIdAsync(id, cancellationToken);

        public static Task<bool> UpdateProfilesAsync(
            Func<PowerDisplayProfiles, bool> update,
            CancellationToken cancellationToken = default)
            => _profileStore.Value.UpdateProfilesAsync(update, cancellationToken);
    }
}
