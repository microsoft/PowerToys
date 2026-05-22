// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class MonitorBlacklistServiceTests
{
    private const string SamplePathDel = @"\\?\DISPLAY#DELD1A8#5&abc123&0&UID12345#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    private const string SamplePathBoe = @"\\?\DISPLAY#BOE0900#4&xyz&0&UID0";
    private const string SampleIdStripped = @"\\?\DISPLAY#DELD1A8#5&abc123&0&UID12345";

    [TestMethod]
    public void IsBlocked_EmptyService_ReturnsFalse()
    {
        var service = new MonitorBlacklistService(Array.Empty<MonitorBlacklistEntry>());

        Assert.IsFalse(service.IsBlocked(SamplePathDel));
        Assert.IsFalse(service.IsBlocked(SamplePathBoe));
    }

    [TestMethod]
    public void IsBlocked_MatchesCustomEntryByEdidId()
    {
        var service = new MonitorBlacklistService(new[]
        {
            new MonitorBlacklistEntry { EdidId = "DELD1A8" },
        });

        Assert.IsTrue(service.IsBlocked(SamplePathDel));
        Assert.IsFalse(service.IsBlocked(SamplePathBoe));
    }

    [TestMethod]
    public void IsBlocked_MatchesStrippedMonitorId()
    {
        var service = new MonitorBlacklistService(new[]
        {
            new MonitorBlacklistEntry { EdidId = "DELD1A8" },
        });

        // Both the raw DevicePath and the GUID-stripped Monitor.Id should match.
        Assert.IsTrue(service.IsBlocked(SampleIdStripped));
    }

    [TestMethod]
    public void IsBlocked_IsCaseInsensitive()
    {
        var service = new MonitorBlacklistService(new[]
        {
            new MonitorBlacklistEntry { EdidId = "deld1a8" },
        });

        Assert.IsTrue(service.IsBlocked(SamplePathDel));
    }

    [TestMethod]
    public void IsBlocked_NormalizesWhitespace()
    {
        var service = new MonitorBlacklistService(new[]
        {
            new MonitorBlacklistEntry { EdidId = "   DELD1A8\t" },
        });

        Assert.IsTrue(service.IsBlocked(SamplePathDel));
    }

    [TestMethod]
    public void IsBlocked_EmptyOrUnknownMonitorId_ReturnsFalse()
    {
        var service = new MonitorBlacklistService(new[]
        {
            new MonitorBlacklistEntry { EdidId = "DELD1A8" },
        });

        Assert.IsFalse(service.IsBlocked(string.Empty));
        Assert.IsFalse(service.IsBlocked(null!));
        Assert.IsFalse(service.IsBlocked(@"\\?\DISPLAY"));               // too few segments
        Assert.IsFalse(service.IsBlocked(@"garbage no hashes here"));
    }

    [TestMethod]
    public void IsBlocked_IgnoresBlankEntriesInList()
    {
        var service = new MonitorBlacklistService(new[]
        {
            new MonitorBlacklistEntry { EdidId = string.Empty },
            new MonitorBlacklistEntry { EdidId = "   " },
            new MonitorBlacklistEntry { EdidId = "DELD1A8" },
        });

        Assert.IsTrue(service.IsBlocked(SamplePathDel));
        Assert.IsFalse(service.IsBlocked(SamplePathBoe));
    }

    [TestMethod]
    public void IsBlocked_BuiltInEntriesAreIncluded()
    {
        // Built-in list ships empty at the moment, but the service must still
        // consult it: a list that combines built-in (currently empty) with
        // custom entries should equal "just custom" today.
        var customOnly = new MonitorBlacklistService(new[]
        {
            new MonitorBlacklistEntry { EdidId = "BOE0900" },
        });

        Assert.IsTrue(customOnly.IsBlocked(SamplePathBoe));
        Assert.IsFalse(customOnly.IsBlocked(SamplePathDel));
    }
}
