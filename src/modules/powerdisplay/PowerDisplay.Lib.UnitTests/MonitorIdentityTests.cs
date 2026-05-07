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
}
