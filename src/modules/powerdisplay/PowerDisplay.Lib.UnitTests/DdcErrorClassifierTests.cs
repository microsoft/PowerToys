// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers.DDC;

namespace PowerDisplay.UnitTests;

/// <summary>
/// Pins the membership of the DDC/CI error sets. The retry budget and the drop-the-monitor
/// decision both hang off these predicates, so a code silently moving between sets changes how
/// much I2C traffic a flaky panel attracts and whether it stays visible at all.
/// </summary>
[TestClass]
public sealed class DdcErrorClassifierTests
{
    [DataTestMethod]
    [DataRow(DdcErrorClassifier.ErrorGraphicsI2CErrorTransmittingData)]
    [DataRow(DdcErrorClassifier.ErrorGraphicsI2CErrorReceivingData)]
    [DataRow(DdcErrorClassifier.ErrorGraphicsDdcCiInvalidData)]
    [DataRow(DdcErrorClassifier.ErrorGraphicsMcaInternalError)]
    [DataRow(DdcErrorClassifier.ErrorGraphicsDdcCiInvalidMessageCommand)]
    [DataRow(DdcErrorClassifier.ErrorGraphicsDdcCiInvalidMessageLength)]
    [DataRow(DdcErrorClassifier.ErrorGraphicsDdcCiInvalidMessageChecksum)]
    [DataRow(DdcErrorClassifier.ErrorGraphicsDdcCiCurrentCurrentValueGreaterThanMaximumValue)]
    [DataRow(DdcErrorClassifier.ErrorTimeout)]
    public void IsTransient_AcceptsRetryableFailures(int errorCode) =>
        Assert.IsTrue(DdcErrorClassifier.IsTransient(errorCode));

    [DataTestMethod]

    // The device's final answer: it does not implement the opcode. Retrying only costs I2C
    // transactions, and it is what lets ProbeCodeAsync stop after a single attempt.
    [DataRow(DdcErrorClassifier.ErrorGraphicsDdcCiVcpNotSupported)]

    // Handle-class failures are owned by IsPhysicalMonitorUnavailable, which aborts the whole
    // probe — treating them as transient would keep hammering a dead handle.
    [DataRow(DdcErrorClassifier.ErrorGraphicsInvalidPhysicalMonitorHandle)]
    [DataRow(DdcErrorClassifier.ErrorGraphicsMonitorNoLongerExists)]

    // Permanent bus-level facts: I2C_NOT_SUPPORTED and I2C_DEVICE_DOES_NOT_EXIST.
    [DataRow(unchecked((int)0xC0262580))]
    [DataRow(unchecked((int)0xC0262581))]

    // MCA_INVALID_CAPABILITIES_STRING belongs to the capabilities path, not to a VCP read.
    [DataRow(unchecked((int)0xC0262587))]

    // DDCCI_MONITOR_RETURNED_INVALID_TIMING_STATUS_BYTE reads like a sibling of the framing codes
    // but is raised only by the get-timing-report command, never by GetVCPFeatureAndVCPFeatureReply.
    [DataRow(unchecked((int)0xC0262586))]
    [DataRow(0)]
    public void IsTransient_RejectsEverythingElse(int errorCode) =>
        Assert.IsFalse(DdcErrorClassifier.IsTransient(errorCode));

    [DataTestMethod]
    [DataRow(DdcErrorClassifier.ErrorGraphicsInvalidPhysicalMonitorHandle)]
    [DataRow(DdcErrorClassifier.ErrorGraphicsMonitorNoLongerExists)]
    public void IsPhysicalMonitorUnavailable_AcceptsHandleClassFailures(int errorCode) =>
        Assert.IsTrue(DdcErrorClassifier.IsPhysicalMonitorUnavailable(errorCode));

    [DataTestMethod]
    [DataRow(DdcErrorClassifier.ErrorGraphicsDdcCiVcpNotSupported)]
    [DataRow(DdcErrorClassifier.ErrorGraphicsI2CErrorTransmittingData)]
    [DataRow(DdcErrorClassifier.ErrorTimeout)]
    [DataRow(0)]
    public void IsPhysicalMonitorUnavailable_RejectsFeatureLevelFailures(int errorCode) =>
        Assert.IsFalse(DdcErrorClassifier.IsPhysicalMonitorUnavailable(errorCode));

    [TestMethod]
    public void HandleClassFailuresAreNeverTransient()
    {
        // The two sets must stay disjoint: ProbeCodeAsync consults IsTransient to decide whether to
        // retry, and ProbeAsync consults the resulting disposition to decide whether to abandon the
        // handle. An overlap would retry against a handle that is already gone.
        Assert.IsFalse(DdcErrorClassifier.IsTransient(DdcErrorClassifier.ErrorGraphicsInvalidPhysicalMonitorHandle));
        Assert.IsFalse(DdcErrorClassifier.IsTransient(DdcErrorClassifier.ErrorGraphicsMonitorNoLongerExists));
    }
}
