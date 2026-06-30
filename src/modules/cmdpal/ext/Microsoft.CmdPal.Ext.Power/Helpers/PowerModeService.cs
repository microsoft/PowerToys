// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Power.Classes;
using Microsoft.CmdPal.Ext.Power.Enumerations;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.Windows.System.Power;
using Windows.Win32;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

internal sealed partial class PowerModeService : IDisposable
{
    private bool _subscribed;

    internal event EventHandler? PowerModeChanged;

    internal PowerModeSnapshot GetSnapshot()
    {
        if (!PowerSourceReader.TryGetPowerStatus(out var status))
        {
            return CreateSnapshot(
                userMode: UserPowerMode.Unknown,
                effective: null,
                powerSourceKind: Enumerations.PowerSourceKind.Unknown,
                hasBattery: false,
                isOnAcPower: true,
                isCharging: false,
                canReadUserMode: false);
        }

        var powerSourceKind = PowerSourceReader.GetPowerSourceKind(in status);
        var hasBattery = PowerSourceReader.HasBattery(in status);
        var isOnAcPower = PowerSourceReader.IsOnAcPower(in status);
        var isCharging = PowerSourceReader.IsCharging(in status);
        var useAcProfile = PowerSourceReader.UseAcPowerProfile(powerSourceKind);
        var canReadUserMode = TryGetUserPowerMode(useAcProfile, out var userGuid);
        var userMode = canReadUserMode ? PowerModeCatalog.FromGuid(userGuid) : UserPowerMode.Unknown;

        EffectivePowerMode? effective = null;
        try
        {
            effective = PowerManager.EffectivePowerMode2;
        }
        catch
        {
            // WinRT API may be unavailable in some environments.
        }

        return CreateSnapshot(
            userMode,
            effective,
            powerSourceKind,
            hasBattery,
            isOnAcPower,
            isCharging,
            canReadUserMode);
    }

    internal bool SupportsPowerModeControl()
    {
        return GetSnapshot().CanReadUserMode;
    }

    internal bool TrySetUserPowerMode(UserPowerMode mode, out string? errorMessage)
    {
        errorMessage = null;
        if (mode is UserPowerMode.Unknown)
        {
            errorMessage = "Unknown power mode.";
            return false;
        }

        if (!PowerSourceReader.TryGetPowerStatus(out var status))
        {
            errorMessage = Resources.power_mode_not_supported;
            return false;
        }

        var powerSourceKind = PowerSourceReader.GetPowerSourceKind(in status);
        var useAcProfile = PowerSourceReader.UseAcPowerProfile(powerSourceKind);
        var guid = PowerModeCatalog.ToGuid(mode);

        var result = useAcProfile
            ? PInvoke.PowerSetUserConfiguredACPowerMode(in guid)
            : PInvoke.PowerSetUserConfiguredDCPowerMode(in guid);

        if (result == 0)
        {
            return true;
        }

        guid = PowerModeCatalog.ToGuid(mode);
        result = PInvoke.PowerSetActiveOverlayScheme(ref guid);
        if (result == 0)
        {
            return true;
        }

        errorMessage = Resources.power_mode_set_failed;
        return false;
    }

    internal void EnsureSubscribed()
    {
        if (_subscribed)
        {
            return;
        }

        try
        {
            PowerManager.EffectivePowerModeChanged += OnEffectivePowerModeChanged;
            _subscribed = true;
        }
        catch
        {
        }
    }

    internal void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        try
        {
            PowerManager.EffectivePowerModeChanged -= OnEffectivePowerModeChanged;
        }
        catch
        {
        }

        _subscribed = false;
    }

    public void Dispose()
    {
        Unsubscribe();
    }

    private void OnEffectivePowerModeChanged(object? sender, object e)
    {
        PowerModeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool TryGetUserPowerMode(bool useAcProfile, out Guid powerModeGuid)
    {
        powerModeGuid = Guid.Empty;
        var result = useAcProfile
            ? PInvoke.PowerGetUserConfiguredACPowerMode(out powerModeGuid)
            : PInvoke.PowerGetUserConfiguredDCPowerMode(out powerModeGuid);

        if (result == 0)
        {
            return true;
        }

        result = PInvoke.PowerGetActualOverlayScheme(out powerModeGuid);
        return result == 0;
    }

    private static PowerModeSnapshot CreateSnapshot(
        UserPowerMode userMode,
        EffectivePowerMode? effective,
        Microsoft.CmdPal.Ext.Power.Enumerations.PowerSourceKind powerSourceKind,
        bool hasBattery,
        bool isOnAcPower,
        bool isCharging,
        bool canReadUserMode) =>
        new(
            userMode,
            effective,
            powerSourceKind,
            hasBattery,
            isOnAcPower,
            isCharging,
            canReadUserMode);
}
