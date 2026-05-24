// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        /// <inheritdoc cref="ModelsProfileHelper.SaveProfiles"/>
        public static bool SaveProfiles(PowerDisplayProfiles profiles) => ModelsProfileHelper.SaveProfiles(profiles);
    }
}
