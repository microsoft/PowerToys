// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerDisplay.Models;
using Settings.UI.Library;

namespace PowerDisplay.Services
{
    internal static class LightSwitchService
    {
        private const string LogPrefix = "[LightSwitch]";

        public static void MigrateLegacyProfileReferences(PowerDisplayProfiles profiles)
        {
            ArgumentNullException.ThrowIfNull(profiles);

            try
            {
                var settings = SettingsUtils.Default.GetSettingsOrDefault<LightSwitchSettings>(
                    LightSwitchSettings.ModuleName);

                if (!LightSwitchProfileReferenceHelper.ReconcileReferences(
                    settings.Properties,
                    profiles))
                {
                    return;
                }

                SettingsUtils.Default.SaveSettings(
                    settings.ToJsonString(),
                    LightSwitchSettings.ModuleName);
                Logger.LogInfo($"{LogPrefix} Migrated legacy profile references to ids");
            }
            catch (Exception ex)
            {
                Logger.LogError($"{LogPrefix} Failed to migrate legacy profile references: {ex.Message}");
            }
        }

        public static int? GetProfileIdForTheme(bool isLightMode)
        {
            try
            {
                var settings = SettingsUtils.Default.GetSettingsOrDefault<LightSwitchSettings>(
                    LightSwitchSettings.ModuleName);
                var profileId = LightSwitchProfileReferenceHelper.GetProfileIdForTheme(
                    settings.Properties,
                    isLightMode);

                if (profileId is null)
                {
                    Logger.LogTrace(
                        $"{LogPrefix} No enabled profile id configured for {(isLightMode ? "light" : "dark")} mode");
                    return null;
                }

                Logger.LogInfo($"{LogPrefix} Profile id to apply: {profileId.Value}");
                return profileId;
            }
            catch (Exception ex)
            {
                Logger.LogError($"{LogPrefix} Failed to get profile for theme: {ex.Message}");
                return null;
            }
        }
    }
}
