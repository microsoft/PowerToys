// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Awake.Core.Models;
using Awake.Core.Native;
using ManagedCommon;

namespace Awake.Core
{
    /// <summary>
    /// Manager for handling lid close action override to prevent sleep when laptop lid is closed.
    /// </summary>
    internal static class LidOverrideManager
    {
        // Crash-recovery state has different lifecycle requirements than user settings:
        // it must survive abnormal termination and is deleted on successful restore.
        // Using a direct path rather than SettingsUtils for this reason.
        // NOTE: Not managed by SettingsUtils — must be explicitly cleaned up on module uninstall.
        private static readonly string StateFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys",
            "Awake",
            "LidOverrideState.json");

        private static readonly object _lock = new();
        private static LidOverrideState? _currentState;
        private static bool _isInitialized;
        private static bool _lidPresent;

        // Mutable copies of the constant GUIDs — required because P/Invoke takes ref Guid.
        private static Guid _subGroupGuid = Native.Constants.GUID_SYSTEM_BUTTON_SUBGROUP;
        private static Guid _lidCloseGuid = Native.Constants.GUID_LIDCLOSE_ACTION;

        /// <summary>
        /// Gets a value indicating whether the lid override is currently active.
        /// </summary>
        internal static bool IsOverrideActive
        {
            get
            {
                lock (_lock)
                {
                    return _currentState?.IsOverrideActive ?? false;
                }
            }
        }

