// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>Renders an Adaptive Cards <c>Chart.VerticalBar</c> or <c>Chart.HorizontalBar</c> element.</summary>
internal sealed partial class BarChartControl : AdaptiveVisualControl
{
    private const double MinimumPlotHeight = 88;
    private const double MaximumPlotHeight = 180;
    private const double PlotHeightToWidth = 0.24;
    private const double PlotTopPadding = 6;
    private const double BarFill = 0.62;
    private const double MaximumBarWidth = 40;
    private const double MinimumXLabelSpacing = 56;
    private const double HorizontalBarHeight = 10;

    private BarChartModel _model;

    public BarChartControl(BarChartModel model)
    {
        _model = model;
        Render();
    }

    public override string IncrementalState => _model.IncrementalState;

    protected override bool WidthAffectsLayout => _model.Orientation == BarOrientation.Vertical;

    protected override void ApplyModel(AdaptiveVisualControl candidate) => _model = ((BarChartControl)candidate)._model;

    protected override void RenderCore()
    {
        var isDarkTheme = IsDarkTheme;
        var secondary = ChartTheme.ToBrush(ChartTheme.GetTextColor(isDarkTheme, secondary: true));
        var (dataMin, dataMax) = _model.GetValueExtent();
        var range = ChartScale.Compute(_model.YMin, _model.YMax, Math.Min(0, dataMin), dataMax);

        var root = new StackPanel { Spacing = 4 };
        if (!string.IsNullOrWhiteSpace(_model.Title))
        {
            var title = ChartShapes.CreateText(_model.Title, ChartShapes.BodyStrongStyle);
            title.Margin = new Thickness(0, 0, 0, 4);
            root.Children.Add(title);
        }

        if (_model.Orientation == BarOrientation.Horizontal)
        {
            root.Children.Add(CreateHorizontalBars(range, isDarkTheme, secondary));
        }
        else
        {
            root.Children.Add(CreateLabelRow(_model.YAxisTitle, ChartValueFormatter.Format(range.Max, _model.ValueFormat, CultureInfo.CurrentCulture), secondary));
            root.Children.Add(CreateVerticalPlot(range, isDarkTheme, secondary));

            // Bars start at zero, so the minimum only needs a label when it isn't zero.
            var minimum = range.Min == 0 ? string.Empty : ChartValueFormatter.Format(range.Min, _model.ValueFormat, CultureInfo.CurrentCulture);
            if (!string.IsNullOrWhiteSpace(_model.XAxisTitle) || minimum.Length > 0)
            {
                root.Children.Add(CreateLabelRow(_model.XAxisTitle, minimum, secondary));
            }
        }

        Content = root;
        AutomationProperties.SetName(this, !string.IsNullOrWhiteSpace(_model.Title) ? _model.Title : RS_.GetString("AdaptiveChart_BarChart"));
        AutomationProperties.SetHelpText(this, CreateSummary());
    }

    private StackPanel CreateVerticalPlot(ChartAxisRange range, bool isDarkTheme, Brush secondary)
    {
        var width = LayoutWidth;
        var height = Math.Clamp(width * PlotHeightToWidth, MinimumPlotHeight, MaximumPlotHeight);
        var plot = new Canvas { Width = width, Height = height };
        var bottom = height - 1;
        double MapY(double value) => bottom - (range.Normalize(value) * (bottom - PlotTopPadding));

        var grid = new GeometryGroup();
        foreach (var tick in range.GetTicks())
        {
            var y = Math.Round(MapY(tick)) + 0.5;
            grid.Children.Add(new LineGeometry { StartPoint = new Point(0, y), EndPoint = new Point(width, y) });
        }

        plot.Children.Add(new Path { Data = grid, Stroke = ChartTheme.ToBrush(GetGridLineColor(isDarkTheme)), StrokeThickness = 1 });

        var count = Math.Max(1, _model.Data.Count);
        var slot = width / count;
        var barWidth = Math.Min(slot * BarFill, MaximumBarWidth);
        var baseline = MapY(Math.Clamp(0, range.Min, range.Max));
        var labels = new Canvas { Width = width, Height = 16 };
        var labelStep = Math.Max(1, (int)Math.Ceiling(MinimumXLabelSpacing / Math.Max(1, slot)));
        for (var i = 0; i < _model.Data.Count; i++)
        {
            var point = _model.Data[i];
            var top = MapY(point.Value);
            var barTop = Math.Min(top, baseline);
            var barHeight = Math.Max(1, Math.Abs(baseline - top));
            var left = (slot * i) + ((slot - barWidth) / 2);
            var bar = new Rectangle
            {
                Width = barWidth,
                Height = barHeight,
                RadiusX = Math.Min(4, barWidth / 2),
                RadiusY = Math.Min(4, barWidth / 2),
                Fill = ChartTheme.ToBrush(GetBarColor(i, isDarkTheme)),
            };
            ToolTipService.SetToolTip(bar, $"{point.Label}: {ChartValueFormatter.Format(point.Value, _model.ValueFormat, CultureInfo.CurrentCulture)}");
            ChartShapes.Place(bar, left, barTop);
            plot.Children.Add(bar);

            if (_model.ShowBarValues)
            {
                var value = ChartShapes.CreateText(ChartValueFormatter.Format(point.Value, _model.ValueFormat, CultureInfo.CurrentCulture), ChartShapes.CaptionStyle);
                var size = ChartShapes.Measure(value);
                ChartShapes.Place(value, left + ((barWidth - size.Width) / 2), Math.Max(0, barTop - size.Height - 2));
                plot.Children.Add(value);
            }

            if (!string.IsNullOrEmpty(point.Label) && i % labelStep == 0)
            {
                var label = ChartShapes.CreateText(point.Label, ChartShapes.CaptionStyle, secondary);
                var size = ChartShapes.Measure(label);
                ChartShapes.Place(label, Math.Clamp((slot * i) + ((slot - size.Width) / 2), 0, Math.Max(0, width - size.Width)), 0);
                labels.Children.Add(label);
            }
        }

        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(plot);
        if (labels.Children.Count > 0)
        {
            panel.Children.Add(labels);
        }

        return panel;
    }

