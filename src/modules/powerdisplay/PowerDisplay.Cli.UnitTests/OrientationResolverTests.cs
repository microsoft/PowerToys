// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Cli.Errors;
using PowerDisplay.Cli.Resolution;

namespace PowerDisplay.Cli.UnitTests;

[TestClass]
public class OrientationResolverTests
{
    [TestMethod]
    [DataRow("0", 0)]
    [DataRow("90", 1)]
    [DataRow("180", 2)]
    [DataRow("270", 3)]
    public void TryResolve_ValidDegrees_MapsToIndex(string degrees, int expectedIndex)
    {
        var resolved = OrientationResolver.TryResolve(degrees, out var error);
        Assert.IsNotNull(resolved);
        Assert.AreEqual(expectedIndex, resolved);
        Assert.IsNull(error);
    }

    [TestMethod]
    [DataRow("45")]
    [DataRow("360")]
    [DataRow("-90")]
    [DataRow("abc")]
    [DataRow("")]
    public void TryResolve_InvalidValue_ReturnsInvalidDiscreteError(string raw)
    {
        var resolved = OrientationResolver.TryResolve(raw, out var error);
        Assert.IsNull(resolved);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.InvalidDiscreteValue, error.Code);
        Assert.AreEqual(CliExitCodes.InvalidDiscreteValue, error.ExitCode);
        Assert.AreEqual("orientation", error.Setting);
        Assert.AreEqual(raw, error.Requested);
        StringAssert.Contains(error.Message, "0, 90, 180, 270");
    }
}
