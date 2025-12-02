// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using PowerDisplay.Common.Models;

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// Helper class for profile management operations.
    /// Provides utilities for profile name generation and validation.
    /// </summary>
    public static class ProfileHelper
    {
        /// <summary>
        /// Default base name for new profiles.
        /// </summary>
        public const string DefaultProfileBaseName = "Profile";

        /// <summary>
        /// Generate a unique profile name that doesn't conflict with existing profiles.
        /// </summary>
        /// <param name="existingProfiles">The collection of existing profiles.</param>
        /// <param name="baseName">The base name to use (default: "Profile").</param>
        /// <returns>A unique profile name like "Profile 1", "Profile 2", etc.</returns>
        public static string GenerateUniqueProfileName(PowerDisplayProfiles? existingProfiles, string baseName = DefaultProfileBaseName)
            => GenerateUniqueProfileName(
                existingProfiles?.Profiles?.Select(p => p.Name).Where(n => !string.IsNullOrEmpty(n))!,
                baseName);

        /// <summary>
        /// Generate a unique profile name from a collection of profile names.
        /// </summary>
        /// <param name="existingNames">Enumerable of existing profile names.</param>
        /// <param name="baseName">The base name to use (default: "Profile").</param>
        /// <returns>A unique profile name like "Profile 1", "Profile 2", etc.</returns>
        public static string GenerateUniqueProfileName(IEnumerable<string>? existingNames, string baseName = DefaultProfileBaseName)
        {
            var nameSet = existingNames != null ? new HashSet<string>(existingNames) : null;
            return GenerateUniqueProfileName(nameSet, baseName);
        }

        /// <summary>
        /// Generate a unique profile name that doesn't conflict with existing names.
        /// Core implementation used by all overloads.
        /// </summary>
        /// <param name="existingNames">Set of existing profile names.</param>
        /// <param name="baseName">The base name to use (default: "Profile").</param>
        /// <returns>A unique profile name like "Profile 1", "Profile 2", etc.</returns>
        public static string GenerateUniqueProfileName(ISet<string>? existingNames, string baseName = DefaultProfileBaseName)
        {
            if (existingNames == null || existingNames.Count == 0)
            {
                return $"{baseName} 1";
            }

            int counter = 1;
            string name;
            do
            {
                name = $"{baseName} {counter}";
                counter++;
            }
            while (existingNames.Contains(name));

            return name;
        }

        /// <summary>
        /// Validate that a profile has at least one monitor with at least one setting.
        /// </summary>
        /// <param name="profile">The profile to validate.</param>
        /// <returns>True if the profile has valid settings.</returns>
        public static bool HasValidSettings(PowerDisplayProfile profile)
        {
            if (profile == null || profile.MonitorSettings == null || profile.MonitorSettings.Count == 0)
            {
                return false;
            }

            // Check that at least one monitor has at least one setting
            return profile.MonitorSettings.Any(m =>
                m.Brightness.HasValue ||
                m.Contrast.HasValue ||
                m.Volume.HasValue ||
                m.ColorTemperatureVcp.HasValue);
        }

        /// <summary>
        /// Check if a profile name is available (not already in use).
        /// </summary>
        /// <param name="name">The name to check.</param>
        /// <param name="existingProfiles">The collection of existing profiles.</param>
        /// <returns>True if the name is available.</returns>
        public static bool IsNameAvailable(string name, PowerDisplayProfiles existingProfiles)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if (existingProfiles?.Profiles == null || existingProfiles.Profiles.Count == 0)
            {
                return true;
            }

            return !existingProfiles.Profiles.Any(p =>
                string.Equals(p.Name, name, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
