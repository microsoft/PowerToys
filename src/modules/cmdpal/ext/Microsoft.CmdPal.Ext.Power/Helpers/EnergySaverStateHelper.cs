// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Windows.System.Power;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

internal static class EnergySaverStateHelper
{
    internal const string PowerControlKeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power";
    internal const string WhesvcKeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\whesvc";
    internal const string EnergySaverStateValueName = "EnergySaverState";
    internal const string EcoModeStateValueName = "EcoModeState";
    internal const string WhesvcWhatIfValueName = "ECP_WhatIf";
    internal const int EnergySaverStateOn = 1;
    internal const int EnergySaverStateOff = 2;

    internal static ResolvedEnergySaverState ResolveVisibleState()
    {
        if (TryGetWinRtStatus(out var winRtStatus))
        {
            return winRtStatus switch
            {
                EnergySaverStatus.On => ResolvedEnergySaverState.On,
                EnergySaverStatus.Off => ResolvedEnergySaverState.Off,
                EnergySaverStatus.Disabled => ResolvedEnergySaverState.NotAvailable,
                _ => ResolvedEnergySaverState.Unknown,
            };
        }

        if (TryGetEffectiveOverlay(out var overlayGuid) && overlayGuid != Guid.Empty)
        {
            return overlayGuid == PowerModeGuids.BestEfficiency
                ? ResolvedEnergySaverState.On
                : ResolvedEnergySaverState.Off;
        }

        if (PowerSourceHelper.TryGetEnergySaverActiveFromSystemStatus(out var systemOn))
        {
            return systemOn ? ResolvedEnergySaverState.On : ResolvedEnergySaverState.Off;
        }

        if (TryGetFromRegistry(out var registryOn))
        {
            return registryOn ? ResolvedEnergySaverState.On : ResolvedEnergySaverState.Off;
        }

        return ResolvedEnergySaverState.Unknown;
    }

    internal static bool TryResolveEffectiveState(out bool isOn, out bool canRead)
    {
        var state = ResolveVisibleState();
        if (state is ResolvedEnergySaverState.On or ResolvedEnergySaverState.Off)
        {
            isOn = state == ResolvedEnergySaverState.On;
            canRead = true;
            return true;
        }

        isOn = false;
        canRead = state == ResolvedEnergySaverState.NotAvailable;
        return canRead;
    }

    internal static bool ResolveEffectiveState(
        bool hasWinRt,
        EnergySaverStatus winRtStatus,
        bool hasOverlay,
        Guid overlayGuid,
        bool hasSystemStatus,
        bool systemOn,
        bool hasRegistry,
        bool registryOn)
    {
        if (hasWinRt)
        {
            return winRtStatus switch
            {
                EnergySaverStatus.On => true,
                EnergySaverStatus.Off => false,
                _ => hasOverlay && overlayGuid != Guid.Empty
                    ? overlayGuid == PowerModeGuids.BestEfficiency
                    : hasSystemStatus
                        ? systemOn
                        : hasRegistry && registryOn,
            };
        }

        if (hasOverlay && overlayGuid != Guid.Empty)
        {
            return overlayGuid == PowerModeGuids.BestEfficiency;
        }

        if (hasSystemStatus)
        {
            return systemOn;
        }

        if (hasRegistry)
        {
            return registryOn;
        }

        return false;
    }

    internal static bool HasRegistryRuntimeDrift()
    {
        if (!TryGetFromRegistry(out var registryOn))
        {
            return false;
        }

        if (!TryGetRuntimeOn(out var runtimeOn))
        {
            return false;
        }

        return registryOn != runtimeOn;
    }

    internal static bool TryGetFromRegistry(out bool isOn)
    {
        isOn = false;
        if (!TryReadRegistryState(EnergySaverStateValueName, out var stateValue)
            && !TryReadRegistryState(EcoModeStateValueName, out stateValue))
        {
            return false;
        }

        isOn = stateValue == EnergySaverStateOn;
        return true;
    }

