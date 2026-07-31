// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using PowerDisplay.Common.Models;
using static PowerDisplay.Common.Drivers.NativeConstants;

namespace PowerDisplay.Common.Drivers.DDC
{
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
        /// Discrete VCPs carry no discovery decision, so a failure here never discards the monitor: the
        /// control simply falls back to its default with the matching <see cref="MonitorReadFlags"/> bit
        /// unset, which beats dropping a display whose capabilities string parsed cleanly because 0xD6
        /// answered badly. Note this stage is not always downstream of a handle-liveness check — a caps
        /// string that parses but advertises none of 0x10/0x12/0x62 leaves nothing for
        /// <see cref="ContinuousVcpInitializer"/> to read and suppresses the probe — so a monitor can be
        /// published here with a handle no read has exercised.
        /// </remarks>
        public void Initialize(Monitor monitor)
        {
            foreach (var code in DiscreteCodes)
            {
                if (!IsSupported(monitor, code))
                {
                    continue;
                }

                var read = _reader.Read(monitor.Handle, code);
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
}
