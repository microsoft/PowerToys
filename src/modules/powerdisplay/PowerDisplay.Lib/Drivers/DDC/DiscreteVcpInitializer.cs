// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using PowerDisplay.Common.Models;
using static PowerDisplay.Common.Drivers.NativeConstants;

namespace PowerDisplay.Common.Drivers.DDC;

internal sealed class DiscreteVcpInitializer
{
    private static readonly byte[] DiscreteCodes =
    {
        VcpCodeSelectColorPreset,
        VcpCodeInputSource,
        VcpCodePowerMode,
    };

    private readonly IVcpFeatureReader _reader;

    public DiscreteVcpInitializer(IVcpFeatureReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Reads the discrete VCP features a monitor advertises.
    /// </summary>
    /// <remarks>
    /// Discrete VCPs carry no discovery decision. A handle-class error is acted on by the stages
    /// that run first — the maximum-compatibility probe through
    /// <see cref="VcpDiscoveryEvidence.Reconcile"/>, and <see cref="ContinuousVcpInitializer"/> in
    /// both modes — so by the time a monitor reaches this stage the usual case is that its handle
    /// has already answered. It is not guaranteed to have been exercised: a capabilities string that
    /// parses but advertises none of 0x10/0x12/0x62 leaves nothing for the continuous stage to read
    /// and suppresses the probe, making these the first reads on the handle. Even then, failing here
    /// only leaves the corresponding <see cref="MonitorReadFlags"/> bit unset — the monitor is kept
    /// and its discrete controls fall back to their defaults, which is preferable to discarding a
    /// display whose capabilities string parsed cleanly because 0xD6 answered badly.
    /// </remarks>
    public void Initialize(Monitor monitor, IntPtr handle)
    {
        foreach (var code in DiscreteCodes)
        {
            if (!IsSupported(monitor, code))
            {
                continue;
            }

            var read = _reader.Read(handle, code);
            if (!read.IsSuccess)
            {
                Logger.LogError($"[{monitor.Id}] Failed to read VCP 0x{code:X2}, error code: {read.ErrorCode}");
                continue;
            }

            ApplyValue(monitor, code, (int)read.Current);
        }
    }

    private static bool IsSupported(Monitor monitor, byte code) => code switch
    {
        VcpCodeSelectColorPreset => monitor.SupportsColorTemperature,
        VcpCodeInputSource => monitor.SupportsInputSource,
        VcpCodePowerMode => monitor.SupportsPowerState,
        _ => false,
    };

    private static void ApplyValue(Monitor monitor, byte code, int current)
    {
        switch (code)
        {
            case VcpCodeSelectColorPreset:
                monitor.CurrentColorTemperature = current;
                monitor.ReadValues |= MonitorReadFlags.ColorTemperature;
                break;

            case VcpCodeInputSource:
                monitor.CurrentInputSource = current;
                monitor.ReadValues |= MonitorReadFlags.InputSource;
                break;

            case VcpCodePowerMode:
                monitor.CurrentPowerState = current;
                monitor.ReadValues |= MonitorReadFlags.PowerState;
                break;
        }
    }
}
