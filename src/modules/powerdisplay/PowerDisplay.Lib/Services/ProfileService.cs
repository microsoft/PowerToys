// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerDisplay.Common.Interfaces;
using PowerDisplay.Models;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Thin facade over <see cref="ProfileHelper"/> that satisfies <see cref="IProfileService"/>
    /// and provides named static entry points for PowerDisplay.exe callers.
    /// All locking and compound-operation atomicity is handled by ProfileHelper.
    /// </summary>
    public class ProfileService : IProfileService
    {
        /// <summary>
        /// Gets the singleton instance of the ProfileService.
        /// </summary>
        public static IProfileService Instance { get; } = new ProfileService();

        private ProfileService()
        {
        }

        /// <inheritdoc cref="ProfileHelper.LoadProfiles"/>
        public static PowerDisplayProfiles LoadProfiles() => ProfileHelper.LoadProfiles();

        /// <inheritdoc cref="ProfileHelper.SaveProfiles"/>
        public static bool SaveProfiles(PowerDisplayProfiles profiles) => ProfileHelper.SaveProfiles(profiles);

        /// <inheritdoc cref="ProfileHelper.AddOrUpdateProfile"/>
        public static bool AddOrUpdateProfile(PowerDisplayProfile profile) => ProfileHelper.AddOrUpdateProfile(profile);

        /// <inheritdoc cref="ProfileHelper.RenameAndUpdateProfile"/>
        public static bool RenameAndUpdateProfile(string oldName, PowerDisplayProfile newProfile) => ProfileHelper.RenameAndUpdateProfile(oldName, newProfile);

        /// <inheritdoc cref="ProfileHelper.RemoveProfile"/>
        public static bool RemoveProfile(string profileName) => ProfileHelper.RemoveProfile(profileName);

        /// <inheritdoc cref="ProfileHelper.GetProfile"/>
        public static PowerDisplayProfile? GetProfile(string profileName) => ProfileHelper.GetProfile(profileName);

        // IProfileService explicit implementation

        /// <inheritdoc/>
        PowerDisplayProfiles IProfileService.LoadProfiles() => ProfileHelper.LoadProfiles();

        /// <inheritdoc/>
        bool IProfileService.SaveProfiles(PowerDisplayProfiles profiles) => ProfileHelper.SaveProfiles(profiles);
    }
}
