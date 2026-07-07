// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerDisplay.Common.Services;
using Settings.UI.Library;

namespace PowerDisplay.Services
{
    /// <summary>
    /// Service for handling LightSwitch theme change events.
    /// Reads LightSwitch settings using the standard PowerToys settings pattern.
    /// </summary>
    public static class LightSwitchService
    {
        private const string LogPrefix = "[LightSwitch]";

        /// <summary>
        /// Get the profile id to apply for the given theme.
        /// </summary>
        /// <param name="isLightMode">Whether the theme changed to light mode.</param>
        /// <returns>The profile id to apply, or null if no profile is configured.</returns>
        public static int? GetProfileForTheme(bool isLightMode)
        {
            try
            {
                Logger.LogInfo($"{LogPrefix} Processing theme change to {(isLightMode ? "light" : "dark")} mode");

                var settings = SettingsUtils.Default.GetSettingsOrDefault<LightSwitchSettings>(LightSwitchSettings.ModuleName);

                if (settings?.Properties == null)
                {
                    Logger.LogWarning($"{LogPrefix} LightSwitch settings not found");
                    return null;
                }

                bool enabled;
                int storedId;
                string? storedName;
                if (isLightMode)
                {
                    enabled = settings.Properties.EnableLightModeProfile.Value;
                    storedId = settings.Properties.LightModeProfileId.Value;
                    storedName = settings.Properties.LightModeProfile.Value;
                }
                else
                {
                    enabled = settings.Properties.EnableDarkModeProfile.Value;
                    storedId = settings.Properties.DarkModeProfileId.Value;
                    storedName = settings.Properties.DarkModeProfile.Value;
                }

                if (!enabled)
                {
                    Logger.LogInfo($"{LogPrefix} {(isLightMode ? "Light" : "Dark")} mode profile is disabled");
                    return null;
                }

                var id = LightSwitchProfileResolver.Resolve(storedId, storedName, ProfileService.LoadProfiles());
                if (id is null)
                {
                    Logger.LogInfo($"{LogPrefix} No profile resolved for {(isLightMode ? "light" : "dark")} mode");
                    return null;
                }

                Logger.LogInfo($"{LogPrefix} Profile id to apply: {id.Value}");
                return id;
            }
            catch (Exception ex)
            {
                Logger.LogError($"{LogPrefix} Failed to get profile for theme: {ex.Message}");
                return null;
            }
        }
    }
}
