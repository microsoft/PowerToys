// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CoreWidgetProvider.Helpers;

/// <summary>
/// Retains timestamped usage observations at the sampling source, so opening a
/// page or switching devices never assigns new timestamps to existing readings.
/// </summary>
internal sealed class UsageHistory
{
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(60);

    private readonly List<GraphSample> _samples = [];
    private readonly int _seriesCount;
    private readonly TimeProvider _timeProvider;
    private readonly DateTimeOffset _origin;
    private readonly long _originTimestamp;

    public UsageHistory(int seriesCount = 1, TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seriesCount);
        _seriesCount = seriesCount;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _origin = _timeProvider.GetUtcNow();
        _originTimestamp = _timeProvider.GetTimestamp();
    }

    public void Add(params ReadOnlySpan<double> values)
    {
        if (values.Length != _seriesCount)
        {
            throw new ArgumentException("Provide one usage value for each series.", nameof(values));
        }

        foreach (var value in values)
        {
            if (!double.IsFinite(value))
            {
                return;
            }
        }

        lock (_samples)
        {
            // Use elapsed time so a wall-clock adjustment cannot reorder samples.
            var timestamp = GraphSampleHelpers.ToTimestampTicks(_origin + _timeProvider.GetElapsedTime(_originTimestamp));
            if (_samples.Count > 0 && timestamp <= _samples[^1].TimestampTicks)
            {
                return;
            }

            for (var index = 0; index < values.Length; index++)
            {
                _samples.Add(new GraphSample { SeriesIndex = (uint)index, TimestampTicks = timestamp, Value = values[index] });
            }

            // Retain two preceding observations for the graph's left boundary.
            var cutoff = timestamp - Duration.Ticks;
            var removeCount = 0;
            while (removeCount + (2 * _seriesCount) < _samples.Count && _samples[removeCount + (2 * _seriesCount)].TimestampTicks < cutoff)
            {
                removeCount += _seriesCount;
            }

            if (removeCount > 0)
            {
                _samples.RemoveRange(0, removeCount);
            }
        }
    }

    public GraphSample[] GetSnapshot()
    {
        lock (_samples)
        {
            return [.. _samples];
        }
    }
}
