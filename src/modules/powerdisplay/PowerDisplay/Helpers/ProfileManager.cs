// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerDisplay.Configuration;
using PowerDisplay.Serialization;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// Manages PowerDisplay profiles storage and retrieval
    /// </summary>
    public class ProfileManager
    {
        private readonly string _profilesFilePath;
        private readonly object _lock = new object();
        private PowerDisplayProfiles? _cachedProfiles;

        public ProfileManager()
        {
            var settingsPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var powerToysPath = Path.Combine(settingsPath, "Microsoft", "PowerToys", "PowerDisplay");

            if (!Directory.Exists(powerToysPath))
            {
                Directory.CreateDirectory(powerToysPath);
            }

            _profilesFilePath = Path.Combine(powerToysPath, "profiles.json");

            Logger.LogInfo($"ProfileManager initialized, profiles file: {_profilesFilePath}");
        }

        /// <summary>
        /// Loads profiles from disk
        /// </summary>
        public PowerDisplayProfiles LoadProfiles()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_profilesFilePath))
                    {
                        var json = File.ReadAllText(_profilesFilePath);
                        var profiles = JsonSerializer.Deserialize(json, AppJsonContext.Default.PowerDisplayProfiles);

                        if (profiles != null)
                        {
                            _cachedProfiles = profiles;
                            Logger.LogInfo($"Loaded {profiles.Profiles.Count} profiles, current: {profiles.CurrentProfile}");
                            return profiles;
                        }
                    }

                    Logger.LogInfo("No profiles file found, creating default");
                    _cachedProfiles = new PowerDisplayProfiles();
                    return _cachedProfiles;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to load profiles: {ex.Message}");
                    _cachedProfiles = new PowerDisplayProfiles();
                    return _cachedProfiles;
                }
            }
        }

        /// <summary>
        /// Saves profiles to disk
        /// </summary>
        public void SaveProfiles(PowerDisplayProfiles profiles)
        {
            lock (_lock)
            {
                try
                {
                    profiles.LastUpdated = DateTime.UtcNow;
                    var json = JsonSerializer.Serialize(profiles, AppJsonContext.Default.PowerDisplayProfiles);
                    File.WriteAllText(_profilesFilePath, json);
                    _cachedProfiles = profiles;

                    Logger.LogInfo($"Saved {profiles.Profiles.Count} profiles, current: {profiles.CurrentProfile}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to save profiles: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Gets the currently active profile
        /// </summary>
        public PowerDisplayProfile? GetCurrentProfile()
        {
            var profiles = LoadProfiles();
            return profiles.GetCurrentProfile();
        }

        /// <summary>
        /// Sets the current profile by name
        /// </summary>
        public void SetCurrentProfile(string profileName)
        {
            lock (_lock)
            {
                var profiles = LoadProfiles();

                // Validate profile exists (unless it's Custom)
                if (!profileName.Equals(PowerDisplayProfiles.CustomProfileName, StringComparison.OrdinalIgnoreCase))
                {
                    var profile = profiles.GetProfile(profileName);
                    if (profile == null)
                    {
                        Logger.LogWarning($"Cannot set current profile: '{profileName}' not found");
                        return;
                    }
                }

                profiles.CurrentProfile = profileName;
                SaveProfiles(profiles);

                Logger.LogInfo($"Current profile set to: {profileName}");
            }
        }

        /// <summary>
        /// Creates or updates the Custom profile from current monitor states
        /// </summary>
        public void CreateCustomProfileFromCurrent(List<ProfileMonitorSetting> monitorSettings)
        {
            lock (_lock)
            {
                var profiles = LoadProfiles();

                var customProfile = new PowerDisplayProfile(
                    PowerDisplayProfiles.CustomProfileName,
                    monitorSettings);

                profiles.SetProfile(customProfile);
                SaveProfiles(profiles);

                Logger.LogInfo($"Custom profile created/updated with {monitorSettings.Count} monitors");
            }
        }

        /// <summary>
        /// Adds or updates a profile
        /// </summary>
        public void AddOrUpdateProfile(PowerDisplayProfile profile)
        {
            lock (_lock)
            {
                if (profile == null || !profile.IsValid())
                {
                    Logger.LogWarning("Cannot add invalid profile");
                    return;
                }

                var profiles = LoadProfiles();
                profiles.SetProfile(profile);
                SaveProfiles(profiles);

                Logger.LogInfo($"Profile '{profile.Name}' added/updated with {profile.MonitorSettings.Count} monitors");
            }
        }

        /// <summary>
        /// Removes a profile by name
        /// </summary>
        public bool RemoveProfile(string profileName)
        {
            lock (_lock)
            {
                var profiles = LoadProfiles();
                bool removed = profiles.RemoveProfile(profileName);

                if (removed)
                {
                    SaveProfiles(profiles);
                    Logger.LogInfo($"Profile '{profileName}' removed");
                }
                else
                {
                    Logger.LogWarning($"Profile '{profileName}' not found or cannot be removed");
                }

                return removed;
            }
        }

        /// <summary>
        /// Gets all profiles
        /// </summary>
        public List<PowerDisplayProfile> GetAllProfiles()
        {
            var profiles = LoadProfiles();
            return profiles.Profiles.ToList();
        }

        /// <summary>
        /// Gets the current profile name
        /// </summary>
        public string GetCurrentProfileName()
        {
            var profiles = LoadProfiles();
            return profiles.CurrentProfile;
        }

        /// <summary>
        /// Checks if currently on a non-Custom profile
        /// </summary>
        public bool IsOnNonCustomProfile()
        {
            var currentProfileName = GetCurrentProfileName();
            return !currentProfileName.Equals(PowerDisplayProfiles.CustomProfileName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
