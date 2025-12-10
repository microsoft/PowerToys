// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using PowerDisplay.Common.Utils;
using PowerDisplay.Serialization;
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
            Logger.LogInfo("[Settings] Processing settings update from Settings UI");

            // Rebuild monitor list with updated hidden monitor settings
            // UpdateMonitorList already handles filtering hidden monitors
            UpdateMonitorList(_monitorManager.Monitors, isInitialLoad: false);

            // Apply UI configuration changes only (feature visibility toggles, etc.)
            // Hardware parameters (brightness, color temperature) are applied via custom actions
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>("PowerDisplay");
            ApplyUIConfiguration(settings);

            // Reload profiles in case they were added/updated/deleted in Settings UI
            LoadProfiles();

            Logger.LogInfo("[Settings] Settings update complete");
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
            Logger.LogInfo("[Settings] Applying UI configuration changes (feature visibility)");

            foreach (var monitorVm in Monitors)
            {
                ApplyFeatureVisibility(monitorVm, settings);
            }

            // Trigger UI refresh
            UIRefreshRequested?.Invoke(this, EventArgs.Empty);

            Logger.LogInfo("[Settings] UI configuration applied");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Settings] Failed to apply UI configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply color temperature to a specific monitor (triggered by custom action from Settings UI)
    /// This is called when user explicitly changes color temperature in Settings UI.
    /// Reads the pending operation from settings and applies it directly.
    /// </summary>
    public async void ApplyColorTemperatureFromSettings()
    {
        try
        {
            await ApplyColorTemperatureFromSettingsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Settings] Failed to apply color temperature from settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Core implementation for applying color temperature from settings
    /// </summary>
    private async Task ApplyColorTemperatureFromSettingsAsync()
    {
        var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>("PowerDisplay");

        // Check if there's a pending color temperature operation
        var pendingOp = settings.Properties.PendingColorTemperatureOperation;

        if (pendingOp != null && !string.IsNullOrEmpty(pendingOp.MonitorId))
        {
            Logger.LogInfo($"[Settings] Processing pending color temperature operation: Monitor '{pendingOp.MonitorId}' -> 0x{pendingOp.ColorTemperatureVcp:X2}");

            // Find the monitor by internal name (ID)
            var monitorVm = Monitors.FirstOrDefault(m => m.Id == pendingOp.MonitorId);

            if (monitorVm != null)
            {
                // Apply color temperature directly
                await ApplyColorTemperatureAsync(monitorVm, pendingOp.ColorTemperatureVcp);
                Logger.LogInfo($"[Settings] Successfully applied color temperature to monitor '{pendingOp.MonitorId}'");

                // Update the monitor's ColorTemperatureVcp in settings to match the applied value
                // This ensures Settings UI gets the correct value when it reloads from file
                var settingsMonitor = settings.Properties.Monitors.FirstOrDefault(m => m.InternalName == pendingOp.MonitorId);
                if (settingsMonitor != null)
                {
                    settingsMonitor.ColorTemperatureVcp = pendingOp.ColorTemperatureVcp;
                    Logger.LogInfo($"[Settings] Updated monitor ColorTemperatureVcp in settings: 0x{pendingOp.ColorTemperatureVcp:X2}");
                }
            }
            else
            {
                Logger.LogWarning($"[Settings] Monitor not found: {pendingOp.MonitorId}");
            }

            // Clear the pending operation and save updated settings
            settings.Properties.PendingColorTemperatureOperation = null;
            _settingsUtils.SaveSettings(
                System.Text.Json.JsonSerializer.Serialize(settings, AppJsonContext.Default.PowerDisplaySettings),
                PowerDisplaySettings.ModuleName);
            Logger.LogInfo("[Settings] Cleared pending color temperature operation and saved updated settings");
        }
        else
        {
            Logger.LogInfo("[Settings] No pending color temperature operation");
        }
    }

    /// <summary>
    /// Apply profile from Settings UI (triggered by custom action from Settings UI)
    /// This is called when user explicitly switches profile in Settings UI.
    /// Reads the pending operation from settings and applies it directly.
    /// </summary>
    public async void ApplyProfileFromSettings()
    {
        try
        {
            await ApplyProfileFromSettingsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Profile] Failed to apply profile from settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Core implementation for applying profile from settings
    /// </summary>
    private async Task ApplyProfileFromSettingsAsync()
    {
        var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>("PowerDisplay");

        // Check if there's a pending profile operation
        var pendingOp = settings.Properties.PendingProfileOperation;

        if (pendingOp != null && !string.IsNullOrEmpty(pendingOp.ProfileName))
        {
            Logger.LogInfo($"[Profile] Processing pending profile operation: '{pendingOp.ProfileName}' with {pendingOp.MonitorSettings?.Count ?? 0} monitors");

            if (pendingOp.MonitorSettings != null && pendingOp.MonitorSettings.Count > 0)
            {
                // Apply profile settings to monitors
                await ApplyProfileAsync(pendingOp.ProfileName, pendingOp.MonitorSettings);

                // Note: We no longer track "current profile" - profiles are just templates
                Logger.LogInfo($"[Profile] Successfully applied profile '{pendingOp.ProfileName}'");
            }
            else
            {
                Logger.LogWarning($"[Profile] Profile '{pendingOp.ProfileName}' has no monitor settings");
            }

            // Clear the pending operation
            settings.Properties.PendingProfileOperation = null;
            _settingsUtils.SaveSettings(
                System.Text.Json.JsonSerializer.Serialize(settings, AppJsonContext.Default.PowerDisplaySettings),
                PowerDisplaySettings.ModuleName);
            Logger.LogInfo("[Profile] Cleared pending profile operation");
        }
        else
        {
            Logger.LogInfo("[Profile] No pending profile operation");
        }
    }

    /// <summary>
    /// Handle theme change notification from LightSwitch
    /// </summary>
    private void OnLightSwitchThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.ProfileToApply))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                Logger.LogInfo($"[LightSwitch Integration] Applying profile: {e.ProfileToApply}");

                // Load and apply the profile
                var profilesData = ProfileService.LoadProfiles();
                var profile = profilesData.GetProfile(e.ProfileToApply);

                if (profile == null || !profile.IsValid())
                {
                    Logger.LogWarning($"[LightSwitch Integration] Profile '{e.ProfileToApply}' not found or invalid");
                    return;
                }

                // Apply the profile
                await ApplyProfileAsync(e.ProfileToApply, profile.MonitorSettings);
                Logger.LogInfo($"[LightSwitch Integration] Successfully applied profile '{e.ProfileToApply}'");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[LightSwitch Integration] Failed to apply profile: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Apply profile settings to monitors
    /// </summary>
    private async Task ApplyProfileAsync(string profileName, List<ProfileMonitorSetting> monitorSettings)
    {
        var updateTasks = new List<Task>();

        foreach (var setting in monitorSettings)
        {
            // Find monitor by InternalName first (unique identifier), fall back to HardwareId for old profiles
            MonitorViewModel? monitorVm = null;

            if (!string.IsNullOrEmpty(setting.MonitorInternalName))
            {
                monitorVm = Monitors.FirstOrDefault(m => m.InternalName == setting.MonitorInternalName);
            }

            // Fallback to HardwareId for backward compatibility with old profiles
            if (monitorVm == null)
            {
                monitorVm = Monitors.FirstOrDefault(m => m.HardwareId == setting.HardwareId);
            }

            if (monitorVm == null)
            {
                Logger.LogWarning($"[Profile] Monitor with InternalName '{setting.MonitorInternalName}' or HardwareId '{setting.HardwareId}' not found (disconnected?)");
                continue;
            }

            Logger.LogInfo($"[Profile] Applying settings to monitor '{monitorVm.Name}' (InternalName: {setting.MonitorInternalName}, HardwareId: {setting.HardwareId})");

            // Apply brightness if included in profile
            if (setting.Brightness.HasValue &&
                IsValueInRange(setting.Brightness.Value, monitorVm.MinBrightness, monitorVm.MaxBrightness))
            {
                updateTasks.Add(monitorVm.SetBrightnessAsync(setting.Brightness.Value, immediate: true));
            }

            // Apply contrast if supported and value provided
            if (setting.Contrast.HasValue && monitorVm.ShowContrast &&
                IsValueInRange(setting.Contrast.Value, monitorVm.MinContrast, monitorVm.MaxContrast))
            {
                updateTasks.Add(monitorVm.SetContrastAsync(setting.Contrast.Value, immediate: true));
            }

            // Apply volume if supported and value provided
            if (setting.Volume.HasValue && monitorVm.ShowVolume &&
                IsValueInRange(setting.Volume.Value, monitorVm.MinVolume, monitorVm.MaxVolume))
            {
                updateTasks.Add(monitorVm.SetVolumeAsync(setting.Volume.Value, immediate: true));
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
            Logger.LogInfo($"[Profile] Applied {updateTasks.Count} parameter updates");
        }
    }

    /// <summary>
    /// Apply color temperature to a specific monitor
    /// </summary>
    private async Task ApplyColorTemperatureAsync(MonitorViewModel monitorVm, int colorTemperature)
    {
        // Use MonitorViewModel's unified method
        await monitorVm.SetColorTemperatureAsync(colorTemperature);
    }

    /// <summary>
    /// Restore monitor settings from state file - ONLY called at startup when RestoreSettingsOnStartup is enabled
    /// </summary>
    public void RestoreMonitorSettings()
    {
        try
        {
            IsLoading = true;

            foreach (var monitorVm in Monitors)
            {
                // Find and apply corresponding saved settings from state file using stable HardwareId
                var savedState = _stateManager.GetMonitorParameters(monitorVm.HardwareId);
                if (!savedState.HasValue)
                {
                    continue;
                }

                // Validate and apply saved values (skip invalid values)
                // Use UpdatePropertySilently to avoid triggering hardware updates during initialization
                if (IsValueInRange(savedState.Value.Brightness, monitorVm.MinBrightness, monitorVm.MaxBrightness))
                {
                    monitorVm.UpdatePropertySilently(nameof(monitorVm.Brightness), savedState.Value.Brightness);
                }

                // Color temperature: VCP 0x14 preset value (discrete values, no range check needed)
                if (savedState.Value.ColorTemperatureVcp > 0)
                {
                    monitorVm.UpdatePropertySilently(nameof(monitorVm.ColorTemperature), savedState.Value.ColorTemperatureVcp);
                }

                // Contrast validation - only apply if hardware supports it
                if (monitorVm.ShowContrast &&
                    IsValueInRange(savedState.Value.Contrast, monitorVm.MinContrast, monitorVm.MaxContrast))
                {
                    monitorVm.UpdatePropertySilently(nameof(monitorVm.Contrast), savedState.Value.Contrast);
                }

                // Volume validation - only apply if hardware supports it
                if (monitorVm.ShowVolume &&
                    IsValueInRange(savedState.Value.Volume, monitorVm.MinVolume, monitorVm.MaxVolume))
                {
                    monitorVm.UpdatePropertySilently(nameof(monitorVm.Volume), savedState.Value.Volume);
                }
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
            m.InternalName == monitorVm.InternalName);

        if (monitorSettings != null)
        {
            Logger.LogInfo($"[Startup] Applying feature visibility for '{monitorVm.InternalName}': Contrast={monitorSettings.EnableContrast}, Volume={monitorSettings.EnableVolume}, InputSource={monitorSettings.EnableInputSource}, Rotation={monitorSettings.EnableRotation}");

            monitorVm.ShowContrast = monitorSettings.EnableContrast;
            monitorVm.ShowVolume = monitorSettings.EnableVolume;
            monitorVm.ShowInputSource = monitorSettings.EnableInputSource;
            monitorVm.ShowRotation = monitorSettings.EnableRotation;
        }
        else
        {
            Logger.LogWarning($"[Startup] No feature settings found for '{monitorVm.InternalName}' - using defaults");
        }
    }

    /// <summary>
    /// Thread-safe save method that can be called from background threads.
    /// Does not access UI collections or update UI properties.
    /// </summary>
    public void SaveMonitorSettingDirect(string hardwareId, string property, int value)
    {
        try
        {
            // This is thread-safe - _stateManager has internal locking
            // No UI thread operations, no ObservableCollection access
            _stateManager.UpdateMonitorParameter(hardwareId, property, value);
        }
        catch (Exception ex)
        {
            // Only log, don't update UI from background thread
            Logger.LogError($"Failed to queue setting save for HardwareId '{hardwareId}': {ex.Message}");
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

            // Create lookup of existing monitors by InternalName to preserve settings
            var existingMonitorSettings = settings.Properties.Monitors
                .ToDictionary(m => m.InternalName, m => m);

            // Build monitor list using Settings UI's MonitorInfo model
            var monitors = new List<Microsoft.PowerToys.Settings.UI.Library.MonitorInfo>();

            foreach (var vm in Monitors)
            {
                var monitorInfo = CreateMonitorInfo(vm);
                ApplyPreservedUserSettings(monitorInfo, existingMonitorSettings);
                monitors.Add(monitorInfo);
            }

            // Also add hidden monitors from existing settings (monitors that are hidden but still connected)
            foreach (var existingMonitor in settings.Properties.Monitors.Where(m => m.IsHidden))
            {
                // Only add if not already in the list (to avoid duplicates)
                if (!monitors.Any(m => m.InternalName == existingMonitor.InternalName))
                {
                    monitors.Add(existingMonitor);
                    Logger.LogInfo($"[SaveMonitorsToSettings] Preserving hidden monitor in settings: {existingMonitor.Name} ({existingMonitor.InternalName})");
                }
            }

            // Update monitors list
            settings.Properties.Monitors = monitors;

            // Save back to settings.json using source-generated context for AOT
            _settingsUtils.SaveSettings(
                System.Text.Json.JsonSerializer.Serialize(settings, AppJsonContext.Default.PowerDisplaySettings),
                PowerDisplaySettings.ModuleName);

            Logger.LogInfo($"Saved {monitors.Count} monitors to settings.json ({Monitors.Count} visible, {monitors.Count - Monitors.Count} hidden)");

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
        var monitorInfo = new Microsoft.PowerToys.Settings.UI.Library.MonitorInfo(
            name: vm.Name,
            internalName: vm.Id,
            hardwareId: vm.HardwareId,
            communicationMethod: vm.CommunicationMethod,
            currentBrightness: vm.Brightness,
            colorTemperatureVcp: vm.ColorTemperature)
        {
            CapabilitiesRaw = vm.CapabilitiesRaw,
            VcpCodes = vm.VcpCapabilitiesInfo?.GetVcpCodesAsHexStrings() ?? new List<string>(),
            VcpCodesFormatted = vm.VcpCapabilitiesInfo?.GetSortedVcpCodes()
                .Select(info => FormatVcpCodeForDisplay(info.Code, info))
                .ToList() ?? new List<Microsoft.PowerToys.Settings.UI.Library.VcpCodeDisplayInfo>(),

            // Infer support flags from VCP capabilities
            // VCP 0x12 (18) = Contrast, 0x14 (20) = Color Temperature, 0x60 (96) = Input Source, 0x62 (98) = Volume
            SupportsContrast = vm.VcpCapabilitiesInfo?.SupportedVcpCodes.ContainsKey(0x12) ?? false,
            SupportsColorTemperature = vm.VcpCapabilitiesInfo?.SupportedVcpCodes.ContainsKey(0x14) ?? false,
            SupportsInputSource = vm.VcpCapabilitiesInfo?.SupportedVcpCodes.ContainsKey(0x60) ?? false,
            SupportsVolume = vm.VcpCapabilitiesInfo?.SupportedVcpCodes.ContainsKey(0x62) ?? false,

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
        if (existingSettings.TryGetValue(monitorInfo.InternalName, out var existingMonitor))
        {
            monitorInfo.IsHidden = existingMonitor.IsHidden;
            monitorInfo.EnableContrast = existingMonitor.EnableContrast;
            monitorInfo.EnableVolume = existingMonitor.EnableVolume;
            monitorInfo.EnableInputSource = existingMonitor.EnableInputSource;
            monitorInfo.EnableRotation = existingMonitor.EnableRotation;
        }
    }

    /// <summary>
    /// Signal Settings UI that the monitor list has been refreshed
    /// </summary>
    private void SignalMonitorsRefreshEvent()
    {
        EventHelper.SignalEvent(Constants.RefreshPowerDisplayMonitorsEvent());
        Logger.LogInfo("Signaled refresh monitors event to Settings UI");
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
                .Select(v => Common.Utils.VcpValueNames.GetFormattedName(code, v))
                .ToList();
            result.Values = $"Values: {string.Join(", ", formattedValues)}";
            result.HasValues = true;

            // Populate value list for Settings UI ComboBox
            // Store raw name (without formatting) so Settings UI can format it consistently
            result.ValueList = info.SupportedValues
                .Select(v => new Microsoft.PowerToys.Settings.UI.Library.VcpValueInfo
                {
                    Value = $"0x{v:X2}",
                    Name = Common.Utils.VcpValueNames.GetName(code, v),
                })
                .ToList();
        }
        else
        {
            result.HasValues = false;
        }

        return result;
    }
}
