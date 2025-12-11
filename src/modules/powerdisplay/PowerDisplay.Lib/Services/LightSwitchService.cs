// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using ManagedCommon;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Service for handling LightSwitch theme change events.
    /// Provides methods to process theme changes and read LightSwitch settings.
    /// Event listening is handled externally via NativeEventWaiter.
    /// </summary>
    public static class LightSwitchService
    {
        private const string LogPrefix = "[LightSwitch]";

        /// <summary>
        /// Process a theme change event and return the profile name to apply.
        /// </summary>
        /// <param name="isLightMode">Whether the theme changed to light mode.</param>
        /// <returns>The profile name to apply, or null if no profile is configured.</returns>
        public static string? GetProfileForTheme(bool isLightMode)
        {
            try
            {
                Logger.LogInfo($"{LogPrefix} Processing theme change to {(isLightMode ? "light" : "dark")} mode");

                var profileToApply = ReadProfileFromLightSwitchSettings(isLightMode);

                if (string.IsNullOrEmpty(profileToApply) || profileToApply == "(None)")
                {
                    Logger.LogInfo($"{LogPrefix} No profile configured for {(isLightMode ? "light" : "dark")} mode");
                    return null;
                }

                Logger.LogInfo($"{LogPrefix} Profile to apply: {profileToApply}");
                return profileToApply;
            }
            catch (Exception ex)
            {
                Logger.LogError($"{LogPrefix} Failed to process theme change: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads LightSwitch settings and returns the profile name to apply for the given theme.
        /// </summary>
        /// <param name="isLightMode">Whether the theme is light mode.</param>
        /// <returns>The profile name to apply, or null if not configured.</returns>
        private static string? ReadProfileFromLightSwitchSettings(bool isLightMode)
        {
            var settingsPath = PathConstants.LightSwitchSettingsFilePath;

            if (!File.Exists(settingsPath))
            {
                Logger.LogWarning($"{LogPrefix} LightSwitch settings file not found");
                return null;
            }

            var json = File.ReadAllText(settingsPath);
            var settings = JsonDocument.Parse(json);
            var root = settings.RootElement;

            if (!root.TryGetProperty("properties", out var properties))
            {
                Logger.LogWarning($"{LogPrefix} LightSwitch settings has no properties");
                return null;
            }

            // Check if monitor settings integration is enabled
            if (!properties.TryGetProperty("apply_monitor_settings", out var applyMonitorSettingsElement) ||
                !applyMonitorSettingsElement.TryGetProperty("value", out var applyValue) ||
                !applyValue.GetBoolean())
            {
                Logger.LogInfo($"{LogPrefix} Monitor settings integration is disabled");
                return null;
            }

            // Get the appropriate profile name based on the theme
            if (isLightMode)
            {
                return GetProfileFromSettings(properties, "enable_light_mode_profile", "light_mode_profile");
            }
            else
            {
                return GetProfileFromSettings(properties, "enable_dark_mode_profile", "dark_mode_profile");
            }
        }

        private static string? GetProfileFromSettings(
            JsonElement properties,
            string enableKey,
            string profileKey)
        {
            if (properties.TryGetProperty(enableKey, out var enableElement) &&
                enableElement.TryGetProperty("value", out var enableValue) &&
                enableValue.GetBoolean() &&
                properties.TryGetProperty(profileKey, out var profileElement) &&
                profileElement.TryGetProperty("value", out var profileValue))
            {
                return profileValue.GetString();
            }

            return null;
        }
    }
}
