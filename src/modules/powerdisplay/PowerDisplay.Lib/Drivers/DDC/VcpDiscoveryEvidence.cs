// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;

namespace PowerDisplay.Common.Drivers.DDC
{
    /// <summary>
    /// What discovery learned about one physical monitor before it is turned into a
    /// <see cref="Monitor"/>: the capabilities it advertises and the values already read off it.
    /// </summary>
    /// <remarks>
    /// This is the seam between the async fetch stage, which owns the I2C traffic, and the
    /// synchronous build stage, which owns the <see cref="Monitor"/> object. Carrying the probe's
    /// values across it is the point: without them the build stage re-reads every code the probe
    /// just answered.
    /// </remarks>
    internal sealed class VcpDiscoveryEvidence
    {
        public VcpDiscoveryEvidence(
            string capabilitiesRaw,
            VcpCapabilities? capabilities,
            IReadOnlyDictionary<byte, VcpFeatureValue> initialValues,
            bool isPhysicalMonitorUnavailable = false)
        {
            CapabilitiesRaw = capabilitiesRaw;
            Capabilities = capabilities;
            InitialValues = initialValues;
            IsPhysicalMonitorUnavailable = isPhysicalMonitorUnavailable;
        }

        public string CapabilitiesRaw { get; }

        public VcpCapabilities? Capabilities { get; }

        /// <summary>
        /// Gets the values this pass already read off the hardware, keyed by VCP code. A code that
        /// is absent still owes the hardware a read; a code that is present must not be re-read.
        /// </summary>
        public IReadOnlyDictionary<byte, VcpFeatureValue> InitialValues { get; }

        public bool IsPhysicalMonitorUnavailable { get; }

        /// <summary>
        /// Folds this pass's probe observations into the parsed capabilities.
        /// </summary>
        /// <remarks>
        /// The probe only runs when the capabilities string is unusable, so on the parsed path
        /// <paramref name="live"/> is empty and this is a pass-through.
        /// </remarks>
        public static VcpDiscoveryEvidence Reconcile(
            string capabilitiesRaw,
            VcpCapabilities? parsedCapabilities,
            IReadOnlyDictionary<byte, VcpProbeObservation> live)
        {
            foreach (var observation in live.Values)
            {
                if (observation.IsPhysicalMonitorUnavailable)
                {
                    // The handle itself is gone, so nothing gathered against it can be trusted and
                    // nothing more may be issued through it.
                    return new VcpDiscoveryEvidence(
                        capabilitiesRaw,
                        capabilities: null,
                        new Dictionary<byte, VcpFeatureValue>(),
                        isPhysicalMonitorUnavailable: true);
                }
            }

            var capabilities = parsedCapabilities;
            var values = new Dictionary<byte, VcpFeatureValue>();

            // Driven by what the probe reported rather than by NativeConstants.ContinuousVcpCodes,
            // which VcpFeatureProbeService only takes as the default for its constructor-injected
            // sweep list. Widening that sweep still needs a matching edit in
            // ContinuousVcpInitializer for the carried value to be used — this loop only keeps the
            // code from being dropped on the way there.
            foreach (var (code, observation) in live)
            {
                if (!observation.Replied)
                {
                    continue;
                }

                // A reply proves the device implements the opcode. Membership does not depend on
                // the value being usable — an unimplemented code fails with DDCCI_VCP_NOT_SUPPORTED
                // instead and never sets Replied.
                capabilities = MarkSupported(capabilities, code);

                if (observation.IsSuccess)
                {
                    values[code] = observation.Value;
                }
            }

            return new VcpDiscoveryEvidence(capabilitiesRaw, capabilities, values);
        }

        /// <summary>
        /// Records that <paramref name="code"/> is supported, creating the container when discovery
        /// produced no parsed capabilities. Adds only: an entry parsed from the capabilities string
        /// carries discrete-value and custom-name metadata a synthesized <see cref="VcpCodeInfo"/>
        /// does not.
        /// </summary>
        private static VcpCapabilities MarkSupported(VcpCapabilities? capabilities, byte code)
        {
            capabilities ??= new VcpCapabilities();
            capabilities.SupportedVcpCodes.TryAdd(code, new VcpCodeInfo(code, VcpNames.GetCodeName(code)));
            return capabilities;
        }
    }
}
