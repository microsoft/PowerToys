// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class GraphSampleHelpersTests
{
    [TestMethod]
    public void TimestampTicks_UseTheWindowsEpochAndFullDateTimeRange()
    {
        Assert.AreEqual(0L, GraphSampleHelpers.ToTimestampTicks(new DateTimeOffset(1601, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.AreEqual(116444736000000000L, GraphSampleHelpers.ToTimestampTicks(DateTimeOffset.UnixEpoch));
        Assert.AreEqual(GraphSampleHelpers.MinTimestampTicks, GraphSampleHelpers.ToTimestampTicks(DateTimeOffset.MinValue));
        Assert.AreEqual(GraphSampleHelpers.MaxTimestampTicks, GraphSampleHelpers.ToTimestampTicks(DateTimeOffset.MaxValue));
        Assert.AreEqual(DateTimeOffset.MinValue, GraphSampleHelpers.FromTimestampTicks(GraphSampleHelpers.MinTimestampTicks));
        Assert.AreEqual(DateTimeOffset.MaxValue, GraphSampleHelpers.FromTimestampTicks(GraphSampleHelpers.MaxTimestampTicks));
        Assert.AreEqual(new DateTimeOffset(1601, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(-1), GraphSampleHelpers.FromTimestampTicks(-1));
    }

    [TestMethod]
    [DataRow(-14)]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(14)]
    public void Create_PreservesSubMicrosecondPrecisionAndNormalizesOffsets(int offsetHours)
    {
        var time = new DateTimeOffset(2026, 9, 7, 14, 0, 0, TimeSpan.FromHours(offsetHours)).AddTicks(7);
        var sample = GraphSampleHelpers.Create(3, time, 12.5);

        Assert.AreEqual(3u, sample.SeriesIndex);
        Assert.AreEqual(12.5, sample.Value);
        Assert.AreEqual(time.UtcTicks, sample.GetTimestamp().UtcTicks);
        Assert.AreEqual(TimeSpan.Zero, sample.GetTimestamp().Offset);
        Assert.AreEqual(7L, sample.TimestampTicks % 10);
    }

    [TestMethod]
    [DataRow(long.MinValue)]
    [DataRow(GraphSampleHelpers.MinTimestampTicks - 1)]
    [DataRow(GraphSampleHelpers.MaxTimestampTicks + 1)]
    [DataRow(long.MaxValue)]
    public void FromTimestampTicks_RejectsValuesOutsideTheSupportedRange(long ticks)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => GraphSampleHelpers.FromTimestampTicks(ticks));
    }

    [TestMethod]
    public void Sample_UsesTheBlittableTransportLayout()
    {
        Assert.IsFalse(RuntimeHelpers.IsReferenceOrContainsReferences<GraphSample>());
        Assert.AreEqual(24, Unsafe.SizeOf<GraphSample>());
        Assert.AreEqual(24, Marshal.SizeOf<GraphSample>());
        Assert.AreEqual((nint)0, Marshal.OffsetOf<GraphSample>(nameof(GraphSample.SeriesIndex)));
        Assert.AreEqual((nint)8, Marshal.OffsetOf<GraphSample>(nameof(GraphSample.TimestampTicks)));
        Assert.AreEqual((nint)16, Marshal.OffsetOf<GraphSample>(nameof(GraphSample.Value)));
    }
}
