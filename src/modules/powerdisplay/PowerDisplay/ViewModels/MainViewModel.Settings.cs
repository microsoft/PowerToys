// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using PowerDisplay.Common.Utils;
using PowerDisplay.Contracts;
using PowerDisplay.Ipc;
using PowerDisplay.Models;
using PowerDisplay.Serialization;
using PowerDisplay.Services;
using PowerDisplay.Telemetry.Events;
using PowerToys.Interop;

namespace PowerDisplay.ViewModels;

/// <summary>
/// MainViewModel - Settings UI synchronization and Profile management methods
/// </summary>
public partial class MainViewModel
{
    /// <summary>
    /// Persist the link-levels toggle state to settings.json. Called from
    /// <c>OnLinkedLevelsActiveChanged</c> in <c>MainViewModel.LinkedBrightness.cs</c>
    /// (which owns the source-generator hook plus the broadcast/seed side effects).
    /// </summary>
    private void SaveLinkedLevelsActive(bool value)
    {
        try
        {
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
            if (settings.Properties.LinkedLevelsActive == value)
            {
                return;
            }

            settings.Properties.LinkedLevelsActive = value;

            _settingsUtils.SaveSettings(
                System.Text.Json.JsonSerializer.Serialize(settings, AppJsonContext.Default.PowerDisplaySettings),
                PowerDisplaySettings.ModuleName);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Settings] Failed to save LinkedLevelsActive: {ex.Message}");
        }
    }

    /// <summary>
    /// Persist the linked-brightness exclusion set to settings.json. Called from
    /// <c>SetMonitorExcludedFromSync</c> in <c>MainViewModel.LinkedBrightness.cs</c> after the
    /// runtime <c>_excludedMonitorIds</c> set has been mutated.
    /// </summary>
    private void SaveExcludedMonitorIds()
    {
        try
        {
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
            settings.Properties.ExcludedFromSyncMonitorIds = _excludedMonitorIds.ToList();

            _settingsUtils.SaveSettings(
                System.Text.Json.JsonSerializer.Serialize(settings, AppJsonContext.Default.PowerDisplaySettings),
                PowerDisplaySettings.ModuleName);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Settings] Failed to save ExcludedFromSyncMonitorIds: {ex.Message}");
        }
    }

    private static void TryRestore(
        List<Task> tasks,
        int? savedValue,
        bool isVisible,
        int currentValue,
        Func<int, Task> setter)
    {
        if (savedValue.HasValue && isVisible && savedValue.Value != currentValue)
        {
            tasks.Add(setter(savedValue.Value));
        }
    }

    /// <summary>
    /// Outcome-capturing variant of <see cref="TryRestore"/> used by
    /// <see cref="ApplyProfileWithOutcomesInternalAsync"/>. Calls <paramref name="applyAsync"/>
    /// directly on the monitor manager (returning a <see cref="MonitorOperationResult"/>) so that
    /// hardware failures are captured rather than swallowed. Returns a
    /// <see cref="ProfileChangeOutcome"/> (with <c>Value</c>, optional <c>Display</c>, status,
    /// and optional <c>Error</c>) for one setting, or <c>null</c> when the setting was not
    /// present in the profile entry.
    /// </summary>
    /// <param name="savedValue">Profile value for this setting; <c>null</c> means not included.</param>
    /// <param name="supportsHardware">
    /// Hardware-capability flag from the underlying <c>Monitor</c> model (e.g.
    /// <c>monitorVm.SupportsBrightness</c>). Must NOT be a UI-visibility flag
    /// (<c>ShowBrightness</c>) — a setting that the user has hidden in the GUI but the
    /// hardware actually supports must still report <c>applied</c>, matching
    /// <c>ApplyProfileCommand.ApplyContinuousAsync</c>.
    /// </param>
    /// <param name="settingName">Canonical setting name for the outcome row.</param>
    /// <param name="monitorId">Monitor identifier forwarded to <paramref name="applyAsync"/>.</param>
    /// <param name="formatDisplay">
    /// Formatter for the human-readable display string when the write succeeds
    /// (e.g. <c>v =&gt; v + "%"</c> for percentage settings,
    /// <c>v =&gt; MonitorDtoProjector.FormatDiscrete(0x14, v)</c> for color-temperature).
    /// </param>
    /// <param name="applyAsync">Hardware-write delegate returning a <see cref="MonitorOperationResult"/>.</param>
    private static async Task<ProfileChangeOutcome?> TryRestoreWithOutcomeAsync(
        int? savedValue,
        bool supportsHardware,
        string settingName,
        string monitorId,
        Func<int, string?> formatDisplay,
        Func<string, int, CancellationToken, Task<MonitorOperationResult>> applyAsync)
    {
        if (!savedValue.HasValue)
        {
            // Setting not included in this profile entry — skip silently (no outcome row).
            return null;
        }

        int value = savedValue.Value;

        if (!supportsHardware)
        {
            // Hardware does not support this setting — report unsupported regardless of whether
            // it is hidden in the GUI (matches ApplyProfileCommand.ApplyContinuousAsync check on
            // monitor.SupportsBrightness / SupportsContrast / SupportsVolume / SupportsColorTemperature).
            return new ProfileChangeOutcome(settingName, value, Display: null, CliProfileChange.StatusUnsupported, Error: null);
        }

        // Basic range guard matching ApplyProfileCommand.ApplyContinuousAsync (0–100 for
        // percentage-based settings; 0x00–0xFF for VCP color-temperature).
        // color-temperature is the only non-percentage setting in profiles; it uses VCP byte range.
        bool outOfRange = settingName == "color-temperature"
            ? value is < 0 or > 0xFF
            : value is < 0 or > 100;

        if (outOfRange)
        {
            return new ProfileChangeOutcome(settingName, value, Display: null, CliProfileChange.StatusOutOfRange, Error: null);
        }

        try
        {
            var result = await applyAsync(monitorId, value, default);
            if (result.IsSuccess)
            {
                return new ProfileChangeOutcome(settingName, value, Display: formatDisplay(value), CliProfileChange.StatusApplied, Error: null);
            }
            else
            {
                return new ProfileChangeOutcome(settingName, value, Display: null, CliProfileChange.StatusHardwareFailure, Error: result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[Profile] Hardware write failed for {settingName}: {ex.Message}");
            return new ProfileChangeOutcome(settingName, value, Display: null, CliProfileChange.StatusHardwareFailure, Error: ex.Message);
        }
    }

    /// <summary>
    /// Apply settings changes from Settings UI (IPC event handler entry point)
    /// Only applies UI configuration changes. Hardware parameter changes (e.g., color temperature)
    /// should be triggered via custom actions to avoid unwanted side effects when non-hardware
    /// settings (like RestoreSettingsOnStartup) are changed.
    /// </summary>
    public void ApplySettingsFromUI()
    {
        try
        {
            // Rebuild monitor list with updated hidden monitor settings
            // UpdateMonitorList already handles filtering hidden monitors
            UpdateMonitorList(_monitorManager.Monitors, isInitialLoad: false);

            // Reload UI display settings first (includes custom VCP mappings)
            // Must be loaded before ApplyUIConfiguration so names are available for UI refresh
            LoadUIDisplaySettings();

            // Apply UI configuration changes only (feature visibility toggles, etc.)
            // Hardware parameters (brightness, color temperature) are applied via custom actions
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
            ApplyUIConfiguration(settings);

            // Push the toggle to the DDC/CI controller so the next refresh / hot-plug
            // discovery picks it up. The value is also re-read inside InitializeAsync /
            // RefreshMonitorsAsync, so this is a no-op-safe redundant push.
            _monitorManager.SetMaxCompatibilityMode(settings.Properties.MaxCompatibilityMode);

            // Reload profiles in case they were added/updated/deleted in Settings UI
            LoadProfiles();

            // Notify MonitorViewModels to refresh their custom VCP name displays
            foreach (var monitor in Monitors)
            {
                monitor.RefreshCustomVcpNames();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Settings] Failed to apply settings from UI: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply UI-only configuration changes (feature visibility toggles)
    /// Synchronous, lightweight operation
    /// </summary>
    private void ApplyUIConfiguration(PowerDisplaySettings settings)
    {
        try
        {
            foreach (var monitorVm in Monitors)
            {
                ApplyFeatureVisibility(monitorVm, settings);
            }

            // Trigger UI refresh
            UIRefreshRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Settings] Failed to apply UI configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply profile by name (called via Named Pipe from Settings UI).
    /// This is the existing void-equivalent entry point; it preserves GUI behavior unchanged.
    /// </summary>
    /// <param name="profileName">The name of the profile to apply.</param>
    public async Task ApplyProfileByNameAsync(string profileName)
    {
        try
        {
            Logger.LogInfo($"[Profile] Applying profile by name: {profileName}");

            // Load profiles and find the requested one
            var profilesData = ProfileService.LoadProfiles();
            var profile = profilesData.GetProfile(profileName);

            if (profile == null || !profile.IsValid())
            {
                Logger.LogWarning($"[Profile] Profile '{profileName}' not found or invalid");
                return;
            }

            // Apply the profile settings to monitors
            await ApplyProfileAsync(profile.MonitorSettings);
            Logger.LogInfo($"[Profile] Successfully applied profile: {profileName}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Profile] Failed to apply profile '{profileName}': {ex.Message}");
        }
    }

    /// <summary>
    /// Applies a saved profile by name and returns structured per-monitor/per-setting outcomes
    /// for the IPC layer to build a <see cref="PowerDisplay.Contracts.CliApplyProfileResult"/>.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="ApplyProfileByNameAsync"/> (which swallows outcomes), this method
    /// captures success/failure per setting and returns them as a list of
    /// <see cref="ProfileApplyOutcome"/> records. Hardware writes are performed sequentially
    /// per monitor so that outcome ordering matches the profile's monitor list — the original
    /// <see cref="ApplyProfileAsync"/> parallel writes are preserved for the GUI path.
    /// </remarks>
    /// <param name="profileName">The name of the profile to apply.</param>
    /// <returns>
    /// One entry per <see cref="PowerDisplay.Models.ProfileMonitorSetting"/> in the profile.
    /// <see cref="ProfileApplyOutcome.Connected"/> is <c>false</c> for monitors not currently
    /// visible; otherwise <see cref="ProfileApplyOutcome.Changes"/> contains one entry per
    /// setting that was present in the profile entry.
    /// <para>
    /// Returns <c>null</c> when the profile name is unknown (not found or invalid).
    /// The IPC handler (Task 2.5) maps <c>null</c> to a
    /// <see cref="PowerDisplay.Contracts.CliErrorResult"/> with
    /// <c>CliErrorCodes.ArgumentError</c> / <c>CliExitCodes.ArgumentError</c> (exit code 7),
    /// mirroring <c>ApplyProfileCommand.RunAsync</c>.
    /// </para>
    /// </returns>
    public async Task<IReadOnlyList<ProfileApplyOutcome>?> ApplyProfileWithOutcomesAsync(string profileName)
    {
        try
        {
            Logger.LogInfo($"[Profile] Applying profile with outcomes: {profileName}");

            var profilesData = ProfileService.LoadProfiles();
            var profile = profilesData.GetProfile(profileName);

            if (profile == null || !profile.IsValid())
            {
                Logger.LogWarning($"[Profile] Profile '{profileName}' not found or invalid (outcomes path)");
                return null;
            }

            var outcomes = await ApplyProfileWithOutcomesInternalAsync(profile.MonitorSettings);
            Logger.LogInfo($"[Profile] Completed apply with outcomes: {profileName}, {outcomes.Count} monitor(s)");
            return outcomes;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Profile] Failed to apply profile with outcomes '{profileName}': {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Handle theme change from LightSwitch by applying the appropriate profile.
    /// Called from App.xaml.cs when LightSwitch theme events are received.
    /// </summary>
    /// <param name="isLightMode">Whether the theme changed to light mode.</param>
    public void ApplyLightSwitchProfile(bool isLightMode)
    {
        var profileName = LightSwitchService.GetProfileForTheme(isLightMode);

        if (string.IsNullOrEmpty(profileName))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                Logger.LogInfo($"[LightSwitch Integration] Applying profile: {profileName}");

                // Load and apply the profile
                var profilesData = ProfileService.LoadProfiles();
                var profile = profilesData.GetProfile(profileName);

                if (profile == null || !profile.IsValid())
                {
                    Logger.LogWarning($"[LightSwitch Integration] Profile '{profileName}' not found or invalid");
                    return;
                }

                // Apply the profile - need to dispatch to UI thread since MonitorViewModels are UI-bound
                var tcs = new TaskCompletionSource<bool>();
                var enqueued = _dispatcherQueue.TryEnqueue(() =>
                {
                    // Start the async operation and handle completion
                    _ = ApplyProfileAndCompleteAsync(profile.MonitorSettings, tcs);
                });

                if (!enqueued)
                {
                    Logger.LogError($"[LightSwitch Integration] Failed to enqueue profile application to UI thread");
                    return;
                }

                await tcs.Task;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[LightSwitch Integration] Failed to apply profile: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Logger.LogError($"[LightSwitch Integration] Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
            }
        });
    }

    /// <summary>
    /// Helper method to apply profile and signal completion.
    /// </summary>
    private async Task ApplyProfileAndCompleteAsync(List<ProfileMonitorSetting> monitorSettings, TaskCompletionSource<bool> tcs)
    {
        try
        {
            await ApplyProfileAsync(monitorSettings);
            tcs.SetResult(true);
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }
    }

    /// <summary>
    /// Apply profile settings to monitors. Profiles are per-monitor snapshots, so applying one
    /// turns off linked brightness before writing individual monitor values.
    /// <para>
    /// This method is the GUI code path. It is preserved exactly as-is: settings are dispatched
    /// in parallel via <c>Task.WhenAll</c> and no per-setting outcome is captured. All existing
    /// callers (<see cref="ApplyProfileByNameAsync"/>, <see cref="ApplyProfileAndCompleteAsync"/>)
    /// continue to call this overload.
    /// </para>
    /// </summary>
    private async Task ApplyProfileAsync(List<ProfileMonitorSetting> monitorSettings)
    {
        if (LinkedLevelsActive)
        {
            Logger.LogInfo("[Profile] Disabling linked brightness before applying per-monitor profile values");
            LinkedLevelsActive = false;
        }

        var updateTasks = new List<Task>();

        foreach (var setting in monitorSettings)
        {
            // Find monitor by Id (unique identifier)
            var monitorVm = Monitors.FirstOrDefault(m => MonitorIdComparer.Equal(m.Id, setting.MonitorId));

            if (monitorVm == null)
            {
                continue;
            }

            // Apply brightness if included in profile
            TryRestore(updateTasks, setting.Brightness, monitorVm.ShowBrightness, monitorVm.Brightness, monitorVm.SetBrightnessAsync);

            // Apply contrast if supported and value provided
            TryRestore(updateTasks, setting.Contrast, monitorVm.ShowContrast, monitorVm.Contrast, monitorVm.SetContrastAsync);

            // Apply volume if supported and value provided
            TryRestore(updateTasks, setting.Volume, monitorVm.ShowVolume, monitorVm.Volume, monitorVm.SetVolumeAsync);

            // Apply color temperature if included in profile
            TryRestore(updateTasks, setting.ColorTemperatureVcp, monitorVm.ShowColorTemperature, monitorVm.ColorTemperature, monitorVm.SetColorTemperatureAsync);
        }

        // Wait for all updates to complete
        if (updateTasks.Count > 0)
        {
            await Task.WhenAll(updateTasks);
        }
    }

    /// <summary>
    /// Outcome-capturing variant of <see cref="ApplyProfileAsync"/>. Used exclusively by
    /// <see cref="ApplyProfileWithOutcomesAsync"/> (the IPC entry point). Hardware writes are
    /// performed sequentially per monitor (not in parallel) so that each outcome can be
    /// individually captured and attributed to its setting; this is acceptable because the
    /// outcomes path is IPC-driven, not GUI-driven.
    /// <para>
    /// The <see cref="ApplyProfileAsync"/> (GUI) parallel path is NOT touched by this method.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<ProfileApplyOutcome>> ApplyProfileWithOutcomesInternalAsync(
        List<ProfileMonitorSetting> monitorSettings)
    {
        if (LinkedLevelsActive)
        {
            Logger.LogInfo("[Profile] Disabling linked brightness before applying per-monitor profile values (outcomes path)");
            LinkedLevelsActive = false;
        }

        var outcomes = new List<ProfileApplyOutcome>(monitorSettings.Count);

        foreach (var setting in monitorSettings)
        {
            var monitorVm = Monitors.FirstOrDefault(m => MonitorIdComparer.Equal(m.Id, setting.MonitorId));

            if (monitorVm == null)
            {
                // Monitor not currently connected — report disconnected, no changes attempted.
                outcomes.Add(new ProfileApplyOutcome(
                    setting.MonitorId ?? string.Empty,
                    Connected: false,
                    Changes: Array.Empty<ProfileChangeOutcome>()));
                continue;
            }

            // Collect per-setting outcomes sequentially (one at a time, in profile field order).
            // We call _monitorManager directly (rather than MonitorViewModel.SetXxxAsync) so that
            // MonitorOperationResult.IsSuccess is available to distinguish applied vs hardware-failure.
            // MonitorViewModel UI state is NOT updated here — this path is IPC-only.
            //
            // IMPORTANT: Use the hardware-capability flags (monitorVm.SupportsBrightness etc.),
            // NOT the UI-visibility flags (monitorVm.ShowBrightness etc.). A setting that is
            // hidden in the Settings UI but physically supported by the hardware must still
            // report "applied", matching ApplyProfileCommand.ApplyContinuousAsync behavior.
            var monitorId = monitorVm.Id;
            var changes = new List<ProfileChangeOutcome>(4);

            var brightnessOutcome = await TryRestoreWithOutcomeAsync(
                setting.Brightness,
                monitorVm.SupportsBrightness,
                "brightness",
                monitorId,
                v => v + "%",
                _monitorManager.SetBrightnessAsync);
            if (brightnessOutcome.HasValue)
            {
                changes.Add(brightnessOutcome.Value);
            }

            var contrastOutcome = await TryRestoreWithOutcomeAsync(
                setting.Contrast,
                monitorVm.SupportsContrast,
                "contrast",
                monitorId,
                v => v + "%",
                _monitorManager.SetContrastAsync);
            if (contrastOutcome.HasValue)
            {
                changes.Add(contrastOutcome.Value);
            }

            var volumeOutcome = await TryRestoreWithOutcomeAsync(
                setting.Volume,
                monitorVm.SupportsVolume,
                "volume",
                monitorId,
                v => v + "%",
                _monitorManager.SetVolumeAsync);
            if (volumeOutcome.HasValue)
            {
                changes.Add(volumeOutcome.Value);
            }

            var colorTempOutcome = await TryRestoreWithOutcomeAsync(
                setting.ColorTemperatureVcp,
                monitorVm.SupportsColorTemperature,
                "color-temperature",
                monitorId,
                v => MonitorDtoProjector.FormatDiscrete(0x14, v),
                _monitorManager.SetColorTemperatureAsync);
            if (colorTempOutcome.HasValue)
            {
                changes.Add(colorTempOutcome.Value);
            }

            outcomes.Add(new ProfileApplyOutcome(
                monitorId,
                Connected: true,
                Changes: changes));
        }

        return outcomes;
    }

    /// <summary>
    /// Restore monitor settings from state file - ONLY called at startup when RestoreSettingsOnStartup is enabled.
    /// Compares saved values with current hardware values and only writes when different.
    /// </summary>
    public async Task RestoreMonitorSettingsAsync()
    {
        try
        {
            IsLoading = true;
            var updateTasks = new List<Task>();

            foreach (var monitorVm in Monitors)
            {
                var savedState = _stateManager.GetMonitorParameters(monitorVm.Id);
                if (!savedState.HasValue)
                {
                    continue;
                }

                var (brightness, colorTemp, contrast, volume) = savedState.Value;

                TryRestore(updateTasks, brightness, monitorVm.ShowBrightness, monitorVm.Brightness, monitorVm.SetBrightnessAsync);
                TryRestore(updateTasks, colorTemp, monitorVm.ShowColorTemperature, monitorVm.ColorTemperature, monitorVm.SetColorTemperatureAsync);
                TryRestore(updateTasks, contrast, monitorVm.ShowContrast, monitorVm.Contrast, monitorVm.SetContrastAsync);
                TryRestore(updateTasks, volume, monitorVm.ShowVolume, monitorVm.Volume, monitorVm.SetVolumeAsync);
            }

            if (updateTasks.Count > 0)
            {
                await Task.WhenAll(updateTasks);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[RestoreMonitorSettings] Failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Apply feature visibility settings to a monitor ViewModel.
    /// Only shows features that are both enabled by user AND supported by hardware.
    /// </summary>
    private void ApplyFeatureVisibility(MonitorViewModel monitorVm, PowerDisplaySettings settings)
    {
        var monitorSettings = settings.Properties.Monitors.FirstOrDefault(m =>
            MonitorIdComparer.Equal(m.Id, monitorVm.Id));

        if (monitorSettings != null)
        {
            // Only show features that are both enabled by user AND supported by hardware
            monitorVm.ShowContrast = monitorSettings.EnableContrast && monitorVm.SupportsContrast;
            monitorVm.ShowVolume = monitorSettings.EnableVolume && monitorVm.SupportsVolume;
            monitorVm.ShowInputSource = monitorSettings.EnableInputSource && monitorVm.SupportsInputSource;
            monitorVm.ShowRotation = monitorSettings.EnableRotation;
            monitorVm.ShowColorTemperature = monitorSettings.EnableColorTemperature && monitorVm.SupportsColorTemperature;
            monitorVm.ShowPowerState = monitorSettings.EnablePowerState && monitorVm.SupportsPowerState;
        }
    }

    /// <summary>
    /// Thread-safe save method that can be called from background threads.
    /// Does not access UI collections or update UI properties.
    /// </summary>
    public void SaveMonitorSettingDirect(string monitorId, string property, int value)
    {
        try
        {
            // This is thread-safe - _stateManager has internal locking
            // No UI thread operations, no ObservableCollection access
            _stateManager.UpdateMonitorParameter(monitorId, property, value);
        }
        catch (Exception ex)
        {
            // Only log, don't update UI from background thread
            Logger.LogError($"Failed to queue setting save for monitorId '{monitorId}': {ex.Message}");
        }
    }

    /// <summary>
    /// Save monitor information to settings.json for Settings UI to read
    /// </summary>
    private void SaveMonitorsToSettings()
    {
        try
        {
            // Load current settings to preserve user preferences (including IsHidden)
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);

            // Create lookup of existing monitors by Id to preserve settings
            // Filter out monitors with empty IDs to avoid dictionary key collision errors
            var existingMonitorSettings = settings.Properties.Monitors
                .Where(m => !string.IsNullOrEmpty(m.Id))
                .GroupBy(m => m.Id, MonitorIdComparer.Instance)
                .ToDictionary(g => g.Key, g => g.First(), MonitorIdComparer.Instance);

            // Build monitor list using Settings UI's MonitorInfo model
            // Only include monitors with valid (non-empty) IDs to auto-fix corrupted settings
            var monitors = new List<Microsoft.PowerToys.Settings.UI.Library.MonitorInfo>();

            foreach (var vm in Monitors)
            {
                // Skip monitors with empty IDs - they are invalid and would cause issues
                if (string.IsNullOrEmpty(vm.Id))
                {
                    Logger.LogWarning($"[SaveMonitors] Skipping monitor '{vm.Name}' with empty Id");
                    continue;
                }

                var monitorInfo = CreateMonitorInfo(vm);
                ApplyPreservedUserSettings(monitorInfo, existingMonitorSettings);
                monitors.Add(monitorInfo);
            }

            // One-shot upgrade migration: PowerDisplay versions before PR #47712 wrote
            // monitor Ids as "{Source}_{EdidId}_{MonitorNumber}". Partition the on-disk
            // entries into legacy + keep, copy preferences from any legacy entry onto the
            // matching DevicePath-based monitor (strict (EdidId, MonitorNumber) match), and
            // feed only the keep set into the retention pass so a stale Id never lingers.
            var legacyEntries = new List<Microsoft.PowerToys.Settings.UI.Library.MonitorInfo>();
            var retentionInput = new List<Microsoft.PowerToys.Settings.UI.Library.MonitorInfo>();
            foreach (var m in settings.Properties.Monitors)
            {
                (MonitorIdentity.IsLegacyId(m.Id) ? legacyEntries : retentionInput).Add(m);
            }

            foreach (var legacy in legacyEntries)
            {
                var newId = MonitorIdMigrator.MatchNewId(
                    legacy.Id,
                    monitors.Select(m => (m.Id, m.MonitorNumber)));
                if (newId is null)
                {
                    Logger.LogWarning(
                        $"[LegacyMigration] Dropping settings entry '{legacy.Id}': no current monitor with matching EdidId+MonitorNumber.");
                    continue;
                }

                // If the target already has a new-format entry on disk, ApplyPreservedUserSettings
                // has restored those preferences just above — do not let the legacy entry overwrite
                // them (this happens when a user upgraded pre-#47712 → #47712 → this PR and customized
                // preferences during the #47712 phase).
                if (existingMonitorSettings.ContainsKey(newId))
                {
                    continue;
                }

                var target = monitors.FirstOrDefault(m => MonitorIdComparer.Equal(m.Id, newId));
                if (target != null)
                {
                    CopyUserFlags(target, legacy);
                }
            }

            // Replace the manually-built `monitors` list with the rebuilt list that
            // applies the 30-day retention rule for non-hidden disconnected monitors.
            monitors = MonitorSettingsRebuilder.Rebuild(
                currentlyDiscovered: monitors,
                existing: retentionInput,
                clock: _clock,
                retentionDays: PowerDisplaySettings.MonitorEntryRetentionDays);

            // Update monitors list
            settings.Properties.Monitors = monitors;

            // Save back to settings.json using source-generated context for AOT
            _settingsUtils.SaveSettings(
                System.Text.Json.JsonSerializer.Serialize(settings, AppJsonContext.Default.PowerDisplaySettings),
                PowerDisplaySettings.ModuleName);

            // Signal Settings UI that monitor list has been updated
            SignalMonitorsRefreshEvent();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to save monitors to settings.json: {ex.Message}");
        }
    }

    /// <summary>
    /// Create MonitorInfo object from MonitorViewModel
    /// </summary>
    private Microsoft.PowerToys.Settings.UI.Library.MonitorInfo CreateMonitorInfo(MonitorViewModel vm)
    {
        // Validate monitor Id - this should never be empty for properly discovered monitors
        if (string.IsNullOrEmpty(vm.Id))
        {
            Logger.LogWarning($"[CreateMonitorInfo] Monitor '{vm.Name}' has empty Id - this may cause issues with Settings UI");
        }

        var monitorInfo = new Microsoft.PowerToys.Settings.UI.Library.MonitorInfo
        {
            Name = vm.Name,
            Id = vm.Id,
            CommunicationMethod = vm.CommunicationMethod,
            CurrentBrightness = vm.Brightness,
            ColorTemperatureVcp = vm.ColorTemperature,
            CapabilitiesRaw = vm.CapabilitiesRaw,
            VcpCodesFormatted = vm.VcpCapabilitiesInfo?.GetSortedVcpCodes()
                .Select(info => FormatVcpCodeForDisplay(info.Code, info))
                .ToList() ?? new List<Microsoft.PowerToys.Settings.UI.Library.VcpCodeDisplayInfo>(),

            // Infer support flags from VCP capabilities
            // VCP 0x12 (18) = Contrast, 0x14 (20) = Color Temperature, 0x60 (96) = Input Source, 0x62 (98) = Volume, 0xD6 (214) = Power Mode
            SupportsContrast = vm.VcpCapabilitiesInfo?.SupportedVcpCodes.ContainsKey(0x12) ?? false,
            SupportsColorTemperature = vm.VcpCapabilitiesInfo?.SupportedVcpCodes.ContainsKey(0x14) ?? false,
            SupportsInputSource = vm.VcpCapabilitiesInfo?.SupportedVcpCodes.ContainsKey(0x60) ?? false,
            SupportsVolume = vm.VcpCapabilitiesInfo?.SupportedVcpCodes.ContainsKey(0x62) ?? false,
            SupportsPowerState = vm.VcpCapabilitiesInfo?.SupportedVcpCodes.ContainsKey(0xD6) ?? false,

            // Default Enable* for new monitors (first-time setup):
            // - Contrast / Volume: enabled if the monitor advertises the VCP code (low-risk features).
            // - InputSource / ColorTemperature / PowerState: always disabled by default. These can leave
            //   the monitor in a state recoverable only via physical buttons; users opt-in via the
            //   Settings UI checkbox, which raises a confirmation dialog (HandleDangerousFeatureClickAsync).
            // ApplyPreservedUserSettings will override these with saved user preferences if they exist.
            EnableContrast = vm.VcpCapabilitiesInfo?.SupportedVcpCodes.ContainsKey(0x12) ?? false,
            EnableVolume = vm.VcpCapabilitiesInfo?.SupportedVcpCodes.ContainsKey(0x62) ?? false,
            EnableInputSource = false,
            EnableColorTemperature = false,
            EnablePowerState = false,

            // Monitor number for display name formatting
            MonitorNumber = vm.MonitorNumber,
            LastSeenUtc = _clock.UtcNow,
        };

        return monitorInfo;
    }

    /// <summary>
    /// Apply preserved user settings from existing monitor settings
    /// </summary>
    private static void ApplyPreservedUserSettings(
        Microsoft.PowerToys.Settings.UI.Library.MonitorInfo monitorInfo,
        Dictionary<string, Microsoft.PowerToys.Settings.UI.Library.MonitorInfo> existingSettings)
    {
        if (existingSettings.TryGetValue(monitorInfo.Id, out var existingMonitor))
        {
            CopyUserFlags(monitorInfo, existingMonitor);
        }
    }

    /// <summary>
    /// Copy the user-managed flags (IsHidden + Enable*) from <paramref name="source"/>
    /// onto <paramref name="target"/>. Shared by the regular new-format restore path
    /// (<see cref="ApplyPreservedUserSettings"/>) and the one-shot legacy-Id migration
    /// so the field list stays in one place.
    /// </summary>
    private static void CopyUserFlags(
        Microsoft.PowerToys.Settings.UI.Library.MonitorInfo target,
        Microsoft.PowerToys.Settings.UI.Library.MonitorInfo source)
    {
        target.IsHidden = source.IsHidden;
        target.EnableContrast = source.EnableContrast;
        target.EnableVolume = source.EnableVolume;
        target.EnableInputSource = source.EnableInputSource;
        target.EnableRotation = source.EnableRotation;
        target.EnableColorTemperature = source.EnableColorTemperature;
        target.EnablePowerState = source.EnablePowerState;
    }

    /// <summary>
    /// Companion one-shot migration for the two side files that
    /// <see cref="SaveMonitorsToSettings"/> does not touch:
    /// <c>profiles.json</c> (user-saved presets) and <c>monitor_state.json</c>
    /// (last-known hardware state used by <c>RestoreSettingsOnStartup</c>).
    /// Invoked from the first successful discovery; on subsequent runs every entry is
    /// already in new-format and the filters short-circuit.
    /// </summary>
    private void MigrateLegacyMonitorIdsInSideFiles()
    {
        var discovered = Monitors
            .Where(m => !string.IsNullOrEmpty(m.Id))
            .Select(m => (m.Id, m.MonitorNumber))
            .ToList();

        if (discovered.Count == 0)
        {
            return;
        }

        // profiles.json and monitor_state.json are independent — a failure in one must
        // not skip the other. MigrateLegacyKeys already has its own try/catch, so we
        // only need to guard the profiles path here.
        try
        {
            MigrateLegacyMonitorIdsInProfiles(discovered);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[LegacyMigration] Failed to migrate profiles.json: {ex.Message}");
        }

        _stateManager.MigrateLegacyKeys(discovered);
    }

    private static void MigrateLegacyMonitorIdsInProfiles(List<(string Id, int MonitorNumber)> discovered)
    {
        var profiles = ProfileService.LoadProfiles();
        if (profiles?.Profiles is null || profiles.Profiles.Count == 0)
        {
            return;
        }

        bool anyChanged = false;
        foreach (var profile in profiles.Profiles)
        {
            if (profile?.MonitorSettings is null)
            {
                continue;
            }

            bool changed = false;
            foreach (var legacy in profile.MonitorSettings
                .Where(s => MonitorIdentity.IsLegacyId(s?.MonitorId))
                .ToList())
            {
                var newId = MonitorIdMigrator.MatchNewId(legacy.MonitorId, discovered);
                if (newId != null && profile.MonitorSettings.All(s => !MonitorIdComparer.Equal(s.MonitorId, newId)))
                {
                    profile.MonitorSettings.Add(new ProfileMonitorSetting(
                        newId,
                        legacy.Brightness,
                        legacy.ColorTemperatureVcp,
                        legacy.Contrast,
                        legacy.Volume));
                }
                else if (newId == null)
                {
                    Logger.LogWarning(
                        $"[LegacyMigration] Dropping profile setting for '{legacy.MonitorId}' in profile '{profile.Name}': no current monitor with matching EdidId+MonitorNumber.");
                }

                profile.MonitorSettings.Remove(legacy);
                changed = true;
            }

            if (changed)
            {
                profile.Touch();
                anyChanged = true;
            }
        }

        if (anyChanged)
        {
            ProfileService.SaveProfiles(profiles);
            Logger.LogInfo("[LegacyMigration] profiles.json updated with DevicePath-based monitor Ids.");
        }
    }

    /// <summary>
    /// Signal Settings UI that the monitor list has been refreshed
    /// </summary>
    private void SignalMonitorsRefreshEvent()
    {
        EventHelper.SignalEvent(Constants.RefreshPowerDisplayMonitorsEvent());
    }

    /// <summary>
    /// Format VCP code information for display in Settings UI
    /// </summary>
    private Microsoft.PowerToys.Settings.UI.Library.VcpCodeDisplayInfo FormatVcpCodeForDisplay(byte code, VcpCodeInfo info)
    {
        var result = new Microsoft.PowerToys.Settings.UI.Library.VcpCodeDisplayInfo
        {
            Code = info.FormattedCode,
            Title = info.FormattedTitle,
        };

        if (info.IsContinuous)
        {
            result.Values = "Continuous range";
            result.HasValues = true;
        }
        else if (info.HasDiscreteValues)
        {
            var formattedValues = info.SupportedValues
                .Select(v => Common.Utils.VcpNames.GetFormattedValueName(code, v))
                .ToList();
            result.Values = $"Values: {string.Join(", ", formattedValues)}";
            result.HasValues = true;

            // Populate value list for Settings UI ComboBox
            // Store raw name (without formatting) so Settings UI can format it consistently
            result.ValueList = info.SupportedValues
                .Select(v => new Microsoft.PowerToys.Settings.UI.Library.VcpValueInfo
                {
                    Value = $"0x{v:X2}",
                    Name = Common.Utils.VcpNames.GetValueName(code, v),
                })
                .ToList();
        }
        else
        {
            result.HasValues = false;
        }

        return result;
    }

    /// <summary>
    /// Send settings telemetry event (triggered by Runner via send_settings_telemetry())
    /// </summary>
    public void SendSettingsTelemetry()
    {
        try
        {
            // Load current settings to get hotkey and tray icon status
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);

            // Load profiles to get count
            var profilesData = ProfileService.LoadProfiles();

            var telemetryEvent = new PowerDisplaySettingsTelemetryEvent
            {
                HotkeyEnabled = settings.Properties.ActivationShortcut?.IsValid() ?? false,
                TrayIconEnabled = settings.Properties.ShowSystemTrayIcon,
                MonitorCount = Monitors.Count,
                ProfileCount = profilesData?.Profiles?.Count ?? 0,
            };

            PowerToysTelemetry.Log.WriteEvent(telemetryEvent);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Telemetry] Failed to send settings telemetry: {ex.Message}");
        }
    }
}
