// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class ResourceBarContentTests
{
    private static readonly double[] EmptyValues = [0, 0];
    private static readonly double[] PublishedValues = [3072, 1024];

    [TestMethod]
    public void ConfigurationAndSnapshots_AreIndependentAndPublishBeforeNotification()
    {
        GraphSeriesInfo[] series = [new() { Name = "Used" }, new() { Name = "Free" }];
        GraphValueScale[] scales = [new() { Divisor = 1024, Suffix = " KB" }];
        var graph = new ResourceBarContent(series, scales) { DisplayName = "Memory", ValueFormat = "0.00" };
        series[0] = new GraphSeriesInfo { Name = "Changed" };
        scales[0] = new GraphValueScale { Divisor = 1, Suffix = " changed" };
        graph.GetSeries()[0] = series[0];
        graph.GetValueScales()[0] = scales[0];

        Assert.AreEqual("Used", graph.GetSeries()[0].Name);
        Assert.AreEqual(1024d, graph.GetValueScales()[0].Divisor);
        Assert.AreEqual("Memory", graph.DisplayName);
        Assert.AreEqual("0.00", graph.ValueFormat);
        CollectionAssert.AreEqual(EmptyValues, graph.GetSnapshot());

        double[] values = [3072, 1024];
        var notifications = 0;
        graph.PropChanged += (_, args) =>
        {
            Assert.AreEqual("Data", args.PropertyName);
            CollectionAssert.AreEqual(values, graph.GetSnapshot());
            notifications++;
        };
        graph.SetSnapshot(values);
        values[0] = 0;
        graph.GetSnapshot()[1] = 0;

        CollectionAssert.AreEqual(PublishedValues, graph.GetSnapshot());
        Assert.AreEqual(1, notifications);
    }

    [TestMethod]
    [DataRow(-1d)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void InvalidValues_DoNotPublish(double invalid)
    {
        var graph = new ResourceBarContent([new GraphSeriesInfo { Name = "Used" }]);
        graph.SetSnapshot([42]);
        var notifications = 0;
        graph.PropChanged += (_, _) => notifications++;

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => graph.SetSnapshot([invalid]));
        Assert.AreEqual(42d, graph.GetSnapshot()[0]);
        Assert.AreEqual(0, notifications);
    }

    [TestMethod]
    public void Snapshots_RequireOneValuePerSeriesAndPreserveLargeValues()
    {
        var graph = new ResourceBarContent([new GraphSeriesInfo { Name = "Used" }, new GraphSeriesInfo { Name = "Free" }]);
        graph.SetSnapshot([double.MaxValue, double.MaxValue]);

        Assert.ThrowsExactly<ArgumentException>(() => graph.SetSnapshot([]));
        Assert.ThrowsExactly<ArgumentException>(() => graph.SetSnapshot([1]));
        Assert.ThrowsExactly<ArgumentNullException>(() => graph.SetSnapshot(null!));
        CollectionAssert.AreEqual(new[] { double.MaxValue, double.MaxValue }, graph.GetSnapshot());

        graph.SetSnapshot([0, 0]);
        CollectionAssert.AreEqual(EmptyValues, graph.GetSnapshot());
        var empty = new ResourceBarContent([]);
        empty.SetSnapshot([]);
        Assert.IsEmpty(empty.GetSnapshot());
    }
}
