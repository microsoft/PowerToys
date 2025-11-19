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
        // NOTE: Custom profile concept has been removed. Profiles are now templates, not states.
        // This constant is kept for backward compatibility (cleaning up legacy Custom profiles).
        public const string CustomProfileName = "Custom";

        [JsonPropertyName("profiles")]
        public List<PowerDisplayProfile> Profiles { get; set; }

        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; }

        public PowerDisplayProfiles()
        {
            Profiles = new List<PowerDisplayProfile>();
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
            var profile = GetProfile(name);
            if (profile != null)
            {
                Profiles.Remove(profile);
                LastUpdated = DateTime.UtcNow;
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
