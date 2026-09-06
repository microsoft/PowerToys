// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Describes a live line graph with fixed configuration and bulk history snapshots.
/// </summary>
public partial class LineGraphContent : BaseObservable, ILineGraphContent
{
    private readonly GraphSeriesInfo[] _series;
    private readonly TimeSpan _historyDuration = TimeSpan.FromSeconds(60);
    private readonly double _smoothing;
    private GraphSample[] _samples = [];

    public string DisplayName { get; init; } = string.Empty;

    public double Minimum { get; }

    public double Maximum { get; }

    /// <summary>
    /// Gets the curve smoothing amount: zero draws straight segments; one draws
    /// a monotone cubic curve. Intermediate values blend the two shapes.
    /// </summary>
    public double Smoothing
    {
        get => _smoothing;
        init
        {
            if (!double.IsFinite(value) || value < 0 || value > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Smoothing must be finite and between zero and one.");
            }

            _smoothing = value;
        }
    }

    public TimeSpan HistoryDuration
    {
        get => _historyDuration;
        init
        {
            if (value < TimeSpan.FromSeconds(1))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "History duration must be at least one second.");
            }

            _historyDuration = value;
        }
    }

    public string ValueFormat { get; init; } = "0.0";

    public string ValueSuffix { get; init; } = string.Empty;

    public LineGraphContent(GraphSeriesInfo[] series, double minimum = 0, double maximum = 100)
    {
        GraphContentValidation.ValidateRange(minimum, maximum);
        _series = GraphContentValidation.CopySeries(series);
        Minimum = minimum;
        Maximum = maximum;
    }

    public GraphSeriesInfo[] GetSeries() => [.. _series];

    public GraphSample[] GetSnapshot() => [.. Volatile.Read(ref _samples)];

    /// <summary>
    /// Publishes a complete retained history and raises <c>PropChanged("Data")</c>.
    /// Samples may be interleaved; timestamps must increase within each series.
    /// The caller owns sampling and bounded history retention.
    /// </summary>
    public void SetSnapshot(GraphSample[] samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        GraphSample[] snapshot = [.. samples];
        var lastTimestamps = new DateTimeOffset?[_series.Length];

        foreach (var sample in snapshot)
        {
            if (sample.SeriesIndex >= (uint)_series.Length)
            {
                throw new ArgumentException("Sample series indices must refer to a configured series.", nameof(samples));
            }

            if (!double.IsFinite(sample.Value))
            {
                throw new ArgumentException("Graph values must be finite.", nameof(samples));
            }

            var index = (int)sample.SeriesIndex;
            if (lastTimestamps[index] is { } previous && sample.Timestamp <= previous)
            {
                throw new ArgumentException("Timestamps must increase within each graph series.", nameof(samples));
            }

            lastTimestamps[index] = sample.Timestamp;
        }

        Volatile.Write(ref _samples, snapshot);
        OnPropertyChanged("Data");
    }
}
