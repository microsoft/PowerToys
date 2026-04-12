// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerDisplay.Models;

namespace PowerDisplay.Common.Interfaces
{
    /// <summary>
    /// Interface for profile management service.
    /// Provides abstraction for loading and saving PowerDisplay profiles.
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
    }
}
