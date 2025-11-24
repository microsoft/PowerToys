// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using ManagedCommon;
using PowerDisplay.Common.Interfaces;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Serialization;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Centralized service for managing PowerDisplay profiles storage and retrieval.
    /// Provides unified access to profile data for PowerDisplay, Settings UI, and LightSwitch modules.
    /// Thread-safe and AOT-compatible.
    /// Implements <see cref="IProfileService"/> for dependency injection support.
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
        /// Private constructor to enforce singleton pattern for instance-based access.
        /// Static methods remain available for backward compatibility.
        /// </summary>
        private ProfileService()
        {
        }

        /// <summary>
        /// Loads PowerDisplay profiles from disk.
        /// Thread-safe operation with automatic legacy profile cleanup.
        /// </summary>
        /// <returns>PowerDisplayProfiles object, or a new empty instance if file doesn't exist or load fails</returns>
        public static PowerDisplayProfiles LoadProfiles()
        {
            lock (_lock)
            {
                try
                {
                    var filePath = PathConstants.ProfilesFilePath;

                    // Ensure directory exists
                    PathConstants.EnsurePowerDisplayFolderExists();

                    if (File.Exists(filePath))
                    {
                        var json = File.ReadAllText(filePath);
                        var profiles = JsonSerializer.Deserialize(json, ProfileSerializationContext.Default.PowerDisplayProfiles);

                        if (profiles != null)
                        {
                            // Clean up any legacy Custom profiles
                            profiles.Profiles.RemoveAll(p => p.Name.Equals(PowerDisplayProfiles.CustomProfileName, StringComparison.OrdinalIgnoreCase));

                            Logger.LogInfo($"{LogPrefix} Loaded {profiles.Profiles.Count} profiles from {filePath}");
                            return profiles;
                        }
                    }
                    else
                    {
                        Logger.LogInfo($"{LogPrefix} No profiles file found at {filePath}, returning empty collection");
                    }

                    return new PowerDisplayProfiles();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"{LogPrefix} Failed to load profiles: {ex.Message}");
                    return new PowerDisplayProfiles();
                }
            }
        }

        /// <summary>
        /// Saves PowerDisplay profiles to disk.
        /// Thread-safe operation with automatic timestamp update and legacy profile cleanup.
        /// </summary>
        /// <param name="profiles">The profiles collection to save</param>
        /// <returns>True if save was successful, false otherwise</returns>
        public static bool SaveProfiles(PowerDisplayProfiles profiles)
        {
            lock (_lock)
            {
                try
                {
                    if (profiles == null)
                    {
                        Logger.LogWarning($"{LogPrefix} Cannot save null profiles");
                        return false;
                    }

                    // Ensure directory exists
                    PathConstants.EnsurePowerDisplayFolderExists();

                    // Clean up any Custom profiles before saving
                    profiles.Profiles.RemoveAll(p => p.Name.Equals(PowerDisplayProfiles.CustomProfileName, StringComparison.OrdinalIgnoreCase));

                    profiles.LastUpdated = DateTime.UtcNow;

                    var json = JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles);

                    var filePath = PathConstants.ProfilesFilePath;
                    File.WriteAllText(filePath, json);

                    Logger.LogInfo($"{LogPrefix} Saved {profiles.Profiles.Count} profiles to {filePath}");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"{LogPrefix} Failed to save profiles: {ex.Message}");
                    return false;
                }
            }
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

                var profiles = LoadProfilesInternal();
                profiles.SetProfile(profile);

                var result = SaveProfilesInternal(profiles);

                if (result)
                {
                    Logger.LogInfo($"{LogPrefix} Profile '{profile.Name}' added/updated with {profile.MonitorSettings.Count} monitors");
                }

                return result;
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
                var profiles = LoadProfilesInternal();
                bool removed = profiles.RemoveProfile(profileName);

                if (removed)
                {
                    SaveProfilesInternal(profiles);
                    Logger.LogInfo($"{LogPrefix} Profile '{profileName}' removed");
                }
                else
                {
                    Logger.LogWarning($"{LogPrefix} Profile '{profileName}' not found or cannot be removed");
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
                var profiles = LoadProfilesInternal();
                return profiles.GetProfile(profileName);
            }
        }

        /// <summary>
        /// Checks if the profiles file exists.
        /// </summary>
        /// <returns>True if profiles file exists, false otherwise</returns>
        public static bool ProfilesFileExists()
        {
            try
            {
                return File.Exists(PathConstants.ProfilesFilePath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"{LogPrefix} Error checking if profiles file exists: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the path to the profiles file.
        /// </summary>
        /// <returns>The full path to the profiles file</returns>
        public static string GetProfilesFilePath()
        {
            return PathConstants.ProfilesFilePath;
        }

        // Internal methods without lock for use within already-locked contexts
        private static PowerDisplayProfiles LoadProfilesInternal()
        {
            try
            {
                var filePath = PathConstants.ProfilesFilePath;

                PathConstants.EnsurePowerDisplayFolderExists();

                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var profiles = JsonSerializer.Deserialize(json, ProfileSerializationContext.Default.PowerDisplayProfiles);

                    if (profiles != null)
                    {
                        profiles.Profiles.RemoveAll(p => p.Name.Equals(PowerDisplayProfiles.CustomProfileName, StringComparison.OrdinalIgnoreCase));
                        return profiles;
                    }
                }

                return new PowerDisplayProfiles();
            }
            catch (Exception ex)
            {
                Logger.LogError($"{LogPrefix} Failed to load profiles internally: {ex.Message}");
                return new PowerDisplayProfiles();
            }
        }

        private static bool SaveProfilesInternal(PowerDisplayProfiles profiles)
        {
            try
            {
                if (profiles == null)
                {
                    return false;
                }

                PathConstants.EnsurePowerDisplayFolderExists();

                profiles.Profiles.RemoveAll(p => p.Name.Equals(PowerDisplayProfiles.CustomProfileName, StringComparison.OrdinalIgnoreCase));
                profiles.LastUpdated = DateTime.UtcNow;

                var json = JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles);
                var filePath = PathConstants.ProfilesFilePath;
                File.WriteAllText(filePath, json);

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"{LogPrefix} Failed to save profiles internally: {ex.Message}");
                return false;
            }
        }

        // IProfileService Implementation
        // Explicit interface implementation to satisfy IProfileService
        // These methods delegate to the static methods for backward compatibility

        /// <inheritdoc/>
        PowerDisplayProfiles IProfileService.LoadProfiles() => LoadProfiles();

        /// <inheritdoc/>
        bool IProfileService.SaveProfiles(PowerDisplayProfiles profiles) => SaveProfiles(profiles);

        /// <inheritdoc/>
        bool IProfileService.AddOrUpdateProfile(PowerDisplayProfile profile) => AddOrUpdateProfile(profile);

        /// <inheritdoc/>
        bool IProfileService.RemoveProfile(string profileName) => RemoveProfile(profileName);

        /// <inheritdoc/>
        PowerDisplayProfile? IProfileService.GetProfile(string profileName) => GetProfile(profileName);

        /// <inheritdoc/>
        bool IProfileService.ProfilesFileExists() => ProfilesFileExists();

        /// <inheritdoc/>
        string IProfileService.GetProfilesFilePath() => GetProfilesFilePath();
    }
}
