// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;

using ManagedCommon;
using PowerToys.Interop;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Propagates the General "theme-adaptive tray icon" master setting into module settings files
    /// so Awake, ZoomIt, PowerDisplay, and Mouse Without Borders pick it up. Command Palette keeps
    /// its own independent setting.
    /// </summary>
    public static class ThemeAdaptiveTrayIconFanOut
    {
        private const string SnakeCasePropertyName = "show_theme_adaptive_tray_icon";
        private const string PascalCasePropertyName = "ShowThemeAdaptiveTrayIcon";

        /// <summary>
        /// Writes <paramref name="themeAdaptive"/> into each PowerToys Settings–managed module that
        /// has a settings file, then notifies running modules when possible.
        /// </summary>
        /// <param name="themeAdaptive">Whether theme-adaptive tray icons should be enabled.</param>
        /// <param name="sendConfigMsg">Optional IPC callback used by the Settings UI (may be null in tests).</param>
        public static void ApplyToModules(bool themeAdaptive, Func<string, int> sendConfigMsg)
        {
            try
            {
                ApplyAwake(themeAdaptive, sendConfigMsg);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to fan out theme-adaptive tray icon to Awake.", ex);
            }

            try
            {
                ApplyZoomIt(themeAdaptive, sendConfigMsg);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to fan out theme-adaptive tray icon to ZoomIt.", ex);
            }

            try
            {
                ApplyPowerDisplay(themeAdaptive);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to fan out theme-adaptive tray icon to PowerDisplay.", ex);
            }

            try
            {
                ApplyMouseWithoutBorders(themeAdaptive);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to fan out theme-adaptive tray icon to Mouse Without Borders.", ex);
            }
        }

        /// <summary>
        /// Returns the ZoomIt settings.json path when the theme-adaptive property was patched,
        /// so the Settings UI can push it through ZoomItSettingsInterop.SaveSettingsJson.
        /// </summary>
        public static string TryGetPatchedZoomItSettingsPath()
        {
            var settingsUtils = SettingsUtils.Default;
            if (!settingsUtils.SettingsExists(ZoomItSettings.ModuleName))
            {
                return null;
            }

            return settingsUtils.GetSettingsFilePath(ZoomItSettings.ModuleName);
        }

        private static void ApplyAwake(bool themeAdaptive, Func<string, int> sendConfigMsg)
        {
            if (!PatchModuleSettingsFile(AwakeSettings.ModuleName, SnakeCasePropertyName, themeAdaptive, wrapAsBoolProperty: false))
            {
                return;
            }

            // Awake watches settings.json; also push IPC when Settings UI is available so a running
            // process applies the change immediately even if the watcher is delayed.
            if (sendConfigMsg == null)
            {
                return;
            }

            var settingsJson = File.ReadAllText(SettingsUtils.Default.GetSettingsFilePath(AwakeSettings.ModuleName));
            sendConfigMsg("{\"powertoys\":{\"Awake\":" + settingsJson + "}}");
        }

        private static void ApplyZoomIt(bool themeAdaptive, Func<string, int> sendConfigMsg)
        {
            if (!PatchModuleSettingsFile(ZoomItSettings.ModuleName, PascalCasePropertyName, themeAdaptive, wrapAsBoolProperty: true))
            {
                return;
            }

            // Registry application via ZoomItSettingsInterop happens in Settings.UI (GeneralViewModel).
            // ShowThemeAdaptiveTrayIcon is honored once the ZoomIt module PR's RegSettings entry is present.
            sendConfigMsg?.Invoke("{\"action\":{\"ZoomIt\":{\"action_name\":\"refresh_settings\", \"value\":\"\"}}}");
        }

        private static void ApplyPowerDisplay(bool themeAdaptive)
        {
            if (!PatchModuleSettingsFile(PowerDisplaySettings.ModuleName, SnakeCasePropertyName, themeAdaptive, wrapAsBoolProperty: false))
            {
                return;
            }

            // PowerDisplay.exe refreshes the tray when this named event is signaled.
            SignalNamedEvent(Constants.SettingsUpdatedPowerDisplayEvent());
        }

        private static void ApplyMouseWithoutBorders(bool themeAdaptive)
        {
            // MWB watches settings.json and applies ShowThemeAdaptiveTrayIcon from file changes.
            PatchModuleSettingsFile(MouseWithoutBordersSettings.ModuleName, SnakeCasePropertyName, themeAdaptive, wrapAsBoolProperty: true);
        }

        /// <summary>
        /// Patches an existing module settings.json. Returns false when the file does not exist yet
        /// (module never configured) so we do not create incomplete settings stubs.
        /// </summary>
        private static bool PatchModuleSettingsFile(string moduleName, string propertyName, bool value, bool wrapAsBoolProperty)
        {
            var settingsUtils = SettingsUtils.Default;
            if (!settingsUtils.SettingsExists(moduleName))
            {
                Logger.LogInfo($"Skipping theme-adaptive tray fan-out for {moduleName}: settings file not found.");
                return false;
            }

            var path = settingsUtils.GetSettingsFilePath(moduleName);
            var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            if (root == null)
            {
                Logger.LogWarning($"Skipping theme-adaptive tray fan-out for {moduleName}: settings JSON was not an object.");
                return false;
            }

            var properties = root["properties"] as JsonObject;
            if (properties == null)
            {
                properties = new JsonObject();
                root["properties"] = properties;
            }

            if (wrapAsBoolProperty)
            {
                properties[propertyName] = new JsonObject { ["value"] = value };
            }
            else
            {
                properties[propertyName] = value;
            }

            settingsUtils.SaveSettings(root.ToJsonString(), moduleName);
            Logger.LogInfo($"Fanned out show_theme_adaptive_tray_icon={value} to {moduleName}.");
            return true;
        }

        private static void SignalNamedEvent(string eventName)
        {
            try
            {
                using var handle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
                handle.Set();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to signal {eventName} for theme-adaptive tray fan-out.", ex);
            }
        }
    }
}
