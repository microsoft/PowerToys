// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Converts graph timestamps between UTC time and signed 100-nanosecond ticks
/// since 1601-01-01T00:00:00Z, preserving the precision of DateTimeOffset.
/// </summary>
public static class GraphSampleHelpers
{
    /// <summary>
    /// The earliest supported timestamp, 0001-01-01T00:00:00Z.
    /// </summary>
    public const long MinTimestampTicks = -504911232000000000;

    /// <summary>
    /// The latest supported timestamp, 9999-12-31T23:59:59.9999999Z.
    /// </summary>
    public const long MaxTimestampTicks = 2650467743999999999;

    /// <summary>
    /// Creates an observation with the supplied time normalized to UTC.
    /// </summary>
    public static GraphSample Create(uint seriesIndex, DateTimeOffset timestamp, double value) => new()
    {
        SeriesIndex = seriesIndex,
        TimestampTicks = ToTimestampTicks(timestamp),
        Value = value,
    };

    /// <summary>
    /// Converts an observation time to ticks since the Windows epoch.
    /// </summary>
    public static long ToTimestampTicks(DateTimeOffset timestamp) => timestamp.UtcTicks + MinTimestampTicks;

    /// <summary>
    /// Converts graph timestamp ticks to a UTC time. Values outside years 0001
    /// through 9999 are rejected.
    /// </summary>
    public static DateTimeOffset FromTimestampTicks(long timestampTicks)
    {
        if (timestampTicks < MinTimestampTicks || timestampTicks > MaxTimestampTicks)
        {
            throw new ArgumentOutOfRangeException(nameof(timestampTicks), "Graph timestamps must represent a UTC time in years 0001 through 9999.");
        }

        return new DateTimeOffset(timestampTicks - MinTimestampTicks, TimeSpan.Zero);
    }

    /// <summary>
    /// Gets the observation time in UTC.
    /// </summary>
    public static DateTimeOffset GetTimestamp(this GraphSample sample) => FromTimestampTicks(sample.TimestampTicks);
}
