// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerDisplay.Common.Models;

namespace PowerDisplay.Common.Interfaces
{
    /// <summary>
    /// Interface for profile management service.
    /// Provides abstraction for loading, saving, and managing PowerDisplay profiles.
    /// Enables dependency injection and unit testing.
    /// </summary>
    public interface IProfileService
    {
        /// <summary>
        /// Loads PowerDisplay profiles from disk.
        /// </summary>
        /// <returns>PowerDisplayProfiles object, or a new empty instance if file doesn't exist or load fails.</returns>
        PowerDisplayProfiles LoadProfiles();

        /// <summary>
        /// Saves PowerDisplay profiles to disk.
        /// </summary>
        /// <param name="profiles">The profiles collection to save.</param>
        /// <returns>True if save was successful, false otherwise.</returns>
        bool SaveProfiles(PowerDisplayProfiles profiles);

        /// <summary>
        /// Adds or updates a profile in the collection and persists to disk.
        /// </summary>
        /// <param name="profile">The profile to add or update.</param>
        /// <returns>True if operation was successful, false otherwise.</returns>
        bool AddOrUpdateProfile(PowerDisplayProfile profile);

        /// <summary>
        /// Removes a profile by name and persists to disk.
        /// </summary>
        /// <param name="profileName">The name of the profile to remove.</param>
        /// <returns>True if profile was found and removed, false otherwise.</returns>
        bool RemoveProfile(string profileName);

        /// <summary>
        /// Gets a profile by name.
        /// </summary>
        /// <param name="profileName">The name of the profile to retrieve.</param>
        /// <returns>The profile if found, null otherwise.</returns>
        PowerDisplayProfile? GetProfile(string profileName);

        /// <summary>
        /// Checks if the profiles file exists.
        /// </summary>
        /// <returns>True if profiles file exists, false otherwise.</returns>
        bool ProfilesFileExists();

        /// <summary>
        /// Gets the path to the profiles file.
        /// </summary>
        /// <returns>The full path to the profiles file.</returns>
        string GetProfilesFilePath();
    }
}