    private Grid CreateHorizontalBars(ChartAxisRange range, bool isDarkTheme, Brush secondary)
    {
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var track = ChartTheme.ToBrush(ChartTheme.GetTrackColor(isDarkTheme));
        for (var i = 0; i < _model.Data.Count; i++)
        {
            var point = _model.Data[i];
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = ChartShapes.CreateText(point.Label ?? string.Empty, ChartShapes.BodyStyle, secondary);
            label.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(label, i);
            grid.Children.Add(label);

            var fraction = range.Normalize(point.Value);
            var bar = new Grid { Height = HorizontalBarHeight, VerticalAlignment = VerticalAlignment.Center };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(fraction, 0.0001), GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(1 - fraction, 0.0001), GridUnitType.Star) });
            var trackBorder = new Border { Background = track, CornerRadius = new CornerRadius(HorizontalBarHeight / 2) };
            Grid.SetColumnSpan(trackBorder, 2);
            bar.Children.Add(trackBorder);
            bar.Children.Add(new Border
            {
                Background = ChartTheme.ToBrush(GetBarColor(i, isDarkTheme)),
                CornerRadius = new CornerRadius(HorizontalBarHeight / 2),
            });
            Grid.SetRow(bar, i);
            Grid.SetColumn(bar, 1);
            grid.Children.Add(bar);

            var value = ChartShapes.CreateText(ChartValueFormatter.Format(point.Value, _model.ValueFormat, CultureInfo.CurrentCulture), ChartShapes.BodyStrongStyle);
            value.HorizontalAlignment = HorizontalAlignment.Right;
            value.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(value, i);
            Grid.SetColumn(value, 2);
            grid.Children.Add(value);
        }

        return grid;
    }

    private static Grid CreateLabelRow(string? left, string right, Brush foreground)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var leftText = ChartShapes.CreateText(left ?? string.Empty, ChartShapes.CaptionStyle, foreground);
        leftText.TextTrimming = TextTrimming.CharacterEllipsis;
        var rightText = ChartShapes.CreateText(right, ChartShapes.CaptionStyle, foreground);
        Grid.SetColumn(rightText, 1);
        row.Children.Add(leftText);
        row.Children.Add(rightText);
        return row;
    }

    // One color for every bar unless the card asks for per-bar colors with a color set.
    private ChartColor GetBarColor(int index, bool isDarkTheme) =>
        ChartTheme.Resolve(_model.Data[index].Color, _model.Color, _model.ColorSet, _model.ColorSet is null ? 0 : index, isDarkTheme);

    private static ChartColor GetGridLineColor(bool isDarkTheme) =>
        isDarkTheme ? new ChartColor(0x15, 0xFF, 0xFF, 0xFF) : new ChartColor(0x0F, 0x00, 0x00, 0x00);

    private string CreateSummary()
    {
        var summary = new StringBuilder();
        foreach (var point in _model.Data)
        {
            if (summary.Length > 0)
            {
                summary.Append(", ");
            }

            summary.Append(point.Label)
                .Append(' ')
                .Append(ChartValueFormatter.Format(point.Value, _model.ValueFormat, CultureInfo.CurrentCulture));
        }

        return summary.ToString();
    }
}
