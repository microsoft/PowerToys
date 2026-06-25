// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Windows.System.Power;
using Windows.Win32.System.Power;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

internal sealed partial class PowerModeService : IDisposable
{
    private bool _subscribed;

    internal event EventHandler? PowerModeChanged;

    internal PowerModeSnapshot GetSnapshot()
    {
        if (!PowerSourceHelper.TryGetPowerStatus(out var status))
        {
            return CreateSnapshot(
                userMode: UserPowerMode.Unknown,
                effective: null,
                powerSourceKind: PowerSourceKind.Unknown,
                hasBattery: false,
                isOnAcPower: true,
                isCharging: false,
                canReadUserMode: false);
        }

        var powerSourceKind = PowerSourceHelper.GetPowerSourceKind(in status);
        var hasBattery = PowerSourceHelper.HasBattery(in status);
        var isOnAcPower = PowerSourceHelper.IsOnAcPower(in status);
        var isCharging = PowerSourceHelper.IsCharging(in status);
        var useAcProfile = PowerSourceHelper.UseAcPowerProfile(powerSourceKind);
        var canReadUserMode = TryGetUserPowerMode(useAcProfile, out var userGuid);
        var userMode = canReadUserMode ? PowerModeMapper.FromGuid(userGuid) : UserPowerMode.Unknown;

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

        if (!PowerSourceHelper.TryGetPowerStatus(out var status))
        {
            errorMessage = Properties.Resources.power_mode_not_supported;
            return false;
        }

        var powerSourceKind = PowerSourceHelper.GetPowerSourceKind(in status);
        var useAcProfile = PowerSourceHelper.UseAcPowerProfile(powerSourceKind);
        var guid = PowerModeMapper.ToGuid(mode);

        var result = useAcProfile
            ? PowerModeNative.PowerSetUserConfiguredACPowerMode(ref guid)
            : PowerModeNative.PowerSetUserConfiguredDCPowerMode(ref guid);

        if (result == PowerModeNative.ErrorSuccess)
        {
            return true;
        }

        guid = PowerModeMapper.ToGuid(mode);
        result = PowerModeNative.PowerSetActiveOverlayScheme(ref guid);
        if (result == PowerModeNative.ErrorSuccess)
        {
            return true;
        }

        errorMessage = Properties.Resources.power_mode_set_failed;
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
            ? PowerModeNative.PowerGetUserConfiguredACPowerMode(out powerModeGuid)
            : PowerModeNative.PowerGetUserConfiguredDCPowerMode(out powerModeGuid);

        if (result == PowerModeNative.ErrorSuccess)
        {
            return true;
        }

        result = PowerModeNative.PowerGetActualOverlayScheme(out powerModeGuid);
        return result == PowerModeNative.ErrorSuccess;
    }

    private static PowerModeSnapshot CreateSnapshot(
        UserPowerMode userMode,
        EffectivePowerMode? effective,
        PowerSourceKind powerSourceKind,
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

internal readonly record struct PowerModeSnapshot(
    UserPowerMode UserMode,
    EffectivePowerMode? EffectiveMode,
    PowerSourceKind PowerSourceKind,
    bool HasBattery,
    bool IsOnAcPower,
    bool IsCharging,
    bool CanReadUserMode)
{
    internal bool UseAcPowerProfile => PowerSourceHelper.UseAcPowerProfile(PowerSourceKind);
}
