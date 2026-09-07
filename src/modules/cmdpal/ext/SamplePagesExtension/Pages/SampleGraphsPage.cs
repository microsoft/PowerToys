// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace SamplePagesExtension;

internal sealed partial class SampleGraphsPage : ContentPage, INotifyItemsChanged, IDisposable
{
    private readonly object _gate = new();
    private readonly LineGraphContent _line = new(
        [new GraphSeriesInfo { Name = "Interactive" }, new GraphSeriesInfo { Name = "Background" }],
        maximum: 150)
    {
        DisplayName = "Requests per second",
        HistoryDuration = TimeSpan.FromSeconds(30),
        Smoothing = 1,
        ValueFormat = "0.0",
        ValueSuffix = " req/s",
    };

    private readonly VerticalUsageBarContent _scalar = new()
    {
        DisplayName = "Queue capacity",
        IndicatorColor = ColorHelpers.FromRgb(0, 170, 150),
    };

    private readonly VerticalUsageBarContent _stack = new(
        [new GraphSeriesInfo { Name = "Active" }, new GraphSeriesInfo { Name = "Cached" }],
        maximum: 32)
    {
        DisplayName = "Storage allocation",
    };

    private readonly ResourceBarContent _resource = new(
        [new GraphSeriesInfo { Name = "Active" }, new GraphSeriesInfo { Name = "Cached" }, new GraphSeriesInfo { Name = "Free" }])
    {
        DisplayName = "Resource composition",
        ValueSuffix = " GB",
    };

    private readonly DoughnutGraphContent _doughnut = new(
        [new GraphSeriesInfo { Name = "Documents" }, new GraphSeriesInfo { Name = "Media" }, new GraphSeriesInfo { Name = "Other" }])
    {
        DisplayName = "Storage composition",
    };

    private readonly List<GraphSample>[] _history = [[], []];
    private readonly DateTimeOffset _origin = DateTimeOffset.UtcNow;
    private readonly long _originTimestamp = Stopwatch.GetTimestamp();
    private readonly IContent[] _content;
    private TypedEventHandler<object, IItemsChangedEventArgs> _listeners;
    private Timer _timer;
    private bool _paused;
    private bool _disposed;
    private int _tick;

    public SampleGraphsPage(bool nested = false)
    {
        Name = "Open";
        Title = nested ? "Nested graphs" : "Live graphs";
        Icon = new IconInfo("\uE9D9");
        SeedHistory();
        var description = new MarkdownContent("Interactive samples every second; Background samples every 3 seconds. The graph uses a 1.5-second buffer: Interactive scrolls smoothly and Background advances in steps. Use the command menu to pause, reset, or clear. Hover or use Left/Right and Home/End to inspect values.");
        _content = nested
            ? [description, new TreeContent { RootContent = _line, Children = [_scalar, _stack, _resource, _doughnut] }]
            : [description, _line, _scalar, _stack, _resource, _doughnut];
        Commands =
        [
            new CommandContextItem(title: "Pause or resume", name: "Toggle", action: TogglePause, result: CommandResult.KeepOpen()),
            new CommandContextItem(title: "Reset samples", name: "Reset", action: Reset, result: CommandResult.KeepOpen()),
            new CommandContextItem(title: "Clear samples", name: "Clear", action: Clear, result: CommandResult.KeepOpen()),
        ];
    }

    // The sample owns one timer while the page has observers. Unsubscribing stops it.
    event TypedEventHandler<object, IItemsChangedEventArgs> INotifyItemsChanged.ItemsChanged
    {
        add
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _listeners += value;
                if (_timer is null)
                {
                    SeedHistory();
                    _timer = new Timer(Tick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
                }
            }
        }

        remove
        {
            lock (_gate)
            {
                _listeners -= value;
                if (_listeners is null)
                {
                    _timer?.Dispose();
                    _timer = null;
                }
            }
        }
    }

    internal IContent[] Graphs => [_line, _scalar, _stack, _resource, _doughnut];

    public override IContent[] GetContent() => _content;

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _listeners = null;
            _timer?.Dispose();
            _timer = null;
        }
    }

    private DateTimeOffset Now => _origin + Stopwatch.GetElapsedTime(_originTimestamp);

    private void SeedHistory()
    {
        foreach (var history in _history)
        {
            history.Clear();
        }

        var now = Now;
        _tick = 0;
        for (var index = -32; index <= 0; index++)
        {
            var timestamp = now.AddSeconds(index);
            AddObservation(0, timestamp);
            if (index % 3 == 0)
            {
                AddObservation(1, timestamp.AddMilliseconds(-175));
            }
        }

        Publish(now);
    }

    private void Tick(object state)
    {
        lock (_gate)
        {
            if (_listeners is null || _paused)
            {
                return;
            }

            var now = Now;
            AddObservation(0, now);
            if (++_tick % 3 == 0)
            {
                AddObservation(1, now);
            }

            // Keep preceding points while the host retains the additional buffered history.
            var cutoff = GraphSampleHelpers.ToTimestampTicks(now - _line.HistoryDuration);
            foreach (var history in _history)
            {
                while (history.Count > 2 && history[2].TimestampTicks < cutoff)
                {
                    history.RemoveAt(0);
                }
            }

            Publish(now);
        }
    }

    private void AddObservation(uint seriesIndex, DateTimeOffset timestamp)
    {
        var seconds = (timestamp - _origin).TotalSeconds;
        var value = seriesIndex == 0
            ? 65 + (40 * Math.Sin(seconds / 3)) + (10 * Math.Cos(seconds * 1.8))
            : 28 + (18 * Math.Sin((seconds / 5) + 1));
        _history[seriesIndex].Add(GraphSampleHelpers.Create(seriesIndex, timestamp, value));
    }

    private void Publish(DateTimeOffset now)
    {
        // The SDK accepts interleaved or grouped streams; no timestamp alignment is needed.
        _line.SetSnapshot(_history.SelectMany(history => history).ToArray());
        var phase = (now - _origin).TotalSeconds / 4;
        var queue = 50 + (35 * Math.Sin(phase));
        _scalar.SetSnapshot(queue, queue.ToString("0", CultureInfo.CurrentCulture) + "%");
        var active = 12 + (5 * Math.Sin(phase));
        const double cached = 8;
        _stack.SetSnapshot(active + cached, (active + cached).ToString("0.0", CultureInfo.CurrentCulture) + " / 32 GB", [active, cached]);
        _resource.SetSnapshot([active, cached, 32 - active - cached]);
        _doughnut.SetSnapshot([active, cached, 4], (active + cached + 4).ToString("0.0", CultureInfo.CurrentCulture) + " GB", "Used");
    }

    private void TogglePause()
    {
        lock (_gate)
        {
            _paused = !_paused;
        }
    }

    private void Reset()
    {
        lock (_gate)
        {
            SeedHistory();
            _paused = false;
        }
    }

    private void Clear()
    {
        lock (_gate)
        {
            _paused = true;
            foreach (var history in _history)
            {
                history.Clear();
            }

            _line.SetSnapshot([]);
            _scalar.SetSnapshot(0, "0%");
            _stack.SetSnapshot(0, "0 / 32 GB", [0, 0]);
            _resource.SetSnapshot([0, 0, 0]);
            _doughnut.SetSnapshot([0, 0, 0], "0 GB", "Used");
        }
    }
}
