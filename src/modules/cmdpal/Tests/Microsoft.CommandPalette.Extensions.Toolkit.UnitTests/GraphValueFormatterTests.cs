// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class GraphValueFormatterTests
{
    private static readonly GraphValueScale[] CountScales =
    [
        new() { Divisor = 1, Suffix = " items" },
        new() { Divisor = 1000, Suffix = " k items" },
        new() { Divisor = 1_000_000, Suffix = " M items" },
    ];

    [TestMethod]
    [DataRow(0d, "0.0 items")]
    [DataRow(999d, "999.0 items")]
    [DataRow(1000d, "1.0 k items")]
    [DataRow(1_500_000d, "1.5 M items")]
    [DataRow(-1536d, "-1.5 k items")]
    [DataRow(1_500_000_000d, "1500.0 M items")]
    public void CallerDefinedScales_SelectByMagnitudeAndRetainTheSign(double value, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.AreEqual(expected, GraphValueFormatter.Format(value, valueSuffix: "ignored", valueScales: CountScales));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [TestMethod]
    [DataRow(0d, "0.0 ms")]
    [DataRow(0.0005d, "0.5 ms")]
    [DataRow(0.5d, "500.0 ms")]
    [DataRow(1d, "1.0 s")]
    [DataRow(59d, "59.0 s")]
    [DataRow(60d, "1.0 min")]
    [DataRow(90d, "1.5 min")]
    public void Scales_CanUseSubunitAndIrregularDivisors(double value, string expected)
    {
        GraphValueScale[] scales =
        [
            new() { Divisor = 0.001, Suffix = " ms" },
            new() { Divisor = 1, Suffix = " s" },
            new() { Divisor = 60, Suffix = " min" },
        ];
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.AreEqual(expected, GraphValueFormatter.Format(value, valueScales: scales));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [TestMethod]
    public void Formatting_UsesTheCurrentCultureAndPreservesNumericSuffixes()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("cs-CZ");
            Assert.AreEqual("1,50 M items", GraphValueFormatter.Format(1_500_000, "0.00", valueScales: CountScales));
            Assert.AreEqual("1,5 M items", GraphValueFormatter.Format(1_500_000, "Q", valueScales: CountScales));
            Assert.AreEqual("42,3%", GraphValueFormatter.Format(42.25, valueSuffix: "%"));
            Assert.AreEqual("42,3%", GraphValueFormatter.Format(42.25, valueFormat: "Q", valueSuffix: "%"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [TestMethod]
    public void AutoScaleConfiguration_CopiesScalesAndPreservesRawValuesAboveTheFloor()
    {
        ILineGraphContent fixedGraph = new LineGraphContent([]);
        Assert.IsFalse(fixedGraph.AutoScaleMaximum);
        Assert.IsEmpty(fixedGraph.GetValueScales());

        GraphValueScale[] scales = [.. CountScales];
        var graph = new LineGraphContent([new GraphSeriesInfo { Name = "Count" }], maximum: 1000, valueScales: scales)
        {
            AutoScaleMaximum = true,
        };
        graph.SetSnapshot([GraphSampleHelpers.Create(0, DateTimeOffset.UnixEpoch, 50_000)]);
        scales[0] = new GraphValueScale { Divisor = 99, Suffix = "changed" };
        graph.GetValueScales()[1] = scales[0];

        ILineGraphContent content = graph;
        Assert.IsTrue(content.AutoScaleMaximum);
        CollectionAssert.AreEqual(CountScales, content.GetValueScales());
        Assert.AreEqual(1000d, content.Maximum);
        Assert.AreEqual(50_000d, content.GetSnapshot()[0].Value);
    }

    [TestMethod]
    [DataRow(0d)]
    [DataRow(-1d)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void InvalidDivisors_AreRejected(double divisor)
    {
        Assert.ThrowsExactly<ArgumentException>(() => _ = new LineGraphContent([], valueScales: [new GraphValueScale { Divisor = divisor, Suffix = " items" }]));
    }

    [TestMethod]
    public void UnorderedScalesAndNullSuffixes_AreRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => _ = new LineGraphContent([], valueScales: [CountScales[0], CountScales[0]]));
        Assert.ThrowsExactly<ArgumentException>(() => _ = new LineGraphContent([], valueScales: [CountScales[1], CountScales[0]]));
        Assert.ThrowsExactly<ArgumentException>(() => _ = new LineGraphContent([], valueScales: [new GraphValueScale { Divisor = 1, Suffix = null! }]));
    }
}
