// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers.DDC;
using PowerDisplay.Common.Models;

namespace PowerDisplay.UnitTests;

/// <summary>
/// Covers what discovery concludes from a set of probe observations: which VCP codes count as
/// supported, and which of them carry a value the build stage may use instead of re-reading.
/// </summary>
[TestClass]
public sealed class VcpDiscoveryEvidenceTests
{
    [TestMethod]
    public void Reconcile_SuccessfulProbeAdvertisesSupportAndCarriesTheValue()
    {
        // The whole point of the evidence: a code the probe read must not be read again.
        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: string.Empty,
            parsedCapabilities: null,
            live: Observations((0x10, VcpProbeObservation.Success(0x10, new VcpFeatureValue(30, 0, 100)))));

        Assert.IsTrue(result.Capabilities!.SupportsVcpCode(0x10));
        Assert.AreEqual(30, result.InitialValues[0x10].Current);
        Assert.AreEqual(100, result.InitialValues[0x10].Maximum);
    }

    [TestMethod]
    public void Reconcile_RepliedProbeWithUnusableRangeAdvertisesSupportWithoutAValue()
    {
        // The device answered, which proves the opcode exists, but max=0 cannot scale a percentage.
        // Support is kept so the control stays reachable; the absent value sends the initializer
        // back to the hardware for one more read.
        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: string.Empty,
            parsedCapabilities: null,
            live: Observations((0x62, VcpProbeObservation.Indeterminate(0x62, lastError: null, attempts: 1, replied: true))));

        Assert.IsTrue(result.Capabilities!.SupportsVcpCode(0x62));
        Assert.IsFalse(result.InitialValues.ContainsKey(0x62));
    }

    [TestMethod]
    public void Reconcile_UnansweredProbeDoesNotAdvertiseSupport()
    {
        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: string.Empty,
            parsedCapabilities: null,
            live: Observations((0x62, VcpProbeObservation.Indeterminate(
                0x62,
                DdcErrorClassifier.ErrorGraphicsDdcCiVcpNotSupported))));

        Assert.IsNull(result.Capabilities);
        Assert.AreEqual(0, result.InitialValues.Count);
    }

    [DataTestMethod]
    [DataRow(DdcErrorClassifier.ErrorGraphicsInvalidPhysicalMonitorHandle)]
    [DataRow(DdcErrorClassifier.ErrorGraphicsMonitorNoLongerExists)]
    public void Reconcile_PhysicalMonitorUnavailableDiscardsEverything(int errorCode)
    {
        // A dead handle invalidates the whole pass, including the codes that answered before it
        // died — nothing gathered through it can be trusted.
        var parsed = new VcpCapabilities();
        parsed.SupportedVcpCodes[0x10] = new VcpCodeInfo(0x10, "Brightness");

        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: "caps",
            parsedCapabilities: parsed,
            live: Observations(
                (0x10, VcpProbeObservation.Success(0x10, new VcpFeatureValue(30, 0, 100))),
                (0x12, VcpProbeObservation.Indeterminate(0x12, errorCode))));

        Assert.IsTrue(result.IsPhysicalMonitorUnavailable);
        Assert.IsNull(result.Capabilities);
        Assert.AreEqual(0, result.InitialValues.Count);
    }

    [TestMethod]
    public void Reconcile_ProbedCodeOutsideTheDefaultSweepIsStillHonoured()
    {
        // Reconcile is driven by what the probe reported, not by NativeConstants.ContinuousVcpCodes,
        // so widening VcpFeatureProbeService's constructor-injected sweep list does not silently drop
        // a code that answered. Honouring it here is necessary but not sufficient: the carried value
        // is only consumed for codes ContinuousVcpInitializer walks, so a widened sweep still needs a
        // matching edit there.
        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: string.Empty,
            parsedCapabilities: null,
            live: Observations((0x60, VcpProbeObservation.Success(0x60, new VcpFeatureValue(0x11, 0, 0x12)))));

        Assert.IsTrue(result.Capabilities!.SupportsVcpCode(0x60));
        Assert.AreEqual(0x11, result.InitialValues[0x60].Current);
    }

    [TestMethod]
    public void Reconcile_ParsedCapabilitiesSurviveWhenNoProbeRan()
    {
        // The probe only runs when the caps string is unusable, so the parsed path must be a
        // pass-through: no codes added, no values invented.
        var parsed = new VcpCapabilities();
        parsed.SupportedVcpCodes[0x10] = new VcpCodeInfo(0x10, "Brightness");

        var result = VcpDiscoveryEvidence.Reconcile(
            capabilitiesRaw: "caps",
            parsedCapabilities: parsed,
            live: new Dictionary<byte, VcpProbeObservation>());

        Assert.AreSame(parsed, result.Capabilities);
        Assert.AreEqual(0, result.InitialValues.Count);
        Assert.AreEqual("caps", result.CapabilitiesRaw);
    }

    private static Dictionary<byte, VcpProbeObservation> Observations(
        params (byte Code, VcpProbeObservation Observation)[] entries)
    {
        var observations = new Dictionary<byte, VcpProbeObservation>();
        foreach (var (code, observation) in entries)
        {
            observations[code] = observation;
        }

        return observations;
    }
}
