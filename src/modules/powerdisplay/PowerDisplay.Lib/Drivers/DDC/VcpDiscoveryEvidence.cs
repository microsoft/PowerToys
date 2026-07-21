// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single type

using System.Collections.Generic;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Utils;
using static PowerDisplay.Common.Drivers.NativeConstants;

namespace PowerDisplay.Common.Drivers.DDC;

internal readonly record struct VcpInitialValue(
    VcpFeatureValue Value,
    VcpObservationSource Source,
    bool IsLive,
    bool PreferLiveRead = false);

internal sealed class VcpDiscoveryEvidence
{
    private static readonly byte[] ContinuousCodes =
    {
        VcpCodeBrightness,
        VcpCodeContrast,
        VcpCodeVolume,
    };

    public VcpDiscoveryEvidence(
        string capabilitiesRaw,
        VcpCapabilities? capabilities,
        IReadOnlyDictionary<byte, VcpInitialValue> initialValues)
    {
        CapabilitiesRaw = capabilitiesRaw;
        Capabilities = capabilities;
        InitialValues = initialValues;
    }

    public string CapabilitiesRaw { get; }

    public VcpCapabilities? Capabilities { get; }

    public IReadOnlyDictionary<byte, VcpInitialValue> InitialValues { get; }

    public static VcpDiscoveryEvidence Reconcile(
        string capabilitiesRaw,
        VcpCapabilities? parsedCapabilities,
        IReadOnlyDictionary<byte, VcpProbeObservation> live,
        IReadOnlyDictionary<byte, KnownGoodVcpFeature> cached,
        bool includeCache)
    {
        var capabilities = parsedCapabilities;
        var values = new Dictionary<byte, VcpInitialValue>();

        foreach (var code in ContinuousCodes)
        {
            var parsedCapabilitiesAdvertiseCode = parsedCapabilities?.SupportsVcpCode(code) == true;

            if (live.TryGetValue(code, out var observation) && observation.IsSuccess)
            {
                capabilities ??= new VcpCapabilities();
                capabilities.SupportedVcpCodes[code] = new VcpCodeInfo(code, VcpNames.GetCodeName(code));
                values[code] = new VcpInitialValue(
                    observation.Value,
                    VcpObservationSource.MaximumCompatibilityProbe,
                    IsLive: true);
                continue;
            }

            if (includeCache &&
                cached.TryGetValue(code, out var knownGood))
            {
                var cachedValue = knownGood.ToVcpFeatureValue();
                if (cachedValue.IsValid)
                {
                    // includeCache is only enabled by Maximum compatibility mode; when it is, exact-ID
                    // cache evidence intentionally supplements parsed capabilities because caps strings
                    // can omit previously proven continuous VCP support.
                    capabilities ??= new VcpCapabilities();
                    capabilities.SupportedVcpCodes[code] = new VcpCodeInfo(code, VcpNames.GetCodeName(code));
                    values[code] = new VcpInitialValue(
                        cachedValue,
                        knownGood.Source,
                        IsLive: false,
                        PreferLiveRead: parsedCapabilitiesAdvertiseCode);
                }
            }
        }

        return new VcpDiscoveryEvidence(capabilitiesRaw, capabilities, values);
    }
}
