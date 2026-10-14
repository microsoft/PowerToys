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
    /// <summary>
    /// Pins the numeric values against winerror.h. Every other assertion in this file addresses the
    /// codes by name, so a typo in a constant would move production and tests together and leave the
    /// whole suite green.
    /// </summary>
    [TestMethod]
    public void Constants_MatchWinerrorValues()
    {
        Assert.AreEqual(unchecked((int)0xC0262582), DdcErrorClassifier.ErrorGraphicsI2CErrorTransmittingData);
        Assert.AreEqual(unchecked((int)0xC0262583), DdcErrorClassifier.ErrorGraphicsI2CErrorReceivingData);
        Assert.AreEqual(unchecked((int)0xC0262584), DdcErrorClassifier.ErrorGraphicsDdcCiVcpNotSupported);
        Assert.AreEqual(unchecked((int)0xC0262585), DdcErrorClassifier.ErrorGraphicsDdcCiInvalidData);
        Assert.AreEqual(unchecked((int)0xC0262588), DdcErrorClassifier.ErrorGraphicsMcaInternalError);
        Assert.AreEqual(unchecked((int)0xC0262589), DdcErrorClassifier.ErrorGraphicsDdcCiInvalidMessageCommand);
        Assert.AreEqual(unchecked((int)0xC026258A), DdcErrorClassifier.ErrorGraphicsDdcCiInvalidMessageLength);
        Assert.AreEqual(unchecked((int)0xC026258B), DdcErrorClassifier.ErrorGraphicsDdcCiInvalidMessageChecksum);
        Assert.AreEqual(unchecked((int)0xC026258C), DdcErrorClassifier.ErrorGraphicsInvalidPhysicalMonitorHandle);
        Assert.AreEqual(unchecked((int)0xC026258D), DdcErrorClassifier.ErrorGraphicsMonitorNoLongerExists);
        Assert.AreEqual(
            unchecked((int)0xC02625D8),
            DdcErrorClassifier.ErrorGraphicsDdcCiCurrentCurrentValueGreaterThanMaximumValue);
        Assert.AreEqual(1460, DdcErrorClassifier.ErrorTimeout);
    }

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

    // See DdcErrorClassifier.IsTransient's <remarks> for why each of these stays out. The two
    // handle-class rows are load-bearing here specifically: ProbeCodeAsync consults IsTransient to
    // decide whether to retry, so an overlap with IsPhysicalMonitorUnavailable would keep hammering
    // a handle ProbeAsync already knows is gone.
    [DataRow(DdcErrorClassifier.ErrorGraphicsDdcCiVcpNotSupported)]
    [DataRow(DdcErrorClassifier.ErrorGraphicsInvalidPhysicalMonitorHandle)]
    [DataRow(DdcErrorClassifier.ErrorGraphicsMonitorNoLongerExists)]
    [DataRow(unchecked((int)0xC0262580))]
    [DataRow(unchecked((int)0xC0262581))]
    [DataRow(unchecked((int)0xC0262587))]
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
    public void Format_RendersTheUnsignedHexTheSdkDocuments()
    {
        // Marshal.GetLastWin32Error hands 0xC026258C back as a negative int, which greps against
        // nothing. The log has to carry the spelling winerror.h uses.
        Assert.AreEqual(
            "0xC026258C",
            DdcErrorClassifier.Format(DdcErrorClassifier.ErrorGraphicsInvalidPhysicalMonitorHandle));
        Assert.AreEqual("0x000005B4", DdcErrorClassifier.Format(DdcErrorClassifier.ErrorTimeout));
        Assert.AreEqual("none", DdcErrorClassifier.Format(null));
    }
}
