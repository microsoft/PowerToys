// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Power.Classes;
using Microsoft.CmdPal.Ext.Power.Enumerations;
using Microsoft.Windows.System.Power;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

internal static class EnergySaverStateResolver
{
    internal static bool ResolveIsOn(in EnergySaverSignals signals)
    {
        if (signals.HasWinRt)
        {
            return signals.WinRtStatus switch
            {
                EnergySaverStatus.On => true,
                EnergySaverStatus.Off => false,
                _ => signals.HasOverlay && signals.OverlayGuid != System.Guid.Empty
                    ? signals.OverlayGuid == PowerModeCatalog.BestEfficiency.Guid
                    : signals.HasSystemStatus
                        ? signals.SystemOn
                        : signals.HasRegistry && signals.RegistryOn,
            };
        }

        if (signals.HasOverlay && signals.OverlayGuid != System.Guid.Empty)
        {
            return signals.OverlayGuid == PowerModeCatalog.BestEfficiency.Guid;
        }

        if (signals.HasSystemStatus)
        {
            return signals.SystemOn;
        }

        if (signals.HasRegistry)
        {
            return signals.RegistryOn;
        }

        return false;
    }

    internal static ResolvedEnergySaverState ResolveVisibleState(in EnergySaverSignals signals)
    {
        if (signals.HasWinRt)
        {
            return signals.WinRtStatus switch
            {
                EnergySaverStatus.On => ResolvedEnergySaverState.On,
                EnergySaverStatus.Off => ResolvedEnergySaverState.Off,
                EnergySaverStatus.Disabled => ResolvedEnergySaverState.NotAvailable,
                _ => ResolvedEnergySaverState.Unknown,
            };
        }

        if (signals.HasOverlay && signals.OverlayGuid != System.Guid.Empty)
        {
            return signals.OverlayGuid == PowerModeCatalog.BestEfficiency.Guid
                ? ResolvedEnergySaverState.On
                : ResolvedEnergySaverState.Off;
        }

        if (signals.HasSystemStatus)
        {
            return signals.SystemOn ? ResolvedEnergySaverState.On : ResolvedEnergySaverState.Off;
        }

        if (signals.HasRegistry)
        {
            return signals.RegistryOn ? ResolvedEnergySaverState.On : ResolvedEnergySaverState.Off;
        }

        return ResolvedEnergySaverState.Unknown;
    }
}