        /// <summary>
        /// Initializes the lid override manager. Checks for and recovers from previous crashes.
        /// </summary>
        /// <param name="lidPresent">Whether a lid is present on the device.</param>
        internal static void Initialize(bool lidPresent)
        {
            lock (_lock)
            {
                if (_isInitialized)
                {
                    return;
                }

                _isInitialized = true;
                _lidPresent = lidPresent;

                if (!lidPresent)
                {
                    Logger.LogInfo("Lid not present on device. LidOverrideManager will be inactive.");
                    return;
                }

                try
                {
                    // Check for orphaned state from previous crash
                    if (File.Exists(StateFilePath))
                    {
                        Logger.LogInfo("Found existing lid override state file. Checking for crash recovery...");
                        string json = File.ReadAllText(StateFilePath);
                        var savedState = JsonSerializer.Deserialize<LidOverrideState>(json);

                        // Validate the deserialized state.
                        // Valid lid close actions: 0=DoNothing, 1=Sleep, 2=Hibernate, 3=Shutdown.
                        // Bounds check also serves as defense against tampered state files.
                        if (savedState?.IsOverrideActive == true &&
                            savedState.SchemeGuid != Guid.Empty &&
                            savedState.OriginalAcValue <= 3 &&
                            savedState.OriginalDcValue <= 3)
                        {
                            Logger.LogInfo("Detected orphaned lid override from previous session. Restoring original settings...");
                            _currentState = savedState;
                            RestoreLidSettingsInternal();
                        }
                        else
                        {
                            // Invalid or inactive state file, clean it up
                            TryDeleteStateFile();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error during LidOverrideManager initialization: {ex.Message}");

                    // Clean up potentially corrupt state file
                    TryDeleteStateFile();
                }
            }
        }

        /// <summary>
        /// Applies the lid override to prevent sleep when the lid is closed.
        /// </summary>
        /// <returns>True if successful, false otherwise.</returns>
        internal static bool ApplyLidOverride()
        {
            lock (_lock)
            {
                return ApplyLidOverrideInternal();
            }
        }

        /// <summary>
        /// Re-applies the lid override after a power event (e.g., resume from suspend).
        /// Only re-writes the "Do nothing" values; does not update saved original values.
        /// </summary>
        internal static void ReapplyLidOverride()
        {
            lock (_lock)
            {
                if (_currentState == null || !_currentState.IsOverrideActive)
                {
                    return;
                }

                try
                {
                    if (!TryGetActiveSchemeGuid(out Guid schemeGuid))
                    {
                        return;
                    }

                    WriteLidCloseAction(ref schemeGuid, Native.Constants.LID_ACTION_DO_NOTHING, Native.Constants.LID_ACTION_DO_NOTHING);

                    Logger.LogInfo("Lid override reapplied after power event.");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to reapply lid override: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Restores the original lid close settings.
        /// </summary>
        /// <returns>True if successful, false otherwise.</returns>
        internal static bool RestoreLidSettings()
        {
            lock (_lock)
            {
                return RestoreLidSettingsInternal();
            }
        }

        /// <summary>
        /// Retrieves the active power scheme GUID via the Win32 API.
        /// Handles allocation, marshaling, and cleanup of the native pointer.
        /// </summary>
        private static bool TryGetActiveSchemeGuid(out Guid schemeGuid)
        {
            schemeGuid = Guid.Empty;

            uint result = Bridge.PowerGetActiveScheme(IntPtr.Zero, out IntPtr activePolicyGuid);
            if (result != Native.Constants.ERROR_SUCCESS)
            {
                if (activePolicyGuid != IntPtr.Zero)
                {
                    Bridge.LocalFree(activePolicyGuid);
                }

                Logger.LogError($"Failed to get active power scheme. Error code: {result}");
                return false;
            }

            if (activePolicyGuid == IntPtr.Zero)
            {
                Logger.LogError("PowerGetActiveScheme succeeded but returned null pointer.");
                return false;
            }

            schemeGuid = Marshal.PtrToStructure<Guid>(activePolicyGuid);
            Bridge.LocalFree(activePolicyGuid);
            return true;
        }

        /// <summary>
        /// Writes AC and DC lid close action values and activates the power scheme.
        /// Logs errors but does not throw. Returns true only if all three writes succeed.
        /// </summary>
        private static bool WriteLidCloseAction(ref Guid schemeGuid, uint acValue, uint dcValue)
        {
            bool allSucceeded = true;

            uint result = Bridge.PowerWriteACValueIndex(
                IntPtr.Zero, ref schemeGuid, ref _subGroupGuid, ref _lidCloseGuid, acValue);
            if (result != Native.Constants.ERROR_SUCCESS)
            {
                Logger.LogError($"Failed to write AC lid close value. Error code: {result}");
                allSucceeded = false;
            }

            result = Bridge.PowerWriteDCValueIndex(
                IntPtr.Zero, ref schemeGuid, ref _subGroupGuid, ref _lidCloseGuid, dcValue);
            if (result != Native.Constants.ERROR_SUCCESS)
            {
                Logger.LogError($"Failed to write DC lid close value. Error code: {result}");
                allSucceeded = false;
            }

            result = Bridge.PowerSetActiveScheme(IntPtr.Zero, ref schemeGuid);
            if (result != Native.Constants.ERROR_SUCCESS)
            {
                Logger.LogError($"Failed to apply power scheme changes. Error code: {result}");
                allSucceeded = false;
            }

            return allSucceeded;
        }

        private static bool ApplyLidOverrideInternal()
        {
            if (!_isInitialized || !_lidPresent)
            {
                Logger.LogInfo("LidOverrideManager not initialized or no lid present. Skipping apply.");
                return false;
            }

            try
            {
                if (!TryGetActiveSchemeGuid(out Guid schemeGuid))
                {
                    return false;
                }

                // Read current AC and DC values
                uint result = Bridge.PowerReadACValueIndex(
                    IntPtr.Zero,
                    ref schemeGuid,
                    ref _subGroupGuid,
                    ref _lidCloseGuid,
                    out uint originalAcValue);

                if (result != Native.Constants.ERROR_SUCCESS)
                {
                    Logger.LogError($"Failed to read AC lid close value. Error code: {result}");
                    return false;
                }

                result = Bridge.PowerReadDCValueIndex(
                    IntPtr.Zero,
                    ref schemeGuid,
                    ref _subGroupGuid,
                    ref _lidCloseGuid,
                    out uint originalDcValue);

                if (result != Native.Constants.ERROR_SUCCESS)
                {
                    Logger.LogError($"Failed to read DC lid close value. Error code: {result}");
                    return false;
                }

                // Set lid close action to "Do nothing" for both AC and DC
                if (!WriteLidCloseAction(ref schemeGuid, Native.Constants.LID_ACTION_DO_NOTHING, Native.Constants.LID_ACTION_DO_NOTHING))
                {
                    // Rollback: restore original values on failure
                    if (!WriteLidCloseAction(ref schemeGuid, originalAcValue, originalDcValue))
                    {
                        Logger.LogError("Rollback of lid override also failed. Power plan lid settings may be inconsistent.");
                    }

                    return false;
                }

                // Only save state AFTER all writes succeed
                _currentState = new LidOverrideState
                {
                    IsOverrideActive = true,
                    OriginalAcValue = originalAcValue,
                    OriginalDcValue = originalDcValue,
                    SchemeGuid = schemeGuid,
                };

                SaveState();

                Logger.LogInfo($"Lid override applied successfully. Original AC: {originalAcValue}, DC: {originalDcValue}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Exception applying lid override: {ex.Message}");
                return false;
            }
        }

        private static bool RestoreLidSettingsInternal()
        {
            if (_currentState == null || !_currentState.IsOverrideActive)
            {
                Logger.LogInfo("No active lid override to restore.");
                TryDeleteStateFile();
                return true;
            }

            try
            {
                Guid savedSchemeGuid = _currentState.SchemeGuid;
                if (savedSchemeGuid == Guid.Empty)
                {
                    Logger.LogError("Empty scheme GUID in state.");
                    TryDeleteStateFile();
                    _currentState = null;
                    return false;
                }

                // Determine which scheme to restore to. If the user changed their power plan
                // while the override was active, restore to the currently active scheme instead
                // of the saved one, since the saved scheme may no longer be active.
                Guid schemeGuid = savedSchemeGuid;
                if (TryGetActiveSchemeGuid(out Guid activeSchemeGuid))
                {
                    if (activeSchemeGuid != savedSchemeGuid)
                    {
                        Logger.LogInfo($"Active power scheme ({activeSchemeGuid}) differs from saved scheme ({savedSchemeGuid}). Restoring to active scheme.");
                        schemeGuid = activeSchemeGuid;
                    }
                }
                else
                {
                    Logger.LogError("Failed to get active power scheme during restore. Falling back to saved scheme.");
                }

                bool allSucceeded = WriteLidCloseAction(ref schemeGuid, _currentState.OriginalAcValue, _currentState.OriginalDcValue);


                if (allSucceeded)
                {
                    Logger.LogInfo($"Lid settings restored. AC: {_currentState.OriginalAcValue}, DC: {_currentState.OriginalDcValue}");
                    _currentState = null;
                    TryDeleteStateFile();
                }
                else
                {
                    Logger.LogError("Partial failure restoring lid settings. State file preserved for retry on next launch.");

                    // Mark override as inactive but preserve original values for potential
                    // in-session retry. Setting _currentState = null would cause the next
                    // ApplyLidOverride to read partially-restored values as "originals."
                    _currentState.IsOverrideActive = false;
                }

                return allSucceeded;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Exception restoring lid settings: {ex.Message}. State file preserved for retry on next launch.");
                return false;
            }
        }

        private static void SaveState()
        {
            try
            {
                string? directory = Path.GetDirectoryName(StateFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string tempPath = StateFilePath + ".tmp";
                string json = JsonSerializer.Serialize(_currentState);
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, StateFilePath, overwrite: true);
                Logger.LogInfo("Lid override state saved.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to save lid override state: {ex.Message}");
            }
        }

        private static void TryDeleteStateFile()
        {
            try
            {
                File.Delete(StateFilePath);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to delete lid override state file: {ex.Message}");
            }
        }
    }
}
