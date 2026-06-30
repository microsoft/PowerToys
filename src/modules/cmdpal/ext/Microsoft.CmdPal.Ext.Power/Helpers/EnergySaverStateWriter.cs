// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.CmdPal.Ext.Power.Enumerations;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

internal static class EnergySaverStateWriter
{
    internal const string PowerControlKeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power";
    internal const string WhesvcKeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\whesvc";
    internal const string EnergySaverStateValueName = "EnergySaverState";
    internal const string EcoModeStateValueName = "EcoModeState";
    internal const string WhesvcWhatIfValueName = "ECP_WhatIf";
    internal const int EnergySaverStateOn = 1;
    internal const int EnergySaverStateOff = 2;

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
                    ? Resources.power_mode_energy_saver_elevation_cancelled
                    : Resources.power_mode_energy_saver_admin_required;
                return false;
            }

            if (exitCode != 0)
            {
                errorMessage = Resources.power_mode_energy_saver_set_failed;
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
            var efficiency = PowerModeCatalog.BestEfficiency.Guid;
            return PInvoke.PowerSetActiveOverlayScheme(ref efficiency) == 0;
        }

        var useAcProfile = !PowerSourceReader.TryGetPowerStatus(out var status)
            || PowerSourceReader.UseAcPowerProfile(PowerSourceReader.GetPowerSourceKind(in status));
        var result = useAcProfile
            ? PInvoke.PowerGetUserConfiguredACPowerMode(out var userGuid)
            : PInvoke.PowerGetUserConfiguredDCPowerMode(out userGuid);

        if (result != 0)
        {
            userGuid = PowerModeCatalog.Balanced.Guid;
        }

        return PInvoke.PowerSetActiveOverlayScheme(ref userGuid) == 0;
    }

    internal static bool TryRefreshActiveScheme()
    {
        unsafe
        {
            if (PInvoke.PowerGetActiveScheme(null, out Guid* activePolicyGuid) != WIN32_ERROR.NO_ERROR)
            {
                return false;
            }

            try
            {
                var schemeGuid = *activePolicyGuid;
                return PInvoke.PowerSetActiveScheme(null, schemeGuid) == WIN32_ERROR.NO_ERROR;
            }
            finally
            {
                _ = PInvoke.LocalFree((HLOCAL)activePolicyGuid);
            }
        }
    }

    internal static bool MatchesExpectedState(bool expectedOn)
    {
        var signals = EnergySaverSignalReader.Read();
        return EnergySaverStateResolver.ResolveVisibleState(in signals) switch
        {
            ResolvedEnergySaverState.On => expectedOn,
            ResolvedEnergySaverState.Off => !expectedOn,
            _ => false,
        };
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
