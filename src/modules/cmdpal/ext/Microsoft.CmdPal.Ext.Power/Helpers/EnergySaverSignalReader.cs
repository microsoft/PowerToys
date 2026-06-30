// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Power.Classes;
using Microsoft.Windows.System.Power;
using Windows.Win32;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

internal static class EnergySaverSignalReader
{
    internal static EnergySaverSignals Read()
    {
        var hasWinRt = TryGetWinRtStatus(out var winRtStatus);
        var hasOverlay = TryGetEffectiveOverlay(out var overlayGuid);
        var hasSystemStatus = PowerSourceReader.TryGetEnergySaverActiveFromSystemStatus(out var systemOn);
        var hasRegistry = EnergySaverStateWriter.TryGetFromRegistry(out var registryOn);

        return new EnergySaverSignals(
            hasWinRt,
            winRtStatus,
            hasOverlay,
            overlayGuid,
            hasSystemStatus,
            systemOn,
            hasRegistry,
            registryOn);
    }

    internal static bool TryGetWinRtStatus(out EnergySaverStatus status)
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

    internal static bool TryGetEffectiveOverlay(out Guid overlayGuid)
    {
        overlayGuid = Guid.Empty;
        return PInvoke.PowerGetEffectiveOverlayScheme(out overlayGuid) == 0;
    }

    internal static bool TryGetRuntimeOn(in EnergySaverSignals signals, out bool isOn)
    {
        isOn = false;
        if (signals.HasWinRt)
        {
            if (signals.WinRtStatus == EnergySaverStatus.Disabled)
            {
                return false;
            }

            isOn = signals.WinRtStatus == EnergySaverStatus.On;
            return true;
        }

        if (signals.HasOverlay && signals.OverlayGuid != Guid.Empty)
        {
            isOn = signals.OverlayGuid == PowerModeCatalog.BestEfficiency.Guid;
            return true;
        }

        if (signals.HasSystemStatus)
        {
            isOn = signals.SystemOn;
            return true;
        }

        return false;
    }
}
