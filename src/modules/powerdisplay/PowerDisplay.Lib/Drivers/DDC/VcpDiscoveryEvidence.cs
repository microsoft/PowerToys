// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable SA1649 // File name should match first type name

using System.Collections.Generic;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;
using static PowerDisplay.Common.Drivers.NativeConstants;

namespace PowerDisplay.Common.Drivers.DDC;

internal readonly record struct VcpInitialValue(
    VcpFeatureValue Value,
    bool IsLive,
    bool PreferLiveRead = false);

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
        CacheSupplementedCodes = cacheSupplementedCodes ?? System.Array.Empty<byte>();
    }

    public string CapabilitiesRaw { get; }

    public VcpCapabilities? Capabilities { get; }

    public IReadOnlyDictionary<byte, VcpInitialValue> InitialValues { get; }

    public bool IsPhysicalMonitorUnavailable { get; }

    /// <summary>
    /// Gets the codes only the known-good cache proved supported — neither the capabilities string
    /// nor a live probe reply covered them. Discovery logs these so a control that exists purely on
    /// persisted evidence can be told apart, in a support log, from one the hardware advertised.
    /// </summary>
    public IReadOnlyList<byte> CacheSupplementedCodes { get; }

    public static VcpDiscoveryEvidence Reconcile(
        string capabilitiesRaw,
        VcpCapabilities? parsedCapabilities,
        IReadOnlyDictionary<byte, VcpProbeObservation> live,
        IReadOnlyDictionary<byte, KnownGoodVcpFeature> cached)
    {
        foreach (var observation in live.Values)
        {
            if (observation.Disposition == VcpProbeDisposition.PhysicalMonitorUnavailable)
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

        // Evidence is merged into the parsed instance rather than into a copy: the caller hands
        // ownership of parsedCapabilities to Reconcile, and the merged object is published as
        // Monitor.VcpCapabilitiesInfo.
        var capabilities = parsedCapabilities;
        var values = new Dictionary<byte, VcpInitialValue>();
        var cacheSupplementedCodes = new List<byte>();

        foreach (var code in ContinuousVcpCodes)
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
                // percentage. Support is proven even though the value is not, so keep the feature
                // reachable and let the initializer read it again or fall back to the cache.
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
                    // cached. One that does slip through is permanent in practice:
                    // RemoveKnownGoodFeatures only fires once settings retention drops the monitor
                    // entry, which requires 30 days both undiscovered and unhidden, so a monitor in
                    // daily use never reclaims it.
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
    /// produced no parsed capabilities. Evidence may only add support: an entry parsed from the
    /// capabilities string carries discrete-value and custom-name metadata that a synthesized
    /// <see cref="VcpCodeInfo"/> does not, so an existing entry is never overwritten.
    /// </summary>
    private static VcpCapabilities MarkSupported(VcpCapabilities? capabilities, byte code)
    {
        capabilities ??= new VcpCapabilities();
        if (!capabilities.SupportsVcpCode(code))
        {
            capabilities.SupportedVcpCodes[code] = new VcpCodeInfo(code, VcpNames.GetCodeName(code));
        }

        return capabilities;
    }
}
