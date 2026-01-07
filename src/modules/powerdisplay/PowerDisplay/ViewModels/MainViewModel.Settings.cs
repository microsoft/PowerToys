// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using PowerDisplay.Common.Utils;
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
    /// Check if a value is within the valid range (inclusive).
    /// </summary>
    private static bool IsValueInRange(int value, int min, int max) => value >= min && value <= max;

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

            // Apply UI configuration changes only (feature visibility toggles, etc.)
            // Hardware parameters (brightness, color temperature) are applied via custom actions
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>("PowerDisplay");
            ApplyUIConfiguration(settings);

            // Reload profiles in case they were added/updated/deleted in Settings UI
            LoadProfiles();

            // Reload UI display settings (profile switcher, identify button, color temp switcher)
            LoadUIDisplaySettings();
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
    /// Apply profile by name (called via Named Pipe from Settings UI)
    /// This is the new direct method that receives the profile name via IPC.
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
    /// Apply profile settings to monitors
    /// </summary>
    private async Task ApplyProfileAsync(List<ProfileMonitorSetting> monitorSettings)
    {
        var updateTasks = new List<Task>();

        foreach (var setting in monitorSettings)
        {
            // Find monitor by Id (unique identifier)
            var monitorVm = Monitors.FirstOrDefault(m => m.Id == setting.MonitorId);

            if (monitorVm == null)
            {
                continue;
            }

            // Apply brightness if included in profile
            if (setting.Brightness.HasValue &&
                IsValueInRange(setting.Brightness.Value, monitorVm.MinBrightness, monitorVm.MaxBrightness))
            {
                updateTasks.Add(monitorVm.SetBrightnessAsync(setting.Brightness.Value));
            }

            // Apply contrast if supported and value provided
            if (setting.Contrast.HasValue && monitorVm.ShowContrast &&
                IsValueInRange(setting.Contrast.Value, monitorVm.MinContrast, monitorVm.MaxContrast))
            {
                updateTasks.Add(monitorVm.SetContrastAsync(setting.Contrast.Value));
            }

            // Apply volume if supported and value provided
            if (setting.Volume.HasValue && monitorVm.ShowVolume &&
                IsValueInRange(setting.Volume.Value, monitorVm.MinVolume, monitorVm.MaxVolume))
            {
                updateTasks.Add(monitorVm.SetVolumeAsync(setting.Volume.Value));
            }

            // Apply color temperature if included in profile
            if (setting.ColorTemperatureVcp.HasValue && setting.ColorTemperatureVcp.Value > 0)
            {
                updateTasks.Add(monitorVm.SetColorTemperatureAsync(setting.ColorTemperatureVcp.Value));
            }
        }

        // Wait for all updates to complete
        if (updateTasks.Count > 0)
        {
            await Task.WhenAll(updateTasks);
        }
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

                // Restore brightness if different from current
                if (IsValueInRange(savedState.Value.Brightness, monitorVm.MinBrightness, monitorVm.MaxBrightness) &&
                    savedState.Value.Brightness != monitorVm.Brightness)
                {
                    updateTasks.Add(monitorVm.SetBrightnessAsync(savedState.Value.Brightness));
                }

                // Restore color temperature if different from current
                if (savedState.Value.ColorTemperatureVcp > 0 &&
                    savedState.Value.ColorTemperatureVcp != monitorVm.ColorTemperature)
                {
                    updateTasks.Add(monitorVm.SetColorTemperatureAsync(savedState.Value.ColorTemperatureVcp));
                }

                // Restore contrast if different from current
                if (monitorVm.ShowContrast &&
                    IsValueInRange(savedState.Value.Contrast, monitorVm.MinContrast, monitorVm.MaxContrast) &&
                    savedState.Value.Contrast != monitorVm.Contrast)
                {
                    updateTasks.Add(monitorVm.SetContrastAsync(savedState.Value.Contrast));
                }

                // Restore volume if different from current
                if (monitorVm.ShowVolume &&
                    IsValueInRange(savedState.Value.Volume, monitorVm.MinVolume, monitorVm.MaxVolume) &&
                    savedState.Value.Volume != monitorVm.Volume)
                {
                    updateTasks.Add(monitorVm.SetVolumeAsync(savedState.Value.Volume));
                }
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
    /// Apply feature visibility settings to a monitor ViewModel
    /// </summary>
    private void ApplyFeatureVisibility(MonitorViewModel monitorVm, PowerDisplaySettings settings)
    {
        var monitorSettings = settings.Properties.Monitors.FirstOrDefault(m =>
            m.Id == monitorVm.Id);

        if (monitorSettings != null)
        {
            monitorVm.ShowContrast = monitorSettings.EnableContrast;
            monitorVm.ShowVolume = monitorSettings.EnableVolume;
            monitorVm.ShowInputSource = monitorSettings.EnableInputSource;
            monitorVm.ShowRotation = monitorSettings.EnableRotation;
            monitorVm.ShowColorTemperature = monitorSettings.EnableColorTemperature;
            monitorVm.ShowPowerState = monitorSettings.EnablePowerState;
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
                .GroupBy(m => m.Id)
                .ToDictionary(g => g.Key, g => g.First());

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

            // Also add hidden monitors from existing settings (monitors that are hidden but still connected)
            // Only include those with valid IDs
            foreach (var existingMonitor in settings.Properties.Monitors.Where(m => m.IsHidden && !string.IsNullOrEmpty(m.Id)))
            {
                // Only add if not already in the list (to avoid duplicates)
                if (!monitors.Any(m => m.Id == existingMonitor.Id))
                {
                    monitors.Add(existingMonitor);
                }
            }

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

            // Default Enable* to match Supports* for new monitors (first-time setup)
            // ApplyPreservedUserSettings will override these with saved user preferences if they exist
            EnableContrast = vm.VcpCapabilitiesInfo?.SupportedVcpCodes.ContainsKey(0x12) ?? false,
            EnableVolume = vm.VcpCapabilitiesInfo?.SupportedVcpCodes.ContainsKey(0x62) ?? false,
            EnableInputSource = vm.VcpCapabilitiesInfo?.SupportedVcpCodes.ContainsKey(0x60) ?? false,
            EnableColorTemperature = vm.VcpCapabilitiesInfo?.SupportedVcpCodes.ContainsKey(0x14) ?? false,
            EnablePowerState = vm.VcpCapabilitiesInfo?.SupportedVcpCodes.ContainsKey(0xD6) ?? false,

            // Monitor number for display name formatting
            MonitorNumber = vm.MonitorNumber,
        };

        return monitorInfo;
    }

    /// <summary>
    /// Apply preserved user settings from existing monitor settings
    /// </summary>
    private void ApplyPreservedUserSettings(
        Microsoft.PowerToys.Settings.UI.Library.MonitorInfo monitorInfo,
        Dictionary<string, Microsoft.PowerToys.Settings.UI.Library.MonitorInfo> existingSettings)
    {
        if (existingSettings.TryGetValue(monitorInfo.Id, out var existingMonitor))
        {
            monitorInfo.IsHidden = existingMonitor.IsHidden;
            monitorInfo.EnableContrast = existingMonitor.EnableContrast;
            monitorInfo.EnableVolume = existingMonitor.EnableVolume;
            monitorInfo.EnableInputSource = existingMonitor.EnableInputSource;
            monitorInfo.EnableRotation = existingMonitor.EnableRotation;
            monitorInfo.EnableColorTemperature = existingMonitor.EnableColorTemperature;
            monitorInfo.EnablePowerState = existingMonitor.EnablePowerState;
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
