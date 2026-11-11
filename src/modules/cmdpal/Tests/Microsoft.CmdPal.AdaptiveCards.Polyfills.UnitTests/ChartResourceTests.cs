// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills.UnitTests;

[OSCondition(OperatingSystems.Windows)]
[TestClass]
public class ChartResourceTests
{
    [TestMethod]
    [DataRow("AdaptiveChart_LineChart", "Line chart")]
    [DataRow("AdaptiveChart_BarChart", "Bar chart")]
    [DataRow("AdaptiveChart_SeriesName", "Series {0}")]
    [DataRow("AdaptiveChart_SeriesSummary", "{0}: latest {1}, low {2}, high {3}.")]
    [DataRow("AdaptiveChart_CompactThousands", "{0}K")]
    [DataRow("AdaptiveChart_CompactMillions", "{0}M")]
    [DataRow("AdaptiveChart_CompactBillions", "{0}B")]
    [DataRow("AdaptiveChart_CompactTrillions", "{0}T")]
    public void ChartStringsAreAvailableWithoutAHost(string name, string expected)
    {
        Assert.AreEqual(expected, Properties.Resources.ResourceManager.GetString(name, CultureInfo.InvariantCulture));
    }
}
