// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.Windows.System.Power;

namespace Microsoft.CmdPal.Ext.PowerMode.Helpers;

internal sealed partial class EnergySaverService : IDisposable
{
    private const int VerifyAttempts = 12;
    private const int VerifyDelayMilliseconds = 500;

    private bool _subscribed;

    internal event EventHandler? EnergySaverChanged;

    internal EnergySaverSnapshot GetSnapshot()
    {
        if (EnergySaverStateHelper.TryResolveEffectiveState(out var isOn, out var canRead))
        {
            var state = isOn ? ResolvedEnergySaverState.On : ResolvedEnergySaverState.Off;
            return new EnergySaverSnapshot(state, CanReadStatus: canRead, CanAttemptSet: true);
        }

        var hasSystemStatus = PowerSourceHelper.TryGetEnergySaverActiveFromSystemStatus(out var systemActive);
        var hasWinRtStatus = TryGetWinRtStatus(out var winRtStatus);

        if (!hasSystemStatus && !hasWinRtStatus)
        {
            return new EnergySaverSnapshot(ResolvedEnergySaverState.Unknown, CanReadStatus: false, CanAttemptSet: true);
        }

        var resolved = ResolveState(hasSystemStatus, systemActive, hasWinRtStatus, winRtStatus);
        return new EnergySaverSnapshot(resolved, CanReadStatus: true, CanAttemptSet: true);
    }

    internal bool TrySetEnergySaver(bool enabled, out string? errorMessage)
    {
        errorMessage = null;

        if (TryApplyLocallyAndVerify(enabled) || TryApplyElevatedAndVerify(enabled, out errorMessage))
        {
            EnergySaverChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        errorMessage ??= Properties.Resources.power_mode_energy_saver_requires_settings;
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
            PowerManager.EnergySaverStatusChanged += OnEnergySaverStatusChanged;
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
            PowerManager.EnergySaverStatusChanged -= OnEnergySaverStatusChanged;
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

    private static bool TryApplyLocallyAndVerify(bool enabled)
    {
        if (!EnergySaverStateHelper.TrySetInRegistry(enabled))
        {
            return false;
        }

        if (!EnergySaverStateHelper.TryApplyOverlayScheme(enabled))
        {
            return false;
        }

        return VerifyState(enabled);
    }

    private static bool TryApplyElevatedAndVerify(bool enabled, out string? errorMessage)
    {
        errorMessage = null;
        if (!EnergySaverStateHelper.TrySetViaElevatedScript(enabled, out errorMessage))
        {
            return false;
        }

        if (!EnergySaverStateHelper.TryApplyOverlayScheme(enabled))
        {
            errorMessage = Properties.Resources.power_mode_energy_saver_requires_settings;
            return false;
        }

        return VerifyState(enabled);
    }

    private static bool VerifyState(bool expectedOn)
    {
        for (var attempt = 0; attempt < VerifyAttempts; attempt++)
        {
            if (EnergySaverStateHelper.MatchesExpectedState(expectedOn))
            {
                return true;
            }

            Thread.Sleep(VerifyDelayMilliseconds);
        }

        return false;
    }

    private void OnEnergySaverStatusChanged(object? sender, object e)
    {
        EnergySaverChanged?.Invoke(this, EventArgs.Empty);
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

    private static ResolvedEnergySaverState ResolveState(
        bool hasSystemStatus,
        bool systemActive,
        bool hasWinRtStatus,
        EnergySaverStatus winRtStatus)
    {
        if (hasSystemStatus)
        {
            return systemActive ? ResolvedEnergySaverState.On : ResolvedEnergySaverState.Off;
        }

        if (!hasWinRtStatus)
        {
            return ResolvedEnergySaverState.Unknown;
        }

        return winRtStatus switch
        {
            EnergySaverStatus.On => ResolvedEnergySaverState.On,
            EnergySaverStatus.Off => ResolvedEnergySaverState.Off,
            EnergySaverStatus.Disabled => ResolvedEnergySaverState.NotAvailable,
            _ => ResolvedEnergySaverState.Unknown,
        };
    }
}
