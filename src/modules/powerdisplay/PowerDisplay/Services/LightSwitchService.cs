// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
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
        /// Get the profile name to apply for the given theme.
        /// </summary>
        /// <param name="isLightMode">Whether the theme changed to light mode.</param>
        /// <returns>The profile name to apply, or null if no profile is configured.</returns>
        public static string? GetProfileForTheme(bool isLightMode)
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

                string? profileName;
                if (isLightMode)
                {
                    if (!settings.Properties.EnableLightModeProfile.Value)
                    {
                        Logger.LogInfo($"{LogPrefix} Light mode profile is disabled");
                        return null;
                    }

                    profileName = settings.Properties.LightModeProfile.Value;
                }
                else
                {
                    if (!settings.Properties.EnableDarkModeProfile.Value)
                    {
                        Logger.LogInfo($"{LogPrefix} Dark mode profile is disabled");
                        return null;
                    }

                    profileName = settings.Properties.DarkModeProfile.Value;
                }

                if (string.IsNullOrEmpty(profileName) || profileName == "(None)")
                {
                    Logger.LogInfo($"{LogPrefix} No profile configured for {(isLightMode ? "light" : "dark")} mode");
                    return null;
                }

                Logger.LogInfo($"{LogPrefix} Profile to apply: {profileName}");
                return profileName;
            }
            catch (Exception ex)
            {
                Logger.LogError($"{LogPrefix} Failed to get profile for theme: {ex.Message}");
                return null;
            }
        }
    }
}
