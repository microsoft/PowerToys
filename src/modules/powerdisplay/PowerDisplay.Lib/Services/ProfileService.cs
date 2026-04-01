// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerDisplay.Common.Interfaces;
using PowerDisplay.Models;
using ModelsProfileHelper = PowerDisplay.Models.ProfileHelper;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Thin facade over <see cref="ProfileHelper"/> that satisfies <see cref="IProfileService"/>
    /// and provides named static entry points for PowerDisplay.exe callers.
    /// All locking and compound-operation atomicity is handled by <see cref="PowerDisplay.Models.ProfileHelper"/>.
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

        /// <inheritdoc cref="ModelsProfileHelper.LoadProfiles"/>
        public static PowerDisplayProfiles LoadProfiles() => ModelsProfileHelper.LoadProfiles();

        /// <inheritdoc cref="ModelsProfileHelper.SaveProfiles"/>
        public static bool SaveProfiles(PowerDisplayProfiles profiles) => ModelsProfileHelper.SaveProfiles(profiles);

        /// <inheritdoc cref="ModelsProfileHelper.AddOrUpdateProfile"/>
        public static bool AddOrUpdateProfile(PowerDisplayProfile profile) => ModelsProfileHelper.AddOrUpdateProfile(profile);

        /// <inheritdoc cref="ModelsProfileHelper.RenameAndUpdateProfile"/>
        public static bool RenameAndUpdateProfile(string oldName, PowerDisplayProfile newProfile) => ModelsProfileHelper.RenameAndUpdateProfile(oldName, newProfile);

        /// <inheritdoc cref="ModelsProfileHelper.RemoveProfile"/>
        public static bool RemoveProfile(string profileName) => ModelsProfileHelper.RemoveProfile(profileName);

        /// <inheritdoc cref="ModelsProfileHelper.GetProfile"/>
        public static PowerDisplayProfile? GetProfile(string profileName) => ModelsProfileHelper.GetProfile(profileName);

        // IProfileService explicit implementation

        /// <inheritdoc/>
        PowerDisplayProfiles IProfileService.LoadProfiles() => ModelsProfileHelper.LoadProfiles();

        /// <inheritdoc/>
        bool IProfileService.SaveProfiles(PowerDisplayProfiles profiles) => ModelsProfileHelper.SaveProfiles(profiles);
    }
}
