// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.IO;
using System.Text.Json;
using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Helper class for managing PowerDisplay profiles storage and retrieval
    /// Provides centralized access to profile data for both PowerDisplay and LightSwitch modules
    /// </summary>
    public static class PowerDisplayProfilesHelper
    {
        private static readonly object _lock = new object();
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true };
        private static string? _cachedProfilesFilePath;

        /// <summary>
        /// Gets the file path where PowerDisplay profiles are stored
        /// </summary>
        public static string GetProfilesFilePath()
        {
            if (_cachedProfilesFilePath != null)
            {
                return _cachedProfilesFilePath;
            }

            lock (_lock)
            {
                if (_cachedProfilesFilePath != null)
                {
                    return _cachedProfilesFilePath;
                }

                var settingsPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var powerToysPath = Path.Combine(settingsPath, "Microsoft", "PowerToys", "PowerDisplay");

                if (!Directory.Exists(powerToysPath))
                {
                    Directory.CreateDirectory(powerToysPath);
                }

                _cachedProfilesFilePath = Path.Combine(powerToysPath, "profiles.json");
                return _cachedProfilesFilePath;
            }
        }

        /// <summary>
        /// Loads PowerDisplay profiles from disk
        /// </summary>
        /// <returns>PowerDisplayProfiles object, or a new empty instance if file doesn't exist or load fails</returns>
        public static PowerDisplayProfiles LoadProfiles()
        {
            lock (_lock)
            {
                try
                {
                    var filePath = GetProfilesFilePath();

                    if (File.Exists(filePath))
                    {
                        var json = File.ReadAllText(filePath);
                        var profiles = JsonSerializer.Deserialize<PowerDisplayProfiles>(json);

                        if (profiles != null)
                        {
                            // Clean up any legacy Custom profiles
                            profiles.Profiles.RemoveAll(p => p.Name.Equals(PowerDisplayProfiles.CustomProfileName, StringComparison.OrdinalIgnoreCase));

                            Logger.LogInfo($"[PowerDisplayProfilesHelper] Loaded {profiles.Profiles.Count} profiles from {filePath}");
                            return profiles;
                        }
                    }
                    else
                    {
                        Logger.LogInfo($"[PowerDisplayProfilesHelper] No profiles file found at {filePath}, returning empty collection");
                    }

                    return new PowerDisplayProfiles();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[PowerDisplayProfilesHelper] Failed to load profiles: {ex.Message}");
                    return new PowerDisplayProfiles();
                }
            }
        }

        /// <summary>
        /// Saves PowerDisplay profiles to disk
        /// </summary>
        /// <param name="profiles">The profiles collection to save</param>
        /// <param name="prettyPrint">Whether to format the JSON with indentation</param>
        public static void SaveProfiles(PowerDisplayProfiles profiles, bool prettyPrint = true)
        {
            lock (_lock)
            {
                try
                {
                    if (profiles == null)
                    {
                        Logger.LogWarning("[PowerDisplayProfilesHelper] Cannot save null profiles");
                        return;
                    }

                    // Clean up any Custom profiles before saving
                    profiles.Profiles.RemoveAll(p => p.Name.Equals(PowerDisplayProfiles.CustomProfileName, StringComparison.OrdinalIgnoreCase));

                    profiles.LastUpdated = DateTime.UtcNow;

                    var json = prettyPrint
                        ? JsonSerializer.Serialize(profiles, _jsonSerializerOptions)
                        : JsonSerializer.Serialize(profiles);

                    var filePath = GetProfilesFilePath();

                    File.WriteAllText(filePath, json);

                    Logger.LogInfo($"[PowerDisplayProfilesHelper] Saved {profiles.Profiles.Count} profiles to {filePath}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[PowerDisplayProfilesHelper] Failed to save profiles: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Checks if the profiles file exists
        /// </summary>
        public static bool ProfilesFileExists()
        {
            try
            {
                var filePath = GetProfilesFilePath();
                return File.Exists(filePath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[PowerDisplayProfilesHelper] Error checking if profiles file exists: {ex.Message}");
                return false;
            }
        }
    }
}
