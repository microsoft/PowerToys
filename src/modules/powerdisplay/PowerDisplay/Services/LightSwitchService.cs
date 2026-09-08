// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
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

                ReconcileAndSaveReferences(settings, profiles);
            }
            catch (Exception ex)
            {
                Logger.LogError($"{LogPrefix} Failed to migrate legacy profile references: {ex.Message}");
            }
        }

        public static async Task<Guid?> GetProfileIdForThemeAsync(bool isLightMode)
        {
            try
            {
                // A theme event may arrive before monitor discovery migrates the legacy
                // settings. Resolve against the store's committed UUID mapping here too.
                var profiles = await ProfileHelper.LoadProfilesAsync();
                var settings = SettingsUtils.Default.GetSettingsOrDefault<LightSwitchSettings>(
                    LightSwitchSettings.ModuleName);
                ReconcileAndSaveReferences(settings, profiles);
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

        private static void ReconcileAndSaveReferences(LightSwitchSettings settings, PowerDisplayProfiles profiles)
        {
            if (!LightSwitchProfileReferenceHelper.ReconcileReferences(settings.Properties, profiles))
            {
                return;
            }

            try
            {
                SettingsUtils.Default.SaveSettings(settings.ToJsonString(), LightSwitchSettings.ModuleName);
                Logger.LogInfo($"{LogPrefix} Migrated legacy profile references to UUIDs");
            }
            catch (Exception ex)
            {
                // Keep the resolved in-memory choice for this theme event. The persisted
                // legacy reference can be resolved again from profiles.json on the next run.
                Logger.LogError($"{LogPrefix} Failed to save migrated profile references: {ex.Message}");
            }
        }
    }
}
