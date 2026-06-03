// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class MonitorIdentityTests
{
    [TestMethod]
    public void FromDevicePath_StripsTrailingGuid()
    {
        var input = @"\\?\DISPLAY#DELD1A8#5&abc123&0&UID12345#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
        var expected = @"\\?\DISPLAY#DELD1A8#5&abc123&0&UID12345";

        var result = MonitorIdentity.FromDevicePath(input);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void FromDevicePath_NoTrailingGuid_ReturnsInputUnchanged()
    {
        var input = @"\\?\DISPLAY#DELD1A8#5&abc123&0&UID12345";

        var result = MonitorIdentity.FromDevicePath(input);

        Assert.AreEqual(input, result);
    }

    [TestMethod]
    public void FromDevicePath_NullOrEmpty_ReturnsEmptyString()
    {
        Assert.AreEqual(string.Empty, MonitorIdentity.FromDevicePath(null!));
        Assert.AreEqual(string.Empty, MonitorIdentity.FromDevicePath(string.Empty));
    }

    [TestMethod]
    public void PnpHardwareKeyFromDevicePath_ReturnsHardwareSegments()
    {
        var input = @"\\?\DISPLAY#BOE0900#4&40f4dee&0&UID8388688#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
        var expected = @"BOE0900#4&40f4dee&0&UID8388688";

        Assert.AreEqual(expected, MonitorIdentity.PnpHardwareKeyFromDevicePath(input));
    }

    [TestMethod]
    public void PnpHardwareKeyFromInstanceName_StripsSuffixAndNormalizesSeparator()
    {
        var input = @"DISPLAY\BOE0900\4&40f4dee&0&UID8388688_0";
        var expected = @"BOE0900#4&40f4dee&0&UID8388688";

        Assert.AreEqual(expected, MonitorIdentity.PnpHardwareKeyFromInstanceName(input));
    }

    [TestMethod]
    public void PnpHardwareKey_CrossFormat_ProducesSameKey()
    {
        // The whole point of the PnP key: a WMI InstanceName and the matching DevicePath
        // for the same physical monitor must produce identical keys, so WMI brightness
        // instances can be joined to QueryDisplayConfig targets with a single lookup —
        // even on dual-internal-panel devices (Yoga Book 9i, Zenbook Duo) where the
        // EdidId alone collides.
        var instanceName = @"DISPLAY\BOE0900\4&40f4dee&0&UID8388688_0";
        var devicePath = @"\\?\DISPLAY#BOE0900#4&40f4dee&0&UID8388688#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

        var keyFromInstance = MonitorIdentity.PnpHardwareKeyFromInstanceName(instanceName);
        var keyFromDevicePath = MonitorIdentity.PnpHardwareKeyFromDevicePath(devicePath);

        Assert.AreEqual(keyFromInstance, keyFromDevicePath);
        Assert.IsFalse(string.IsNullOrEmpty(keyFromInstance), "expected non-empty key");
    }

    [TestMethod]
    public void PnpHardwareKey_DualInternalPanel_DistinguishesByUid()
    {
        // Yoga Book 9i style: two identical internal panels (same EdidId BOE0900) with
        // different PnP UIDs. The PnP key must differ so the two WMI brightness instances
        // each pair with the correct MonitorDisplayInfo.
        var panelA = @"DISPLAY\BOE0900\4&abcdef&0&UID111_0";
        var panelB = @"DISPLAY\BOE0900\4&abcdef&0&UID222_0";

        Assert.AreNotEqual(
            MonitorIdentity.PnpHardwareKeyFromInstanceName(panelA),
            MonitorIdentity.PnpHardwareKeyFromInstanceName(panelB));
    }

    [TestMethod]
    public void PnpHardwareKeyFromInstanceName_MultiDigitSuffix_StrippedCorrectly()
    {
        // WMI instance suffix can be _0, _1, _10, etc. — LastIndexOf('_') ensures we
        // strip only the trailing suffix, not an underscore inside the UID itself.
        var input = @"DISPLAY\BOE0900\4&40f4dee&0&UID8388688_12";
        var expected = @"BOE0900#4&40f4dee&0&UID8388688";

        Assert.AreEqual(expected, MonitorIdentity.PnpHardwareKeyFromInstanceName(input));
    }

    [TestMethod]
    public void PnpHardwareKey_NullEmptyOrMalformed_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, MonitorIdentity.PnpHardwareKeyFromDevicePath(null));
        Assert.AreEqual(string.Empty, MonitorIdentity.PnpHardwareKeyFromDevicePath(string.Empty));
        Assert.AreEqual(string.Empty, MonitorIdentity.PnpHardwareKeyFromDevicePath(@"\\?\DISPLAY"));

        Assert.AreEqual(string.Empty, MonitorIdentity.PnpHardwareKeyFromInstanceName(null));
        Assert.AreEqual(string.Empty, MonitorIdentity.PnpHardwareKeyFromInstanceName(string.Empty));
        Assert.AreEqual(string.Empty, MonitorIdentity.PnpHardwareKeyFromInstanceName(@"DISPLAY"));
        Assert.AreEqual(string.Empty, MonitorIdentity.PnpHardwareKeyFromInstanceName(@"DISPLAY\BOE0900"));
    }

    [TestMethod]
    public void EdidIdFromMonitorId_NewFormat_ReturnsMiddleSegment()
    {
        var input = @"\\?\DISPLAY#DELD1A8#5&abc123&0&UID12345";

        Assert.AreEqual("DELD1A8", MonitorIdentity.EdidIdFromMonitorId(input));
    }

    [TestMethod]
    public void EdidIdFromMonitorId_RawDevicePathWithTrailingGuid_ReturnsMiddleSegment()
    {
        // The Phase 0 classification log calls EdidIdFromMonitorId with a raw
        // QueryDisplayConfig DevicePath (still carrying the trailing "#{guid}" segment).
        // The helper must extract the EdidId correctly for that form too.
        var input = @"\\?\DISPLAY#DELD1A8#5&abc123&0&UID12345#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

        Assert.AreEqual("DELD1A8", MonitorIdentity.EdidIdFromMonitorId(input));
    }

    [TestMethod]
    public void EdidIdFromMonitorId_SameModelMonitorsProduceSameId()
    {
        // Two Dell U2723QE on different ports share an EdidId but have different UIDs.
        // Logging the EdidId identifies the model for crash correlation without leaking
        // per-unit identifiers.
        var portA = @"\\?\DISPLAY#DELD1A8#5&abc&0&UID111#{guid}";
        var portB = @"\\?\DISPLAY#DELD1A8#5&xyz&0&UID222#{guid}";

        Assert.AreEqual(
            MonitorIdentity.EdidIdFromMonitorId(portA),
            MonitorIdentity.EdidIdFromMonitorId(portB));
    }

    [TestMethod]
    public void EdidIdFromMonitorId_NullEmptyOrMalformed_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, MonitorIdentity.EdidIdFromMonitorId(null));
        Assert.AreEqual(string.Empty, MonitorIdentity.EdidIdFromMonitorId(string.Empty));
        Assert.AreEqual(string.Empty, MonitorIdentity.EdidIdFromMonitorId(@"\\?\DISPLAY"));
        Assert.AreEqual(string.Empty, MonitorIdentity.EdidIdFromMonitorId(@"\\?\DISPLAY#"));
        Assert.AreEqual(string.Empty, MonitorIdentity.EdidIdFromMonitorId("DDC_DELD1A8_1"));
    }

    [TestMethod]
    public void IsLegacyId_MatchesDdcAndWmiPrefixWithDigitSuffix()
    {
        Assert.IsTrue(MonitorIdentity.IsLegacyId("DDC_DELD1A8_1"));
        Assert.IsTrue(MonitorIdentity.IsLegacyId("WMI_BOE0900_2"));
        Assert.IsTrue(MonitorIdentity.IsLegacyId("DDC_Unknown_3"));
        Assert.IsTrue(MonitorIdentity.IsLegacyId("WMI_Unknown_10"));
    }

    [TestMethod]
    public void IsLegacyId_RejectsNewFormatAndMalformed()
    {
        Assert.IsFalse(MonitorIdentity.IsLegacyId(null));
        Assert.IsFalse(MonitorIdentity.IsLegacyId(string.Empty));
        Assert.IsFalse(MonitorIdentity.IsLegacyId(@"\\?\DISPLAY#DELD1A8#5&abc&0&UID12345"));
        Assert.IsFalse(MonitorIdentity.IsLegacyId("HDR_DELD1A8_1"));   // unknown source prefix
        Assert.IsFalse(MonitorIdentity.IsLegacyId("DDC_DELD1A8_"));    // empty number suffix
        Assert.IsFalse(MonitorIdentity.IsLegacyId("DDC__1"));           // empty EdidId segment
        Assert.IsFalse(MonitorIdentity.IsLegacyId("DDC_DELD1A8_abc"));  // non-digit suffix
    }

    [TestMethod]
    public void LegacyEdidId_ReturnsMiddleSegment()
    {
        Assert.AreEqual("DELD1A8", MonitorIdentity.LegacyEdidId("DDC_DELD1A8_1"));
        Assert.AreEqual("BOE0900", MonitorIdentity.LegacyEdidId("WMI_BOE0900_2"));
    }

    [TestMethod]
    public void LegacyEdidId_UnknownPlaceholderReturnsEmpty()
    {
        // "Unknown" is the placeholder PowerDisplay wrote when EDID was unavailable —
        // it can never identify a specific monitor, so migration must skip these.
        Assert.AreEqual(string.Empty, MonitorIdentity.LegacyEdidId("DDC_Unknown_1"));
        Assert.AreEqual(string.Empty, MonitorIdentity.LegacyEdidId("WMI_Unknown_2"));
    }

    [TestMethod]
    public void LegacyEdidId_NewFormatReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, MonitorIdentity.LegacyEdidId(@"\\?\DISPLAY#DELD1A8#5&abc&0&UID12345"));
        Assert.AreEqual(string.Empty, MonitorIdentity.LegacyEdidId(null));
        Assert.AreEqual(string.Empty, MonitorIdentity.LegacyEdidId(string.Empty));
    }

    [TestMethod]
    public void LegacyMonitorNumber_ReturnsTrailingDigits()
    {
        Assert.AreEqual(1, MonitorIdentity.LegacyMonitorNumber("DDC_DELD1A8_1"));
        Assert.AreEqual(2, MonitorIdentity.LegacyMonitorNumber("WMI_BOE0900_2"));
        Assert.AreEqual(10, MonitorIdentity.LegacyMonitorNumber("DDC_DELD1A8_10"));
    }

    [TestMethod]
    public void LegacyMonitorNumber_ParsesEvenForUnknownEdid()
    {
        // LegacyEdidId returns empty for the "Unknown" placeholder, but the trailing
        // digits are still well-formed. Callers gate on the (EdidId, MonitorNumber)
        // pair, so reading the number cleanly is fine here.
        Assert.AreEqual(3, MonitorIdentity.LegacyMonitorNumber("DDC_Unknown_3"));
    }

    [TestMethod]
    public void LegacyMonitorNumber_NewFormatOrMalformedReturnsZero()
    {
        Assert.AreEqual(0, MonitorIdentity.LegacyMonitorNumber(null));
        Assert.AreEqual(0, MonitorIdentity.LegacyMonitorNumber(string.Empty));
        Assert.AreEqual(0, MonitorIdentity.LegacyMonitorNumber(@"\\?\DISPLAY#DELD1A8#5&abc&0&UID12345"));
        Assert.AreEqual(0, MonitorIdentity.LegacyMonitorNumber("DDC_DELD1A8_abc"));
        Assert.AreEqual(0, MonitorIdentity.LegacyMonitorNumber("DDC_DELD1A8_"));
    }
}
