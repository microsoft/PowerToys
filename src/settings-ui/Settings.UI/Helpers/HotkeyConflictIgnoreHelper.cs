// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Views;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// Static helper class to manage and check hotkey conflict ignore settings
    /// </summary>
    public static class HotkeyConflictIgnoreHelper
    {
        private static readonly ISettingsRepository<GeneralSettings> _generalSettingsRepository;
        private static readonly ISettingsUtils _settingsUtils;

        static HotkeyConflictIgnoreHelper()
        {
            _settingsUtils = SettingsUtils.Default;
            _generalSettingsRepository = SettingsRepository<GeneralSettings>.GetInstance(_settingsUtils);
        }

        /// <summary>
        /// Ensures ignored conflict properties are initialized
        /// </summary>
        private static void EnsureInitialized()
        {
            var settings = _generalSettingsRepository.SettingsConfig;
            if (settings.IgnoredConflictProperties == null)
            {
                settings.IgnoredConflictProperties = new ShortcutConflictProperties();
                SaveSettings();
            }
        }

        /// <summary>
        /// Checks if a specific hotkey setting is configured to ignore conflicts
        /// </summary>
        /// <param name="hotkeySettings">The hotkey settings to check</param>
        /// <returns>True if the hotkey is set to ignore conflicts, false otherwise</returns>
        public static bool IsIgnoringConflicts(HotkeySettings hotkeySettings)
        {
            if (hotkeySettings == null)
            {
                return false;
            }

            try
            {
                EnsureInitialized();
                var settings = _generalSettingsRepository.SettingsConfig;
                return settings.IgnoredConflictProperties.IgnoredShortcuts
                    .Any(h => AreHotkeySettingsEqual(h, hotkeySettings));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error checking if hotkey is ignoring conflicts: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Adds a hotkey setting to the ignored shortcuts list
        /// </summary>
        /// <param name="hotkeySettings">The hotkey settings to add to the ignored list</param>
        /// <returns>True if successfully added, false if it was already ignored or on error</returns>
        public static bool AddToIgnoredList(HotkeySettings hotkeySettings)
        {
            if (hotkeySettings == null)
            {
                return false;
            }

            try
            {
                EnsureInitialized();
                var settings = _generalSettingsRepository.SettingsConfig;

                // Check if already ignored (avoid duplicates)
                if (IsIgnoringConflicts(hotkeySettings))
                {
                    Logger.LogInfo($"Hotkey already in ignored list: {hotkeySettings}");
                    return false;
                }

                // Add to ignored list
                settings.IgnoredConflictProperties.IgnoredShortcuts.Add(hotkeySettings);
                SaveSettings();

                Logger.LogInfo($"Added hotkey to ignored list: {hotkeySettings}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding hotkey to ignored list: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Removes a hotkey setting from the ignored shortcuts list
        /// </summary>
        /// <param name="hotkeySettings">The hotkey settings to remove from the ignored list</param>
        /// <returns>True if successfully removed, false if it wasn't in the list or on error</returns>
        public static bool RemoveFromIgnoredList(HotkeySettings hotkeySettings)
        {
            if (hotkeySettings == null)
            {
                return false;
            }

            try
            {
                EnsureInitialized();
                var settings = _generalSettingsRepository.SettingsConfig;
                var ignoredShortcut = settings.IgnoredConflictProperties.IgnoredShortcuts
                    .FirstOrDefault(h => AreHotkeySettingsEqual(h, hotkeySettings));

                if (ignoredShortcut != null)
                {
                    settings.IgnoredConflictProperties.IgnoredShortcuts.Remove(ignoredShortcut);
                    SaveSettings();

                    Logger.LogInfo($"Removed hotkey from ignored list: {ignoredShortcut}");
                    return true;
                }

                Logger.LogInfo($"Hotkey not found in ignored list: {hotkeySettings}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error removing hotkey from ignored list: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets all hotkey settings that are currently being ignored
        /// </summary>
        /// <returns>List of ignored hotkey settings</returns>
        public static List<HotkeySettings> GetAllIgnoredShortcuts()
        {
            try
            {
                EnsureInitialized();
                var settings = _generalSettingsRepository.SettingsConfig;
                return new List<HotkeySettings>(settings.IgnoredConflictProperties.IgnoredShortcuts);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting ignored shortcuts: {ex.Message}");
                return new List<HotkeySettings>();
            }
        }

        /// <summary>
        /// Clears all ignored shortcuts from the list
        /// </summary>
        /// <returns>True if successfully cleared, false on error</returns>
        public static bool ClearAllIgnoredShortcuts()
        {
            try
            {
                EnsureInitialized();
                var settings = _generalSettingsRepository.SettingsConfig;
                var count = settings.IgnoredConflictProperties.IgnoredShortcuts.Count;
                settings.IgnoredConflictProperties.IgnoredShortcuts.Clear();
                SaveSettings();

                Logger.LogInfo($"Cleared all {count} ignored shortcuts");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error clearing ignored shortcuts: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Compares two HotkeySettings for equality
        /// </summary>
        /// <param name="hotkey1">First hotkey settings</param>
        /// <param name="hotkey2">Second hotkey settings</param>
        /// <returns>True if they represent the same shortcut, false otherwise</returns>
        private static bool AreHotkeySettingsEqual(HotkeySettings hotkey1, HotkeySettings hotkey2)
        {
            if (hotkey1 == null || hotkey2 == null)
            {
                return false;
            }

            return hotkey1.Win == hotkey2.Win &&
                   hotkey1.Ctrl == hotkey2.Ctrl &&
                   hotkey1.Alt == hotkey2.Alt &&
                   hotkey1.Shift == hotkey2.Shift &&
                   hotkey1.Code == hotkey2.Code;
        }

        /// <summary>
        /// Saves the general settings using PowerToys standard settings persistence
        /// </summary>
        private static void SaveSettings()
        {
            try
            {
                var settings = _generalSettingsRepository.SettingsConfig;

                // Send IPC message to notify runner of changes (this is thread-safe)
                var outgoing = new OutGoingGeneralSettings(settings);
                ShellPage.SendDefaultIPCMessage(outgoing.ToString());
                ShellPage.ShellHandler?.SignalGeneralDataUpdate();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error saving shortcut conflict settings: {ex.Message}");
                Logger.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}
