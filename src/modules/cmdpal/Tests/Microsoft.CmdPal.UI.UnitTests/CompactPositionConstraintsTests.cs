// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public sealed class CompactPositionConstraintsTests
{
    [DataRow(0.0, 840.0, 50.0)]
    [DataRow(75.0, 1080.0, 75.0)]
    [DataRow(0.0, 360.0, 100.0)]
    [DataRow(120.0, 1080.0, 100.0)]
    [DataTestMethod]
    public void ClampPercentageFromBottom_ReservesExpansionSpace(
        double percentage,
        double workAreaHeightDip,
        double expected)
    {
        var actual = CompactPositionConstraints.ClampPercentageFromBottom(
            percentage,
            workAreaHeightDip);

        Assert.AreEqual(expected, actual, 0.001);
    }
}
