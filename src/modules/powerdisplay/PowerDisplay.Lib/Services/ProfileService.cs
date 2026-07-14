// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Models;
using ModelsProfileHelper = PowerDisplay.Models.ProfileHelper;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Thin facade over <see cref="ProfileHelper"/> that provides named static entry points
    /// for PowerDisplay.exe callers.
    /// All locking and compound-operation atomicity is handled by <see cref="PowerDisplay.Models.ProfileHelper"/>.
    /// </summary>
    public static class ProfileService
    {
        /// <inheritdoc cref="ModelsProfileHelper.LoadProfiles"/>
        public static PowerDisplayProfiles LoadProfiles() => ModelsProfileHelper.LoadProfiles();

        /// <inheritdoc cref="ModelsProfileHelper.LoadProfilesAsync(CancellationToken)"/>
        public static Task<PowerDisplayProfiles> LoadProfilesAsync(CancellationToken cancellationToken = default)
            => ModelsProfileHelper.LoadProfilesAsync(cancellationToken);

        /// <inheritdoc cref="ModelsProfileHelper.SaveProfiles"/>
        public static bool SaveProfiles(PowerDisplayProfiles profiles) => ModelsProfileHelper.SaveProfiles(profiles);

        /// <inheritdoc cref="ModelsProfileHelper.UpdateProfiles"/>
        public static bool UpdateProfiles(Func<PowerDisplayProfiles, bool> update) => ModelsProfileHelper.UpdateProfiles(update);

        /// <inheritdoc cref="ModelsProfileHelper.UpdateProfilesAsync(Func{PowerDisplayProfiles, bool}, CancellationToken)"/>
        public static Task<bool> UpdateProfilesAsync(
            Func<PowerDisplayProfiles, bool> update,
            CancellationToken cancellationToken = default)
            => ModelsProfileHelper.UpdateProfilesAsync(update, cancellationToken);
    }
}
