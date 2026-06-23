// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public class MonitorBlacklistServiceTests
{
    private const string SamplePathDel = @"\\?\DISPLAY#DELD1A8#5&abc123&0&UID12345#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    private const string SamplePathBoe = @"\\?\DISPLAY#BOE0900#4&xyz&0&UID0";

    [TestMethod]
    public void IsBlocked_EmptyBuiltIn_ReturnsFalse()
    {
        // Built-in list ships empty in this release, so the service should never block.
        var service = new MonitorBlacklistService();

        Assert.IsFalse(service.IsBlocked(SamplePathDel));
        Assert.IsFalse(service.IsBlocked(SamplePathBoe));
    }

    [TestMethod]
    public void IsBlocked_EmptyOrUnknownMonitorId_ReturnsFalse()
    {
        var service = new MonitorBlacklistService();

        Assert.IsFalse(service.IsBlocked(string.Empty));
        Assert.IsFalse(service.IsBlocked(null!));
        Assert.IsFalse(service.IsBlocked(@"\\?\DISPLAY"));
        Assert.IsFalse(service.IsBlocked(@"garbage no hashes here"));
    }
}
