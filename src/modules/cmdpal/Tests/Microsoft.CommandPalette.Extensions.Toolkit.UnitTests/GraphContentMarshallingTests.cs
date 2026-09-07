// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WinRT;
using GraphAbi = ABI.Microsoft.CommandPalette.Extensions;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class GraphContentMarshallingTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LineGraph_RoundTripsArraysThroughTheAbi(bool empty)
    {
        var series = Series(empty);
        GraphValueScale[] scales = empty ? [] : [new() { Divisor = 1, Suffix = " bps" }, new() { Divisor = 1024, Suffix = " KiB/s" }];
        var timestamp = new DateTimeOffset(2026, 9, 7, 14, 0, 0, TimeSpan.FromHours(2)).AddTicks(7);
        GraphSample[] samples = empty ? [] :
        [
            GraphSampleHelpers.Create(0, DateTimeOffset.MinValue, 1),
            GraphSampleHelpers.Create(0, timestamp, 12.5),
            GraphSampleHelpers.Create(1, timestamp, 0),
            GraphSampleHelpers.Create(2, timestamp, 42.25),
            GraphSampleHelpers.Create(0, timestamp.AddSeconds(1), 110.75),
            GraphSampleHelpers.Create(0, DateTimeOffset.MaxValue, 2),
        ];
        var graph = new LineGraphContent(series, valueScales: scales);
        graph.SetSnapshot(samples);
        using var reference = MarshalInterface<ILineGraphContent>.CreateMarshaler(graph);

        // Invoke generated ABI methods directly; a managed cast can return the original
        // object and bypass the array allocation, conversion, and cleanup.
        for (var iteration = 0; iteration < 3; iteration++)
        {
            EqualSeries(series, GraphAbi.ILineGraphContentMethods.GetSeries(reference));
            EqualScales(scales, GraphAbi.ILineGraphContentMethods.GetValueScales(reference));
            Equal(samples, GraphAbi.ILineGraphContentMethods.GetSnapshot(reference));
        }

        graph.SetSnapshot([]);
        Equal<GraphSample>([], GraphAbi.ILineGraphContentMethods.GetSnapshot(reference));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void VerticalBar_RoundTripsArraysThroughTheAbi(bool empty)
    {
        var series = Series(empty);
        double[] values = empty ? [] : [12.5, 0, 42.25];
        var graph = new VerticalUsageBarContent(series);
        graph.SetSnapshot(31.25, "31.25%", values);
        using var reference = MarshalInterface<IVerticalUsageBarContent>.CreateMarshaler(graph);
        EqualSeries(series, GraphAbi.IVerticalUsageBarContentMethods.GetSeries(reference));
        Equal(values, GraphAbi.IVerticalUsageBarContentMethods.GetSnapshot(reference, out var value, out var text));
        if (value != 31.25 || text != "31.25%")
        {
            throw new InvalidOperationException("The vertical graph lost its coherent headline.");
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Doughnut_RoundTripsArraysThroughTheAbi(bool empty)
    {
        var series = Series(empty);
        double[] values = empty ? [] : [12.5, 0, 42.25];
        var graph = new DoughnutGraphContent(series);
        graph.SetSnapshot(values, "54.75", "Total");
        using var reference = MarshalInterface<IDoughnutGraphContent>.CreateMarshaler(graph);
        EqualSeries(series, GraphAbi.IDoughnutGraphContentMethods.GetSeries(reference));
        Equal(values, GraphAbi.IDoughnutGraphContentMethods.GetSnapshot(reference, out var value, out var label));
        if (value != "54.75" || label != "Total")
        {
            throw new InvalidOperationException("The doughnut graph lost its coherent center text.");
        }
    }

    private static GraphSeriesInfo[] Series(bool empty) => empty ? [] :
    [
        new()
        {
            Name = "Send",
            Color = new OptionalColor { HasValue = true, Color = new Color { A = 127, R = 185, G = 120, B = 198 } },
            LineStyle = GraphLineStyle.Dotted,
        },
        new() { Name = "Receive \u03bb", LineStyle = GraphLineStyle.Dashed },
        new() { Name = "Readout", LineStyle = GraphLineStyle.Solid, IsReadoutOnly = true, ReadoutValueSuffix = " GiB" },
    ];

    private static void EqualSeries(GraphSeriesInfo[] expected, IGraphSeriesInfo[]? actual)
    {
        actual ??= [];
        if (expected.Length != actual.Length)
        {
            throw new InvalidOperationException("The series array changed across the graph ABI.");
        }

        for (var index = 0; index < expected.Length; index++)
        {
            // Enter each metadata getter through its ABI too, even when an in-process
            // interface-array read unwraps the original managed object.
            using var reference = MarshalInterface<IGraphSeriesInfo>.CreateMarshaler(actual[index]);
            var series = expected[index];
            if (GraphAbi.IGraphSeriesInfoMethods.get_Name(reference) != series.Name ||
                GraphAbi.IGraphSeriesInfoMethods.get_Color(reference) != series.Color ||
                GraphAbi.IGraphSeriesInfoMethods.get_LineStyle(reference) != series.LineStyle ||
                GraphAbi.IGraphSeriesInfoMethods.get_IsReadoutOnly(reference) != series.IsReadoutOnly ||
                GraphAbi.IGraphSeriesInfoMethods.get_ReadoutValueSuffix(reference) != series.ReadoutValueSuffix)
            {
                throw new InvalidOperationException("Series metadata changed across the graph ABI.");
            }
        }
    }

    private static void EqualScales(GraphValueScale[] expected, IGraphValueScale[]? actual)
    {
        actual ??= [];
        if (expected.Length != actual.Length)
        {
            throw new InvalidOperationException("The value-scale array changed across the graph ABI.");
        }

        for (var index = 0; index < expected.Length; index++)
        {
            using var reference = MarshalInterface<IGraphValueScale>.CreateMarshaler(actual[index]);
            if (GraphAbi.IGraphValueScaleMethods.get_Divisor(reference) != expected[index].Divisor ||
                GraphAbi.IGraphValueScaleMethods.get_Suffix(reference) != expected[index].Suffix)
            {
                throw new InvalidOperationException("Value-scale metadata changed across the graph ABI.");
            }
        }
    }

    private static void Equal<T>(T[] expected, T[]? actual)
        where T : IEquatable<T>
    {
        if (!expected.AsSpan().SequenceEqual(actual))
        {
            throw new InvalidOperationException($"The {typeof(T).Name} array changed across the graph ABI.");
        }
    }
}
