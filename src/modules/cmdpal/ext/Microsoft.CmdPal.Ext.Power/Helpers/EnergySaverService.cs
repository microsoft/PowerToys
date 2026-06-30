// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CmdPal.Ext.Power.Classes;
using Microsoft.CmdPal.Ext.Power.Enumerations;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.Windows.System.Power;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

internal sealed partial class EnergySaverService : IDisposable
{
    private const int VerifyAttempts = 12;
    private const int VerifyDelayMilliseconds = 500;

    private bool _subscribed;

    internal event EventHandler? EnergySaverChanged;

    internal EnergySaverSnapshot GetSnapshot()
    {
        var signals = EnergySaverSignalReader.Read();
        var state = EnergySaverStateResolver.ResolveVisibleState(in signals);
        if (state is ResolvedEnergySaverState.On or ResolvedEnergySaverState.Off or ResolvedEnergySaverState.NotAvailable)
        {
            return new EnergySaverSnapshot(state, CanReadStatus: true, CanAttemptSet: state != ResolvedEnergySaverState.NotAvailable);
        }

        return new EnergySaverSnapshot(ResolvedEnergySaverState.Unknown, CanReadStatus: false, CanAttemptSet: true);
    }

    internal bool HasRegistryRuntimeDrift()
    {
        if (!EnergySaverStateWriter.TryGetFromRegistry(out var registryOn))
        {
            return false;
        }

        var signals = EnergySaverSignalReader.Read();
        if (!EnergySaverSignalReader.TryGetRuntimeOn(in signals, out var runtimeOn))
        {
            return false;
        }

        return registryOn != runtimeOn;
    }

    internal bool TrySetEnergySaver(bool enabled, out string? errorMessage)
    {
        errorMessage = null;

        if (EnergySaverStateWriter.MatchesExpectedState(enabled))
        {
            EnergySaverChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (!EnergySaverStateWriter.TryApplyOverlayScheme(enabled))
        {
            errorMessage = Resources.power_mode_energy_saver_set_failed;
            return false;
        }

        if (EnergySaverStateWriter.TrySetInRegistry(enabled))
        {
            _ = EnergySaverStateWriter.TryRefreshActiveScheme();
            if (VerifyState(enabled))
            {
                EnergySaverChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
        }

        if (TryApplyElevatedRegistryAndVerify(enabled, out errorMessage))
        {
            EnergySaverChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        errorMessage ??= Resources.power_mode_energy_saver_requires_settings;
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

    private static bool TryApplyElevatedRegistryAndVerify(bool enabled, out string? errorMessage)
    {
        errorMessage = null;
        if (!EnergySaverStateWriter.TrySetViaElevatedScript(enabled, out errorMessage))
        {
            return false;
        }

        _ = EnergySaverStateWriter.TryRefreshActiveScheme();
        return VerifyState(enabled);
    }

    private static bool VerifyState(bool expectedOn)
    {
        for (var attempt = 0; attempt < VerifyAttempts; attempt++)
        {
            if (EnergySaverStateWriter.MatchesExpectedState(expectedOn))
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
}
