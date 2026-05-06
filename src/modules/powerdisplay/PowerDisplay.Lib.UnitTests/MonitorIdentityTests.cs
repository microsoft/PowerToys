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
    public void TryGetEdidId_ExtractsBetweenFirstAndSecondHash()
    {
        var input = @"\\?\DISPLAY#DELD1A8#5&abc123&0&UID12345";

        bool ok = MonitorIdentity.TryGetEdidId(input, out var edidId);

        Assert.IsTrue(ok);
        Assert.AreEqual("DELD1A8", edidId);
    }

    [TestMethod]
    public void TryGetEdidId_LegacyFormatId_ReturnsFalse()
    {
        bool ok = MonitorIdentity.TryGetEdidId("DDC_DELD1A8_1", out var edidId);

        Assert.IsFalse(ok);
        Assert.AreEqual(string.Empty, edidId);
    }
}
