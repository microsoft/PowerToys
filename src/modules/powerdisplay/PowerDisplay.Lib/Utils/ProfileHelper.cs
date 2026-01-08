// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// Helper class for profile management.
    /// Provides shared logic for generating unique profile names and other profile-related operations.
    /// </summary>
    public static class ProfileHelper
    {
        /// <summary>
        /// Default base name for new profiles.
        /// </summary>
        public const string DefaultProfileBaseName = "Profile";

        /// <summary>
        /// Generates a unique profile name that doesn't conflict with existing names.
        /// Uses the format "Profile N" where N is an incrementing number.
        /// </summary>
        /// <param name="existingNames">Set of existing profile names to avoid conflicts.</param>
        /// <param name="baseName">Optional base name to use (defaults to "Profile").</param>
        /// <returns>A unique profile name.</returns>
        public static string GenerateUniqueProfileName(ISet<string> existingNames, string? baseName = null)
        {
            if (existingNames == null)
            {
                existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var nameBase = string.IsNullOrEmpty(baseName) ? DefaultProfileBaseName : baseName;

            // Start with base name without number
            if (!existingNames.Contains(nameBase))
            {
                return nameBase;
            }

            // Try "Profile 2", "Profile 3", etc.
            // Use 1000 as reasonable upper limit for counter
            int counter = 2;
            while (counter < 1000)
            {
                var candidateName = string.Format(CultureInfo.InvariantCulture, "{0} {1}", nameBase, counter);
                if (!existingNames.Contains(candidateName))
                {
                    return candidateName;
                }

                counter++;
            }

            // Fallback with timestamp if somehow we hit the limit
            return string.Format(CultureInfo.InvariantCulture, "{0} {1}", nameBase, DateTime.Now.Ticks);
        }
    }
}
