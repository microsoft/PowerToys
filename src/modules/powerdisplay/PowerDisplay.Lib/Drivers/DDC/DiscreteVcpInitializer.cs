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
    /// Discrete VCPs carry no discovery decision. Whether the physical-monitor handle is usable is
    /// already settled before this stage runs — by the maximum-compatibility probe through
    /// <see cref="VcpDiscoveryEvidence.Reconcile"/>, and by <see cref="ContinuousVcpInitializer"/>
    /// in both modes — and both drop the monitor on a handle-class error. Failing here only leaves
    /// the corresponding <see cref="MonitorReadFlags"/> bit unset, so a monitor whose continuous
    /// features were read successfully is never discarded because 0xD6 answered badly.
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
