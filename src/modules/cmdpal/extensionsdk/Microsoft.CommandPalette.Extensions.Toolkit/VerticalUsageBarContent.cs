// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Describes a vertical resource meter with fixed configuration and coherent value snapshots.
/// </summary>
public partial class VerticalUsageBarContent : BaseObservable, IVerticalUsageBarContent
{
    private readonly GraphSeriesInfo[] _series;
    private Snapshot _snapshot;

    public string DisplayName { get; init; } = string.Empty;

    public double Minimum { get; }

    public double Maximum { get; }

    public OptionalColor IndicatorColor { get; init; }

    public VerticalUsageBarContent()
        : this([])
    {
    }

    public VerticalUsageBarContent(GraphSeriesInfo[] series, double minimum = 0, double maximum = 100)
    {
        GraphContentValidation.ValidateRange(minimum, maximum);
        _series = GraphContentValidation.CopySeries(series);
        Minimum = minimum;
        Maximum = maximum;
        _snapshot = new Snapshot(minimum, string.Empty, new double[_series.Length]);
    }

    public GraphSeriesInfo[] GetSeries() => [.. _series];

    /// <summary>
    /// Returns the headline value, footer, and all contributions from one publication.
    /// An empty contribution array represents a simple meter.
    /// </summary>
    public double[] GetSnapshot(out double value, out string valueText)
    {
        var snapshot = Volatile.Read(ref _snapshot);
        value = snapshot.Value;
        valueText = snapshot.ValueText;
        return [.. snapshot.Values];
    }

    /// <summary>
    /// Publishes the value and footer of a simple meter with no configured series.
    /// </summary>
    public void SetSnapshot(double value, string valueText) => SetSnapshot(value, valueText, []);

    /// <summary>
    /// Publishes the headline, footer, and stack together, then raises
    /// <c>PropChanged("Data")</c>. Contributions are non-negative range units;
    /// their sum is independent of the headline value.
    /// </summary>
    public void SetSnapshot(double value, string valueText, double[] contributions)
    {
        ArgumentNullException.ThrowIfNull(valueText);
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The headline value must be finite.");
        }

        var values = GraphContentValidation.CopyValues(contributions, _series.Length);
        Volatile.Write(ref _snapshot, new Snapshot(value, valueText, values));
        OnPropertyChanged("Data");
    }

    private sealed record Snapshot(double Value, string ValueText, double[] Values);
}
