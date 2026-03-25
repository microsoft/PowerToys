// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using PowerDisplay.Common.Interfaces;
using PowerDisplay.Models;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Centralized service for managing PowerDisplay profiles storage and retrieval.
    /// Provides unified access to profile data for PowerDisplay, Settings UI, and LightSwitch modules.
    /// Thread-safe and AOT-compatible.
    /// </summary>
    public class ProfileService : IProfileService
    {
        private const string LogPrefix = "[ProfileService]";
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets the singleton instance of the ProfileService.
        /// Use this for dependency injection or when interface-based access is needed.
        /// </summary>
        public static IProfileService Instance { get; } = new ProfileService();

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileService"/> class.
        /// Private constructor to enforce singleton pattern for instance-based access.
        /// Static methods remain available for backward compatibility.
        /// </summary>
        private ProfileService()
        {
        }

        /// <summary>
        /// Loads PowerDisplay profiles from disk.
        /// Delegates to ProfileHelper.LoadProfiles().
        /// </summary>
        /// <returns>PowerDisplayProfiles object, or a new empty instance if file doesn't exist or load fails</returns>
        public static PowerDisplayProfiles LoadProfiles()
        {
            return ProfileHelper.LoadProfiles();
        }

        /// <summary>
        /// Saves PowerDisplay profiles to disk.
        /// Delegates to ProfileHelper.SaveProfiles().
        /// </summary>
        /// <param name="profiles">The profiles collection to save</param>
        /// <returns>True if save was successful, false otherwise</returns>
        public static bool SaveProfiles(PowerDisplayProfiles profiles)
        {
            return ProfileHelper.SaveProfiles(profiles);
        }

        /// <summary>
        /// Adds or updates a profile in the collection and persists to disk.
        /// Thread-safe operation.
        /// </summary>
        /// <param name="profile">The profile to add or update</param>
        /// <returns>True if operation was successful, false otherwise</returns>
        public static bool AddOrUpdateProfile(PowerDisplayProfile profile)
        {
            lock (_lock)
            {
                if (profile == null || !profile.IsValid())
                {
                    Logger.LogWarning($"{LogPrefix} Cannot add invalid profile");
                    return false;
                }

                var profiles = ProfileHelper.LoadProfiles();
                profiles.SetProfile(profile);

                return ProfileHelper.SaveProfiles(profiles);
            }
        }

        /// <summary>
        /// Removes a profile by name and persists to disk.
        /// Thread-safe operation.
        /// </summary>
        /// <param name="profileName">The name of the profile to remove</param>
        /// <returns>True if profile was found and removed, false otherwise</returns>
        public static bool RemoveProfile(string profileName)
        {
            lock (_lock)
            {
                var profiles = ProfileHelper.LoadProfiles();
                bool removed = profiles.RemoveProfile(profileName);

                if (removed)
                {
                    ProfileHelper.SaveProfiles(profiles);
                }

                return removed;
            }
        }

        /// <summary>
        /// Gets a profile by name.
        /// Thread-safe operation.
        /// </summary>
        /// <param name="profileName">The name of the profile to retrieve</param>
        /// <returns>The profile if found, null otherwise</returns>
        public static PowerDisplayProfile? GetProfile(string profileName)
        {
            lock (_lock)
            {
                var profiles = ProfileHelper.LoadProfiles();
                return profiles.GetProfile(profileName);
            }
        }

        // IProfileService Implementation
        // Explicit interface implementation to satisfy IProfileService

        /// <inheritdoc/>
        PowerDisplayProfiles IProfileService.LoadProfiles() => LoadProfiles();

        /// <inheritdoc/>
        bool IProfileService.SaveProfiles(PowerDisplayProfiles profiles) => SaveProfiles(profiles);
    }
}
