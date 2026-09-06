// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Describes a doughnut graph with fixed slice descriptions and bulk value snapshots.
/// </summary>
public partial class DoughnutGraphContent : BaseObservable, IDoughnutGraphContent
{
    private readonly GraphSeriesInfo[] _series;
    private Snapshot _snapshot;

    public string DisplayName { get; init; } = string.Empty;

    public DoughnutGraphContent(GraphSeriesInfo[] series)
    {
        _series = GraphContentValidation.CopySeries(series);
        _snapshot = new Snapshot(new double[_series.Length], string.Empty, string.Empty);
    }

    public GraphSeriesInfo[] GetSeries() => [.. _series];

    /// <summary>
    /// Returns all slice values and both center strings from one publication.
    /// The host normalizes the non-negative values; an all-zero snapshot clears the ring.
    /// </summary>
    public double[] GetSnapshot(out string centerValue, out string centerLabel)
    {
        var snapshot = Volatile.Read(ref _snapshot);
        centerValue = snapshot.CenterValue;
        centerLabel = snapshot.CenterLabel;
        return [.. snapshot.Values];
    }

    /// <summary>
    /// Publishes all slice values and center text together, then raises
    /// <c>PropChanged("Data")</c>. Values use a common unit and are not pre-normalized.
    /// </summary>
    public void SetSnapshot(double[] values, string centerValue = "", string centerLabel = "")
    {
        ArgumentNullException.ThrowIfNull(centerValue);
        ArgumentNullException.ThrowIfNull(centerLabel);
        var copy = GraphContentValidation.CopyValues(values, _series.Length);
        Volatile.Write(ref _snapshot, new Snapshot(copy, centerValue, centerLabel));
        OnPropertyChanged("Data");
    }

    private sealed record Snapshot(double[] Values, string CenterValue, string CenterLabel);
}
