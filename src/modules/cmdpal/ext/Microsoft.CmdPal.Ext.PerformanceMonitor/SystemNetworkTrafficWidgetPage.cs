// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading;
using CoreWidgetProvider.Helpers;
using CoreWidgetProvider.Widgets.Enums;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

internal sealed partial class SystemNetworkTrafficWidgetPage : WidgetPage, IDisposable
{
    private readonly SystemNetworkUsageWidgetPage _source;
    private readonly SettingsManager _settingsManager;
    private readonly Lock _lifecycleLock = new();
    private SpeedUnit _speedUnit;
    private GraphValueScale[] _valueScales;
    private bool _ownsActivation;
    private bool _disposed;

    public override string Id => "com.microsoft.cmdpal.network_traffic_widget";

    public override string Title => Resources.GetResource("Network_Traffic_Title");

    public override IconInfo Icon => Icons.NetworkIcon;

    public SystemNetworkTrafficWidgetPage(SystemNetworkUsageWidgetPage source, SettingsManager settingsManager)
    {
        _source = source;
        _settingsManager = settingsManager;
        _speedUnit = GetSpeedUnit();
        _valueScales = CreateValueScales(_speedUnit);
        UsageGraph = CreateTrafficGraph();
        Commands = source.Commands;
        _source.Updated += Source_Updated;
        _settingsManager.Settings.SettingsChanged += Settings_SettingsChanged;
        UpdateWidget();
    }

    private LineGraphContent CreateTrafficGraph()
    {
        return new LineGraphContent(
            [
                new GraphSeriesInfo { Name = Resources.GetResource("Network_Traffic_Send"), Color = ColorHelpers.FromRgb(185, 120, 198), LineStyle = GraphLineStyle.Dotted },
                new GraphSeriesInfo { Name = Resources.GetResource("Network_Traffic_Receive"), Color = ColorHelpers.FromRgb(232, 160, 88) },
            ],
            maximum: _speedUnit == SpeedUnit.BinaryBytesPerSecond ? 1024 : 1000,
            valueScales: _valueScales)
        {
            DisplayName = Title,
            HistoryDuration = UsageHistory.Duration,
            AutoScaleMaximum = true,
            Smoothing = 0.2,
        };
    }

    protected override void LoadContentData()
    {
        var snapshot = _source.CurrentSnapshot;
        ContentData.Clear();
        if (snapshot.ErrorMessage is { } error)
        {
            ContentData["errorMessage"] = error;
            return;
        }

        ContentData["networkName"] = snapshot.Name;
        ContentData["netSent"] = FormatRate(snapshot.Usage.Sent);
        ContentData["netReceived"] = FormatRate(snapshot.Usage.Received);
        UsageGraph!.SetSnapshot(GetGraphSamples(snapshot));
    }

    public string GetUpSpeed() => FormatRate(_source.CurrentSnapshot.Usage.Sent);

    public string GetDownSpeed() => FormatRate(_source.CurrentSnapshot.Usage.Received);

    public string GetSummary()
    {
        var snapshot = _source.CurrentSnapshot;
        return string.IsNullOrEmpty(snapshot.Name)
            ? string.Empty
            : string.Format(CultureInfo.CurrentCulture, Resources.GetResource("Network_Traffic_Summary"), snapshot.Name, FormatRate(snapshot.Usage.Sent), FormatRate(snapshot.Usage.Received));
    }

    private SpeedUnit GetSpeedUnit() => _settingsManager.NetworkSpeedUnit switch
    {
        SpeedUnit.BytesPerSecond => SpeedUnit.BytesPerSecond,
        SpeedUnit.BinaryBytesPerSecond => SpeedUnit.BinaryBytesPerSecond,
        _ => SpeedUnit.BitsPerSecond,
    };

    private static GraphValueScale[] CreateValueScales(SpeedUnit unit)
    {
        string[] suffixes = unit switch
        {
            SpeedUnit.BytesPerSecond => [" B/s", " KB/s", " MB/s", " GB/s", " TB/s", " PB/s", " EB/s"],
            SpeedUnit.BinaryBytesPerSecond => [" B/s", " KiB/s", " MiB/s", " GiB/s", " TiB/s", " PiB/s", " EiB/s"],
            _ => [" bps", " Kbps", " Mbps", " Gbps", " Tbps", " Pbps", " Ebps"],
        };
        var factor = unit == SpeedUnit.BinaryBytesPerSecond ? 1024d : 1000d;
        var divisor = 1d;
        var scales = new GraphValueScale[suffixes.Length];
        for (var index = 0; index < scales.Length; index++)
        {
            scales[index] = new GraphValueScale { Divisor = divisor, Suffix = suffixes[index] };
            divisor *= factor;
        }

        return scales;
    }

    private string FormatRate(double bytesPerSecond)
        => GraphValueFormatter.Format(_speedUnit == SpeedUnit.BitsPerSecond ? bytesPerSecond * 8 : bytesPerSecond, valueScales: _valueScales);

    private GraphSample[] GetGraphSamples(NetworkSnapshot snapshot)
    {
        if (_speedUnit != SpeedUnit.BitsPerSecond)
        {
            return snapshot.TrafficHistory;
        }

        var samples = (GraphSample[])snapshot.TrafficHistory.Clone();
        for (var index = 0; index < samples.Length; index++)
        {
            samples[index].Value *= 8;
        }

        return samples;
    }

    private void Source_Updated(object? sender, EventArgs args)
    {
        if (!_disposed)
        {
            UpdateWidget();
        }
    }

    private void Settings_SettingsChanged(object sender, Settings args)
    {
        lock (ContentData)
        {
            var unit = GetSpeedUnit();
            if (_disposed || _speedUnit == unit)
            {
                return;
            }

            _speedUnit = unit;
            _valueScales = CreateValueScales(unit);
            var graph = CreateTrafficGraph();
            graph.SetSnapshot(GetGraphSamples(_source.CurrentSnapshot));
            UsageGraph = graph;
        }

        RaiseItemsChanged();
        UpdateWidget();
    }

    protected override string GetTemplatePath(WidgetPageState page) => page switch
    {
        WidgetPageState.Content or WidgetPageState.Loading => @"DevHome\Templates\SystemNetworkUsageTemplate.json",
        _ => throw new NotImplementedException(),
    };

    protected override void OnActivated()
    {
        lock (_lifecycleLock)
        {
            if (!_disposed && !_ownsActivation)
            {
                _ownsActivation = true;
                _source.PushActivate();
            }
        }
    }

    protected override void OnDeactivated()
    {
        lock (_lifecycleLock)
        {
            ReleaseActivation();
        }
    }

    private void ReleaseActivation()
    {
        if (_ownsActivation)
        {
            _ownsActivation = false;
            _source.PopActivate();
        }
    }

    public void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ReleaseActivation();
        }

        _source.Updated -= Source_Updated;
        _settingsManager.Settings.SettingsChanged -= Settings_SettingsChanged;
    }
}
