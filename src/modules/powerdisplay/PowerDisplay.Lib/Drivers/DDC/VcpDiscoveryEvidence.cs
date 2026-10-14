// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
            IReadOnlyDictionary<byte, VcpInitialValue> initialValues,
            bool isPhysicalMonitorUnavailable = false,
            IReadOnlyList<byte>? cacheSupplementedCodes = null)
        {
            CapabilitiesRaw = capabilitiesRaw;
            Capabilities = capabilities;
            InitialValues = initialValues;
            IsPhysicalMonitorUnavailable = isPhysicalMonitorUnavailable;
            CacheSupplementedCodes = cacheSupplementedCodes ?? Array.Empty<byte>();
        }

        public string CapabilitiesRaw { get; }

        public VcpCapabilities? Capabilities { get; }

        /// <summary>
        /// Gets the value discovery should start each feature at, keyed by VCP code. A code that is
        /// absent still owes the hardware a read; a code that is present carries whether this pass
        /// read it live or replayed it from the known-good cache.
        /// </summary>
        public IReadOnlyDictionary<byte, VcpInitialValue> InitialValues { get; }

        public bool IsPhysicalMonitorUnavailable { get; }

        /// <summary>
        /// Gets the codes only the known-good cache proved supported — neither the capabilities string
        /// nor a live probe reply covered them. Discovery logs these so a control that exists purely on
        /// persisted evidence can be told apart, in a support log, from one the hardware advertised.
        /// </summary>
        public IReadOnlyList<byte> CacheSupplementedCodes { get; }

        /// <summary>
        /// Folds this pass's probe observations and the monitor's known-good cache into the parsed
        /// capabilities.
        /// </summary>
        /// <remarks>
        /// The probe only runs when the capabilities string is unusable, so on the parsed path
        /// <paramref name="live"/> is empty and <paramref name="cached"/> is the only source that can
        /// still add anything.
        /// <para>
        /// A non-null <paramref name="parsedCapabilities"/> is extended <b>in place</b> and returned
        /// as the same instance — callers must not assume their input survives unmodified. Reusing
        /// the container rather than rebuilding it is deliberate: an entry parsed from the
        /// capabilities string carries discrete-value and custom-name metadata that a synthesized
        /// <see cref="VcpCodeInfo"/> cannot reproduce, so a copy would have to be a deep one and
        /// would silently lose whatever a later field addition forgot to carry over. Pinned by
        /// <c>Reconcile_ParsedCapabilitiesSurviveWhenNoProbeRan</c>, which asserts reference identity.
        /// </para>
        /// </remarks>
        public static VcpDiscoveryEvidence Reconcile(
            string capabilitiesRaw,
            VcpCapabilities? parsedCapabilities,
            IReadOnlyDictionary<byte, VcpProbeObservation> live,
            IReadOnlyDictionary<byte, KnownGoodVcpFeature> cached)
        {
            foreach (var observation in live.Values)
            {
                if (observation.IsPhysicalMonitorUnavailable)
                {
                    // A cache entry can establish feature support when a live feature read is
                    // merely inconclusive, but it cannot make an invalid native handle usable.
                    return new VcpDiscoveryEvidence(
                        capabilitiesRaw,
                        capabilities: null,
                        new Dictionary<byte, VcpInitialValue>(),
                        isPhysicalMonitorUnavailable: true);
                }
            }

            var capabilities = parsedCapabilities;
            var values = new Dictionary<byte, VcpInitialValue>();
            var cacheSupplementedCodes = new List<byte>();

            // Driven by what the probe reported and what the cache has already proven, rather than by
            // NativeConstants.ContinuousVcpCodes, which VcpFeatureProbeService only takes as the
            // default for its constructor-injected sweep list. Widening that sweep still needs a
            // matching edit in ContinuousVcpInitializer for the carried value to be used — this loop
            // only keeps the code from being dropped on the way there.
            var codes = new List<byte>(live.Keys);
            foreach (var cachedCode in cached.Keys)
            {
                if (!live.ContainsKey(cachedCode))
                {
                    codes.Add(cachedCode);
                }
            }

            foreach (var code in codes)
            {
                var probed = live.TryGetValue(code, out var observation);

                if (probed && observation.IsSuccess)
                {
                    capabilities = MarkSupported(capabilities, code);
                    values[code] = new VcpInitialValue(
                        observation.Value,
                        IsLive: true);
                    continue;
                }

                if (probed && observation.Replied)
                {
                    // The device answered this VCP code but reported a range that cannot scale a
                    // percentage. A reply is what proves the opcode is implemented — an unimplemented
                    // code fails with DDCCI_VCP_NOT_SUPPORTED and never sets Replied — so support is
                    // proven even though the value is not. Keep the feature reachable and let the
                    // initializer read it again or fall back to the cache.
                    capabilities = MarkSupported(capabilities, code);
                }

                if (cached.TryGetValue(code, out var knownGood))
                {
                    var cachedValue = knownGood.ToVcpFeatureValue();
                    if (cachedValue.IsValid)
                    {
                        // Reached in Maximum compatibility mode only: the caller hands an empty
                        // dictionary in normal mode, so cache evidence supplements parsed capabilities
                        // only there, where caps strings can omit support the hardware has proven.
                        //
                        // Positive evidence is never retracted, not even by a definitive
                        // DDCCI_VCP_NOT_SUPPORTED — panels whose DDC/CI engine is busy or asleep return
                        // that as a generic refusal, and dropping the last cached code would leave
                        // capabilities null and make BuildMonitorFromPhysical discard the whole display.
                        // Pinned by Reconcile_VcpNotSupportedStillUsesCachedPositiveEvidence; the full
                        // trade-off is argued in the PR description.
                        //
                        // Known limitation: seeding a false positive needs the panel to answer an
                        // unimplemented code with a non-zero, in-range maximum, since the common
                        // current=0/max=0 garbage reply fails VcpFeatureValue.IsValid and is never
                        // cached. One that does slip through is permanent — nothing retracts a cached
                        // code, so the control stays in the flyout until the user clears
                        // monitor_state.json. Accepted for the same reason the retraction above is:
                        // the panels this mode exists for refuse codes they do implement far more
                        // often than they answer codes they do not.
                        if (capabilities?.SupportsVcpCode(code) != true)
                        {
                            cacheSupplementedCodes.Add(code);
                        }

                        capabilities = MarkSupported(capabilities, code);

                        // The probe already issued at least one transaction for every code it touched —
                        // exhausting its paced retry budget, or stopping early on a definitive answer —
                        // so re-reading one of those in the same pass is pure I2C noise. A code the
                        // probe never saw still owes the hardware one read: the probe only runs when
                        // the caps string is unusable, so on the caps-parsed path nothing has confirmed
                        // the cached value and nothing would ever refresh it.
                        values[code] = new VcpInitialValue(
                            cachedValue,
                            IsLive: false,
                            PreferLiveRead: !probed);
                    }
                }
            }

            return new VcpDiscoveryEvidence(
                capabilitiesRaw,
                capabilities,
                values,
                cacheSupplementedCodes: cacheSupplementedCodes);
        }

        /// <summary>
        /// Records that <paramref name="code"/> is supported, creating the container when discovery
        /// produced no parsed capabilities and otherwise extending the caller's instance in place.
        /// Adds only: an entry parsed from the capabilities string carries discrete-value and custom-name
        /// metadata a synthesized <see cref="VcpCodeInfo"/> does not.
        /// </summary>
        private static VcpCapabilities MarkSupported(VcpCapabilities? capabilities, byte code)
        {
            capabilities ??= new VcpCapabilities();
            capabilities.SupportedVcpCodes.TryAdd(code, new VcpCodeInfo(code, VcpNames.GetCodeName(code)));
            return capabilities;
        }
    }

    /// <summary>
    /// The value discovery should start a continuous VCP feature at, and how much the hardware has
    /// already said about it this pass.
    /// </summary>
    /// <param name="Value">The device-native value to apply.</param>
    /// <param name="IsLive">
    /// True when this pass's probe read the value off the hardware, which is what lets the initializer
    /// set the matching <see cref="MonitorReadFlags"/> bit. Cached values stay non-live.
    /// </param>
    /// <param name="PreferLiveRead">
    /// True when the probe never touched this code, so the initializer owes the hardware one read
    /// before it falls back to <paramref name="Value"/>. Never set together with
    /// <paramref name="IsLive"/>.
    /// </param>
    internal readonly record struct VcpInitialValue(
        VcpFeatureValue Value,
        bool IsLive,
        bool PreferLiveRead = false);
}
