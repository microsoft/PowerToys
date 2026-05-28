// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Cli.Errors;
using PowerDisplay.Cli.Resolution;

namespace PowerDisplay.Cli.UnitTests;

[TestClass]
public class DiscreteValueResolverTests
{
    private static readonly IReadOnlyList<int> SupportedInputSources = new[] { 0x0F, 0x11, 0x12, 0x1B };

    [TestMethod]
    public void TryResolve_FriendlyName_ReturnsVcpValue()
    {
        var resolved = DiscreteValueResolver.TryResolve(0x60, "input-source", "HDMI-1", SupportedInputSources, out var error);
        Assert.IsNull(error);
        Assert.AreEqual(0x11, resolved);
    }

    [TestMethod]
    public void TryResolve_FriendlyNameIsCaseInsensitive()
    {
        var resolved = DiscreteValueResolver.TryResolve(0x60, "input-source", "hdmi-1", SupportedInputSources, out var error);
        Assert.IsNull(error);
        Assert.AreEqual(0x11, resolved);
    }

    [TestMethod]
    public void TryResolve_HexValue_ReturnsParsedInt()
    {
        var resolved = DiscreteValueResolver.TryResolve(0x60, "input-source", "0x11", SupportedInputSources, out var error);
        Assert.IsNull(error);
        Assert.AreEqual(0x11, resolved);
    }

    [TestMethod]
    public void TryResolve_HexUpperCasePrefix_ReturnsParsedInt()
    {
        var resolved = DiscreteValueResolver.TryResolve(0x60, "input-source", "0X1B", SupportedInputSources, out var error);
        Assert.IsNull(error);
        Assert.AreEqual(0x1B, resolved);
    }

    [TestMethod]
    public void TryResolve_UnknownName_ReturnsInvalidWithSupportedList()
    {
        var resolved = DiscreteValueResolver.TryResolve(0x60, "input-source", "PIZZA", SupportedInputSources, out var error);
        Assert.IsNull(resolved);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.InvalidDiscreteValue, error!.Code);
        Assert.AreEqual(CliExitCodes.InvalidDiscreteValue, error.ExitCode);
        Assert.AreEqual("input-source", error.Setting);
        Assert.AreEqual("PIZZA", error.Requested);
        Assert.IsNotNull(error.Supported);
        Assert.AreEqual(4, error.Supported!.Count);
    }

    [TestMethod]
    public void TryResolve_HexNotInSupportedSet_ReturnsInvalidDiscreteError()
    {
        // 0xFF is a valid VCP byte but not in the monitor's supported set.
        var resolved = DiscreteValueResolver.TryResolve(0x60, "input-source", "0xFF", SupportedInputSources, out var error);
        Assert.IsNull(resolved);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.InvalidDiscreteValue, error!.Code);
        StringAssert.Contains(error.Message, "not in the monitor's supported set");
    }

    [TestMethod]
    public void TryResolve_NoSupportedSet_AcceptsAnyParseableValue()
    {
        // When the monitor doesn't advertise a supported set, any parseable hex or known
        // name is accepted; controller-level write surfaces hardware failure if rejected.
        var resolved = DiscreteValueResolver.TryResolve(0x60, "input-source", "0x33", supportedValues: null, out var error);
        Assert.IsNull(error);
        Assert.AreEqual(0x33, resolved);
    }

    [TestMethod]
    public void TryResolve_PowerStateOn_ResolvesToVcpOne()
    {
        var supported = new[] { 0x01, 0x02, 0x04 };
        var resolved = DiscreteValueResolver.TryResolve(0xD6, "power-state", "On", supported, out var error);
        Assert.IsNull(error);
        Assert.AreEqual(0x01, resolved);
    }

    [TestMethod]
    public void TryResolve_ColorTemperatureSrgb_ResolvesViaName()
    {
        var supported = new[] { 0x01, 0x05, 0x06 };
        var resolved = DiscreteValueResolver.TryResolve(0x14, "color-temperature", "sRGB", supported, out var error);
        Assert.IsNull(error);
        Assert.AreEqual(0x01, resolved);
    }
}
