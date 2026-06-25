// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.Win32;

namespace Microsoft.CmdPal.Ext.PowerMode.Helpers;

internal static class EnergySaverStateHelper
{
    internal const string PowerControlKeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power";
    internal const string WhesvcKeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\whesvc";
    internal const string EnergySaverStateValueName = "EnergySaverState";
    internal const string EcoModeStateValueName = "EcoModeState";
    internal const string WhesvcWhatIfValueName = "ECP_WhatIf";
    internal const int EnergySaverStateOn = 1;
    internal const int EnergySaverStateOff = 2;

    internal static bool TryResolveEffectiveState(out bool isOn, out bool canRead)
    {
        canRead = false;
        isOn = false;

        var hasRegistry = TryGetFromRegistry(out var registryOn);
        var hasOverlay = TryGetEffectiveOverlay(out var overlayGuid);

        if (!hasRegistry && !hasOverlay)
        {
            if (PowerSourceHelper.TryGetEnergySaverActiveFromSystemStatus(out var systemOn))
            {
                isOn = systemOn;
                canRead = true;
                return true;
            }

            return false;
        }

        canRead = true;
        if (hasRegistry && hasOverlay)
        {
            // Win11 24H2 engages energy saver by forcing the efficiency overlay.
            isOn = registryOn && overlayGuid == PowerModeGuids.BestEfficiency;
            return true;
        }

        if (hasRegistry)
        {
            isOn = registryOn;
            return true;
        }

        isOn = overlayGuid == PowerModeGuids.BestEfficiency;
        return true;
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
                "exit /b 0" + Environment.NewLine;
            File.WriteAllText(scriptPath, scriptContent);

            if (!ElevatedProcessHelper.TryRunElevated("cmd.exe", $"/q /c \"\"{scriptPath}\"\"", out var exitCode, out var win32Error))
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

    internal static bool MatchesExpectedState(bool expectedOn) =>
        TryResolveEffectiveState(out var isOn, out _) && isOn == expectedOn;

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
