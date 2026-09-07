// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class ContentGraphViewModel(IContent content, WeakReference<IPageContext> context) : ContentViewModel(context)
{
    private readonly object _gate = new();
    private Configuration? _configuration;
    private Snapshot? _pendingSnapshot;
    private bool _initialized;
    private bool _subscribed;
    private bool _running;
    private bool _dirty;
    private bool _uiQueued;
    private bool _stopped;

    public enum GraphKind
    {
        Line,
        VerticalBar,
        Doughnut,
    }

    public sealed record Configuration(GraphKind Kind, string DisplayName, GraphSeriesInfo[] Series, double Minimum = 0, double Maximum = 100, TimeSpan HistoryDuration = default, string ValueFormat = "0.0", string ValueSuffix = "", OptionalColor IndicatorColor = default, double Smoothing = 0, bool AutoScaleMaximum = false, GraphValueScale[]? ValueScales = null);

    public sealed record Snapshot(Configuration Configuration, GraphSample[] Samples, double[] Values, double Value = 0, string ValueText = "", string CenterLabel = "");

    public Snapshot? Data { get; private set; }

    public override void InitializeProperties()
    {
        lock (_gate)
        {
            if (_initialized || _stopped)
            {
                return;
            }

            _initialized = _dirty = true;
            StartWorkerIfNeeded();
        }
    }

    private void Model_PropChanged(object sender, IPropChangedEventArgs args)
    {
        // Configuration is immutable. Every notification invalidates only the snapshot;
        // even reading PropertyName here would make another call into an OOP extension.
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _dirty = true;
            StartWorkerIfNeeded();
        }
    }

    // Called under _gate. A single worker owns subscription and all extension reads.
    private void StartWorkerIfNeeded()
    {
        if (!_running)
        {
            _running = true;
            _ = Task.Run(DrainUpdates);
        }
    }

    private bool IsStopped
    {
        get
        {
            lock (_gate)
            {
                return _stopped;
            }
        }
    }

    private void DrainUpdates()
    {
        try
        {
            if (!_subscribed && !IsStopped)
            {
                content.PropChanged += Model_PropChanged;
                _subscribed = true;
            }

            while (true)
            {
                lock (_gate)
                {
                    if (_stopped || !_dirty)
                    {
                        break;
                    }

                    _dirty = false;
                }

                try
                {
                    _configuration ??= ReadConfiguration();
                    Publish(ReadSnapshot(_configuration));
                }
                catch (Exception ex)
                {
                    if (!IsStopped)
                    {
                        ShowException(ex);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _dirty = false;
            }

            if (!IsStopped)
            {
                ShowException(ex);
            }
        }
        finally
        {
            if (IsStopped && _subscribed)
            {
                try
                {
                    content.PropChanged -= Model_PropChanged;
                }
                catch (Exception ex)
                {
                    ShowException(ex);
                }

                _subscribed = false;
            }

            lock (_gate)
            {
                _running = false;
                if ((!_stopped && _dirty) || (_stopped && _subscribed))
                {
                    StartWorkerIfNeeded();
                }
            }
        }
    }

    // Materialize extension-owned metadata on this worker. UI snapshots only hold
    // local immutable objects, so later validation and rendering cannot cross COM.
    // Empty native arrays can project as null; normalize them at the read boundary.
    private static GraphSeriesInfo[] ReadSeries(IGraphSeriesInfo[]? series)
    {
        series ??= [];
        var result = new GraphSeriesInfo[series.Length];
        for (var index = 0; index < series.Length; index++)
        {
            var item = series[index] ?? throw new ArgumentException("Graph series must not be null.", nameof(series));
            result[index] = new GraphSeriesInfo
            {
                Name = item.Name,
                Color = item.Color,
                LineStyle = item.LineStyle,
                IsReadoutOnly = item.IsReadoutOnly,
                ReadoutValueSuffix = item.ReadoutValueSuffix ?? string.Empty,
            };
        }

        return result;
    }

    private static GraphValueScale[] ReadValueScales(IGraphValueScale[]? scales)
    {
        scales ??= [];
        var result = new GraphValueScale[scales.Length];
        for (var index = 0; index < scales.Length; index++)
        {
            var scale = scales[index] ?? throw new ArgumentException("Graph value scales must not be null.", nameof(scales));
            result[index] = new GraphValueScale { Divisor = scale.Divisor, Suffix = scale.Suffix };
        }

        return result;
    }

    private Configuration ReadConfiguration()
    {
        var configuration = content switch
        {
            ILineGraphContent line => new Configuration(GraphKind.Line, line.DisplayName, ReadSeries(line.GetSeries()), line.Minimum, line.Maximum, line.HistoryDuration, line.ValueFormat, line.ValueSuffix, Smoothing: line.Smoothing, AutoScaleMaximum: line.AutoScaleMaximum, ValueScales: ReadValueScales(line.GetValueScales())),
            IVerticalUsageBarContent bar => new Configuration(GraphKind.VerticalBar, bar.DisplayName, ReadSeries(bar.GetSeries()), bar.Minimum, bar.Maximum, IndicatorColor: bar.IndicatorColor),
            IDoughnutGraphContent doughnut => new Configuration(GraphKind.Doughnut, doughnut.DisplayName, ReadSeries(doughnut.GetSeries())),
            _ => throw new ArgumentException("Unsupported graph content.", nameof(content)),
        };

        if (configuration.Series.Any(series => series.Name is null))
        {
            throw new ArgumentException("Graph series must have non-null names.");
        }

        if (configuration.Series.Any(series => series.LineStyle is not (GraphLineStyle.Solid or GraphLineStyle.Dashed or GraphLineStyle.Dotted)))
        {
            throw new ArgumentException("Graph series line styles must be solid, dashed, or dotted.");
        }

        if (!double.IsFinite(configuration.Minimum) || !double.IsFinite(configuration.Maximum) ||
            configuration.Maximum <= configuration.Minimum || !double.IsFinite(configuration.Maximum - configuration.Minimum))
        {
            throw new ArgumentException("Graph range must have a finite positive span.");
        }

        if (configuration.Kind == GraphKind.Line && configuration.HistoryDuration < TimeSpan.FromSeconds(1))
        {
            throw new ArgumentException("Graph history must be at least one second.");
        }

        if (!double.IsFinite(configuration.Smoothing) || configuration.Smoothing < 0 || configuration.Smoothing > 1)
        {
            throw new ArgumentException("Graph smoothing must be finite and between zero and one.");
        }

        if (configuration.Kind == GraphKind.Line)
        {
            ArgumentNullException.ThrowIfNull(configuration.ValueScales);
            var previousDivisor = 0d;
            foreach (var scale in configuration.ValueScales)
            {
                if (!double.IsFinite(scale.Divisor) || scale.Divisor <= previousDivisor || scale.Suffix is null)
                {
                    throw new ArgumentException("Graph value scales require positive, finite, increasing divisors and non-null suffixes.");
                }

                previousDivisor = scale.Divisor;
            }
        }

        return configuration;
    }

    private Snapshot ReadSnapshot(Configuration configuration)
    {
        switch (content)
        {
            case ILineGraphContent line:
                var samples = line.GetSnapshot() ?? [];
                var previous = new long?[configuration.Series.Length];
                foreach (var sample in samples)
                {
                    if (sample.SeriesIndex >= previous.Length || !double.IsFinite(sample.Value) ||
                        sample.TimestampTicks < GraphSampleHelpers.MinTimestampTicks || sample.TimestampTicks > GraphSampleHelpers.MaxTimestampTicks ||
                        (previous[sample.SeriesIndex] is { } timestamp && sample.TimestampTicks <= timestamp))
                    {
                        throw new ArgumentException("Graph samples must be finite, indexed, and ordered within each series, with UTC timestamps in years 0001 through 9999.");
                    }

                    previous[sample.SeriesIndex] = sample.TimestampTicks;
                }

                return new(configuration, samples, []);

            case IVerticalUsageBarContent bar:
                var contributions = bar.GetSnapshot(out var value, out var valueText) ?? [];
                ValidateValues(contributions, configuration.Series.Length);
                if (!double.IsFinite(value))
                {
                    throw new ArgumentException("Graph value must be finite.");
                }

                return new(configuration, [], contributions, value, valueText ?? string.Empty);

            case IDoughnutGraphContent doughnut:
                var values = doughnut.GetSnapshot(out var centerValue, out var centerLabel) ?? [];
                ValidateValues(values, configuration.Series.Length);
                return new(configuration, [], values, ValueText: centerValue ?? string.Empty, CenterLabel: centerLabel ?? string.Empty);

            default:
                throw new InvalidOperationException("Unsupported graph content.");
        }
    }

    private static void ValidateValues(double[] values, int count)
    {
        if (values.Length != count || values.Any(value => !double.IsFinite(value) || value < 0))
        {
            throw new ArgumentException("Graph contributions must match the series and be finite and nonnegative.");
        }
    }

    private void Publish(Snapshot snapshot)
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _pendingSnapshot = snapshot;
            if (_uiQueued)
            {
                return;
            }

            _uiQueued = true;
        }

        if (!TryDoOnUiThread(ApplySnapshot))
        {
            SafeCleanup();
        }
    }

    private void ApplySnapshot()
    {
        lock (_gate)
        {
            _uiQueued = false;
            if (_stopped)
            {
                return;
            }

            Data = _pendingSnapshot;
            _pendingSnapshot = null;
        }

        OnPropertyChanged(nameof(Data));
    }

    protected override void UnsafeCleanup()
    {
        lock (_gate)
        {
            _stopped = true;
            _dirty = false;
            _pendingSnapshot = null;

            // Unsubscribe on the worker too: removing a handler can cross COM.
            if (_initialized)
            {
                StartWorkerIfNeeded();
            }
        }
    }
}
