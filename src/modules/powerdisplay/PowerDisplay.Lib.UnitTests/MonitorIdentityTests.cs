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
    public void PnpHardwareKeyFromInstanceName_StripsSuffixAndNormalisesSeparator()
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
}
