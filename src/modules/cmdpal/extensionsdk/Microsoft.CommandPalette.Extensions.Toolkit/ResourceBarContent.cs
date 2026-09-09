// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Describes a horizontal resource composition with immutable metadata and
/// complete snapshots of non-negative values. Segments share the full bar.
/// </summary>
public partial class ResourceBarContent : BaseObservable, IResourceBarContent
{
    private readonly GraphSeriesInfo[] _series;
    private readonly GraphValueScale[] _valueScales;
    private double[] _values;

    public string DisplayName { get; init; } = string.Empty;

    public string ValueFormat { get; init; } = "0.0";

    public string ValueSuffix { get; init; } = string.Empty;

    public ResourceBarContent(GraphSeriesInfo[] series, GraphValueScale[]? valueScales = null)
    {
        _series = GraphContentValidation.CopySeries(series);
        _valueScales = GraphContentValidation.CopyValueScales(valueScales ?? []);
        _values = new double[_series.Length];
    }

    public IGraphSeriesInfo[] GetSeries() => [.. _series];

    public IGraphValueScale[] GetValueScales() => [.. _valueScales];

    public double[] GetSnapshot() => [.. Volatile.Read(ref _values)];

    /// <summary>
    /// Publishes one value per configured series before notifying readers.
    /// All-zero values leave the bar empty. Readers receive independent copies.
    /// </summary>
    public void SetSnapshot(double[] values)
    {
        var snapshot = GraphContentValidation.CopyValues(values, _series.Length);
        Volatile.Write(ref _values, snapshot);
        OnPropertyChanged("Data");
    }
}
