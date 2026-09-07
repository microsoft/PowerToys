// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.CmdPal.UI.Controls.Graphs;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CommandPalette.Extensions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using NativeColor = Windows.UI.Color;

namespace Microsoft.CmdPal.UI.ExtViews.Controls;

public sealed partial class GraphContentViewer : UserControl
{
    private ContentGraphViewModel? _viewModel;
    private ContentGraphViewModel.Configuration? _configuration;
    private GraphControl? _graph;

    public GraphContentViewer()
    {
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        DataContextChanged += (_, _) =>
        {
            if (IsLoaded)
            {
                Attach();
            }
        };
        SizeChanged += (_, _) =>
        {
            if (_graph is not null && ActualWidth > 0)
            {
                _graph.MaxWidth = ActualWidth;
            }
        };
    }

    private void Attach()
    {
        if (ReferenceEquals(_viewModel, DataContext))
        {
            return;
        }

        Detach();
        _viewModel = DataContext as ContentGraphViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnPropertyChanged;
            UpdateSnapshot();
        }
    }

    private void Detach()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnPropertyChanged;
        }

        _viewModel = null;
        _configuration = null;
        _graph = null;
        Content = null;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ContentGraphViewModel.Data))
        {
            UpdateSnapshot();
        }
    }

    private void UpdateSnapshot()
    {
        if (_viewModel?.Data is not { } snapshot)
        {
            return;
        }

        var configuration = snapshot.Configuration;
        if (!ReferenceEquals(_configuration, configuration))
        {
            var series = configuration.Series.Select(series => new GraphSeries(series.Name, ToColor(series.Color), ToStrokeStyle(series.LineStyle), series.IsReadoutOnly, series.ReadoutValueSuffix ?? string.Empty)).ToArray();
            _graph = configuration.Kind switch
            {
                ContentGraphViewModel.GraphKind.Line => new LiveAreaGraph(series, configuration.Minimum, configuration.Maximum, configuration.HistoryDuration, configuration.ValueFormat, configuration.ValueSuffix, ResourceLoaderInstance.GetString("GraphInspection_Now"), ResourceLoaderInstance.GetString("GraphInspection_SecondsAgo"), smoothing: configuration.Smoothing, autoScaleMaximum: configuration.AutoScaleMaximum, valueScales: configuration.ValueScales),
                ContentGraphViewModel.GraphKind.VerticalBar => new VerticalUsageBar(series, configuration.Minimum, configuration.Maximum, ToColor(configuration.IndicatorColor)),
                ContentGraphViewModel.GraphKind.Doughnut => new DoughnutGraph(series),
                _ => throw new InvalidOperationException("Unsupported graph kind."),
            };
            AutomationProperties.SetName(_graph, configuration.DisplayName);
            var layout = new StackPanel { Spacing = 8 };
            if (!string.IsNullOrEmpty(configuration.DisplayName))
            {
                layout.Children.Add(new TextBlock { Text = configuration.DisplayName, TextWrapping = TextWrapping.Wrap });
            }

            layout.Children.Add(_graph);
            Content = layout;
            _configuration = configuration;
            if (ActualWidth > 0)
            {
                _graph.MaxWidth = ActualWidth;
            }
        }

        switch (_graph)
        {
            case LiveAreaGraph line:
                var counts = new int[configuration.Series.Length];
                foreach (var sample in snapshot.Samples)
                {
                    counts[sample.SeriesIndex]++;
                }

                var points = counts.Select(count => new LineGraphPoint[count]).ToArray();
                Array.Clear(counts);
                foreach (var sample in snapshot.Samples)
                {
                    points[sample.SeriesIndex][counts[sample.SeriesIndex]++] = new(sample.Timestamp, sample.Value);
                }

                line.SetSnapshot(points);
                break;
            case VerticalUsageBar bar:
                bar.SetSnapshot(snapshot.Value, snapshot.ValueText, snapshot.Values);
                break;
            case DoughnutGraph doughnut:
                doughnut.SetSnapshot(snapshot.Values, snapshot.ValueText, snapshot.CenterLabel);
                break;
        }
    }

    private static GraphStrokeStyle ToStrokeStyle(GraphLineStyle style)
        => style switch
        {
            GraphLineStyle.Solid => GraphStrokeStyle.Solid,
            GraphLineStyle.Dashed => GraphStrokeStyle.Dashed,
            GraphLineStyle.Dotted => GraphStrokeStyle.Dotted,
            _ => throw new ArgumentOutOfRangeException(nameof(style)),
        };

    private static NativeColor? ToColor(OptionalColor color)
        => color.HasValue ? NativeColor.FromArgb(color.Color.A, color.Color.R, color.Color.G, color.Color.B) : null;
}