    internal static bool TrySetInRegistry(bool enabled)
    {
        try
        {
            WriteRegistryValues(enabled);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static bool TrySetViaElevatedScript(bool enabled, out string? errorMessage)
    {
        errorMessage = null;
        var scriptPath = Path.Combine(Path.GetTempPath(), $"cmdpal-energy-saver-{Guid.NewGuid():N}.cmd");
        var stateValue = enabled ? EnergySaverStateOn : EnergySaverStateOff;

        try
        {
            var scriptContent =
                "@echo off" + Environment.NewLine +
                $"reg add HKLM\\SYSTEM\\CurrentControlSet\\Control\\Power /v {EnergySaverStateValueName} /t REG_DWORD /d {stateValue} /f >nul 2>&1" + Environment.NewLine +
                "if errorlevel 1 exit /b 1" + Environment.NewLine +
                $"reg add HKLM\\SYSTEM\\CurrentControlSet\\Control\\Power /v {EcoModeStateValueName} /t REG_DWORD /d {stateValue} /f >nul 2>&1" + Environment.NewLine +
                "if errorlevel 1 exit /b 1" + Environment.NewLine +
                $"reg add HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\whesvc /v {WhesvcWhatIfValueName} /t REG_DWORD /d 1 /f >nul 2>&1" + Environment.NewLine +
                "if errorlevel 1 exit /b 1" + Environment.NewLine +
                "for /f \"tokens=4\" %%i in ('powercfg /getactivescheme') do powercfg /setactive %%i >nul 2>&1" + Environment.NewLine +
                "exit /b 0" + Environment.NewLine;
            File.WriteAllText(scriptPath, scriptContent);

            if (!ElevatedProcessHelper.TryRunElevated("cmd.exe", $"/q /c \"{scriptPath}\"", out var exitCode, out var win32Error))
            {
                errorMessage = ElevatedProcessHelper.IsUacCancelled(win32Error)
                    ? Properties.Resources.power_mode_energy_saver_elevation_cancelled
                    : Properties.Resources.power_mode_energy_saver_admin_required;
                return false;
            }

            if (exitCode != 0)
            {
                errorMessage = Properties.Resources.power_mode_energy_saver_set_failed;
                return false;
            }

            return true;
        }
        finally
        {
            TryDeleteFile(scriptPath);
        }
    }

    internal static bool TryApplyOverlayScheme(bool enabled)
    {
        if (enabled)
        {
            var efficiency = PowerModeGuids.BestEfficiency;
            return PowerModeNative.PowerSetActiveOverlayScheme(ref efficiency) == PowerModeNative.ErrorSuccess;
        }

        var useAcProfile = !PowerSourceHelper.TryGetPowerStatus(out var status)
            || PowerSourceHelper.UseAcPowerProfile(PowerSourceHelper.GetPowerSourceKind(in status));
        var result = useAcProfile
            ? PowerModeNative.PowerGetUserConfiguredACPowerMode(out var userGuid)
            : PowerModeNative.PowerGetUserConfiguredDCPowerMode(out userGuid);

        if (result != PowerModeNative.ErrorSuccess)
        {
            userGuid = PowerModeGuids.Balanced;
        }

        return PowerModeNative.PowerSetActiveOverlayScheme(ref userGuid) == PowerModeNative.ErrorSuccess;
    }

    internal static bool TryRefreshActiveScheme()
    {
        if (PowerModeNative.PowerGetActiveScheme(IntPtr.Zero, out var activePolicyGuid) != PowerModeNative.ErrorSuccess
            || activePolicyGuid == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var schemeGuid = Marshal.PtrToStructure<Guid>(activePolicyGuid);
            return PowerModeNative.PowerSetActiveScheme(IntPtr.Zero, ref schemeGuid) == PowerModeNative.ErrorSuccess;
        }
        finally
        {
            _ = PowerModeNative.LocalFree(activePolicyGuid);
        }
    }

    internal static bool MatchesExpectedState(bool expectedOn) =>
        ResolveVisibleState() switch
        {
            ResolvedEnergySaverState.On => expectedOn,
            ResolvedEnergySaverState.Off => !expectedOn,
            _ => false,
        };

    private static bool TryGetRuntimeOn(out bool isOn)
    {
        isOn = false;
        if (TryGetWinRtStatus(out var winRtStatus))
        {
            if (winRtStatus == EnergySaverStatus.Disabled)
            {
                return false;
            }

            isOn = winRtStatus == EnergySaverStatus.On;
            return true;
        }

        if (TryGetEffectiveOverlay(out var overlayGuid) && overlayGuid != Guid.Empty)
        {
            isOn = overlayGuid == PowerModeGuids.BestEfficiency;
            return true;
        }

        if (PowerSourceHelper.TryGetEnergySaverActiveFromSystemStatus(out var systemOn))
        {
            isOn = systemOn;
            return true;
        }

        return false;
    }

    private static bool TryGetWinRtStatus(out EnergySaverStatus status)
    {
        status = EnergySaverStatus.Disabled;
        try
        {
            status = PowerManager.EnergySaverStatus;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetEffectiveOverlay(out Guid overlayGuid)
    {
        overlayGuid = Guid.Empty;
        return PowerModeNative.PowerGetEffectiveOverlayScheme(out overlayGuid) == PowerModeNative.ErrorSuccess;
    }

    private static void WriteRegistryValues(bool enabled)
    {
        var stateValue = enabled ? EnergySaverStateOn : EnergySaverStateOff;
        Registry.SetValue(PowerControlKeyPath, EnergySaverStateValueName, stateValue, RegistryValueKind.DWord);
        Registry.SetValue(PowerControlKeyPath, EcoModeStateValueName, stateValue, RegistryValueKind.DWord);
        Registry.SetValue(WhesvcKeyPath, WhesvcWhatIfValueName, 1, RegistryValueKind.DWord);
    }

    private static bool TryReadRegistryState(string valueName, out int stateValue)
    {
        stateValue = EnergySaverStateOff;
        try
        {
            var value = Registry.GetValue(PowerControlKeyPath, valueName, null);
            if (value is int intValue)
            {
                stateValue = intValue;
                return true;
            }

            if (value is byte byteValue)
            {
                stateValue = byteValue;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
