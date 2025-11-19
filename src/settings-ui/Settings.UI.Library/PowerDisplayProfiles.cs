// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Container for all PowerDisplay profiles
    /// </summary>
    public class PowerDisplayProfiles
    {
        public const string CustomProfileName = "Custom";

        [JsonPropertyName("profiles")]
        public List<PowerDisplayProfile> Profiles { get; set; }

        [JsonPropertyName("currentProfile")]
        public string CurrentProfile { get; set; }

        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; }

        public PowerDisplayProfiles()
        {
            Profiles = new List<PowerDisplayProfile>();
            CurrentProfile = CustomProfileName;
            LastUpdated = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets the profile by name
        /// </summary>
        public PowerDisplayProfile? GetProfile(string name)
        {
            return Profiles.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets the currently active profile
        /// </summary>
        public PowerDisplayProfile? GetCurrentProfile()
        {
            return GetProfile(CurrentProfile);
        }

        /// <summary>
        /// Adds or updates a profile
        /// </summary>
        public void SetProfile(PowerDisplayProfile profile)
        {
            if (profile == null || !profile.IsValid())
            {
                throw new ArgumentException("Profile is invalid");
            }

            var existing = GetProfile(profile.Name);
            if (existing != null)
            {
                Profiles.Remove(existing);
            }

            profile.Touch();
            Profiles.Add(profile);
            LastUpdated = DateTime.UtcNow;
        }

        /// <summary>
        /// Removes a profile by name
        /// </summary>
        public bool RemoveProfile(string name)
        {
            // Cannot remove the Custom profile
            if (name.Equals(CustomProfileName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var profile = GetProfile(name);
            if (profile != null)
            {
                Profiles.Remove(profile);
                LastUpdated = DateTime.UtcNow;

                // If the removed profile was current, switch to Custom
                if (CurrentProfile.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    CurrentProfile = CustomProfileName;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Generates the next available profile name (Profile1, Profile2, etc.)
        /// </summary>
        public string GenerateProfileName()
        {
            int counter = 1;
            while (true)
            {
                string name = $"Profile{counter}";
                if (GetProfile(name) == null)
                {
                    return name;
                }

                counter++;
            }
        }

        /// <summary>
        /// Checks if a profile name is valid and available
        /// </summary>
        public bool IsNameAvailable(string name, string? excludeName = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            // Custom is reserved
            if (name.Equals(CustomProfileName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check if name is already used (excluding the profile being renamed)
            var existing = GetProfile(name);
            if (existing != null && (excludeName == null || !existing.Name.Equals(excludeName, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return true;
        }
    }
}
