// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Cli.Errors;
using PowerDisplay.Cli.Resolution;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Cli.UnitTests;

[TestClass]
public class MonitorResolverTests
{
    private static List<Monitor> SampleMonitors() => new()
    {
        new Monitor { MonitorNumber = 1, Id = "\\\\?\\DISPLAY#DEL0001#abc", Name = "Dell" },
        new Monitor { MonitorNumber = 2, Id = "\\\\?\\DISPLAY#BOE0002#def", Name = "Internal" },
    };

    [TestMethod]
    public void Resolve_NeitherSelector_ReturnsSelectorMissing()
    {
        var result = MonitorResolver.Resolve(SampleMonitors(), monitorNumber: null, monitorId: null);
        Assert.IsNull(result.Monitor);
        Assert.IsNotNull(result.Error);
        Assert.AreEqual(CliErrorCodes.SelectorMissing, result.Error.Code);
        Assert.AreEqual(CliExitCodes.SelectorMissing, result.Error.ExitCode);
    }

    [TestMethod]
    public void Resolve_NumberOnly_MatchesByNumber()
    {
        var result = MonitorResolver.Resolve(SampleMonitors(), monitorNumber: 2, monitorId: null);
        Assert.IsNotNull(result.Monitor);
        Assert.AreEqual(2, result.Monitor!.MonitorNumber);
        Assert.IsNull(result.Warning);
    }

    [TestMethod]
    public void Resolve_IdOnly_MatchesById()
    {
        var result = MonitorResolver.Resolve(SampleMonitors(), monitorNumber: null, monitorId: "\\\\?\\DISPLAY#DEL0001#abc");
        Assert.IsNotNull(result.Monitor);
        Assert.AreEqual("Dell", result.Monitor!.Name);
        Assert.IsNull(result.Warning);
    }

    [TestMethod]
    public void Resolve_IdMatchIsCaseInsensitive()
    {
        var result = MonitorResolver.Resolve(SampleMonitors(), monitorNumber: null, monitorId: "\\\\?\\display#del0001#ABC");
        Assert.IsNotNull(result.Monitor);
        Assert.AreEqual("Dell", result.Monitor!.Name);
    }

    [TestMethod]
    public void Resolve_BothSelectors_IdWinsAndWarnsAboutNumber()
    {
        var result = MonitorResolver.Resolve(
            SampleMonitors(),
            monitorNumber: 1, // would match Dell
            monitorId: "\\\\?\\DISPLAY#BOE0002#def"); // matches Internal

        Assert.IsNotNull(result.Monitor);
        Assert.AreEqual("Internal", result.Monitor!.Name);
        Assert.IsNotNull(result.Warning);
        StringAssert.Contains(result.Warning!, "--monitor-number 1 ignored");
    }

    [TestMethod]
    public void Resolve_UnknownNumber_ReturnsMonitorNotFound()
    {
        var result = MonitorResolver.Resolve(SampleMonitors(), monitorNumber: 99, monitorId: null);
        Assert.IsNotNull(result.Error);
        Assert.AreEqual(CliErrorCodes.MonitorNotFound, result.Error!.Code);
        StringAssert.Contains(result.Error.Message, "99");
    }

    [TestMethod]
    public void Resolve_UnknownId_ReturnsMonitorNotFound()
    {
        var result = MonitorResolver.Resolve(SampleMonitors(), monitorNumber: null, monitorId: "\\\\?\\DISPLAY#UNKNOWN");
        Assert.IsNotNull(result.Error);
        Assert.AreEqual(CliErrorCodes.MonitorNotFound, result.Error!.Code);
        StringAssert.Contains(result.Error.Message, "UNKNOWN");
    }
}
