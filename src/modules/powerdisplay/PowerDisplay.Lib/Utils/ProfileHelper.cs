// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// Helper class for profile management operations.
    /// Provides utilities for profile name generation and validation.
    /// </summary>
    public static class ProfileHelper
    {
        private const string DefaultProfileBaseName = "Profile";

        /// <summary>
        /// Generate a unique profile name that doesn't conflict with existing names.
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
    }
}
