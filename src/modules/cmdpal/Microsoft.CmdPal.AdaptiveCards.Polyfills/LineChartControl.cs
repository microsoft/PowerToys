// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.CmdPal.AdaptiveCards.IncrementalRendering;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>
/// Renders an Adaptive Cards <c>Chart.Line</c> element as smooth, filled lines. New versions apply
/// in place, and a one-sample shift of live data animates as a scroll. Narrower than
/// <see cref="LineChartLayout.CompactWidth"/>, the chart draws as a sparkline: just the lines.
/// </summary>
internal sealed partial class LineChartControl : UserControl, IIncrementalAdaptiveElementControl
{
    private const double LineThickness = 2;
    private const double MarkerDiameter = 7;
    private const double MarkerHaloDiameter = 15;
    private const double IsolatedPointDiameter = 4;
    private const double LegendSwatchDiameter = 8;
    private const double PlotTopPadding = 6;
    private const double PlotRightPadding = 8;
    private const double SparklineRightPadding = 4;
    private const double PlotBottomPadding = 1;
    private const double SparklineHeight = 36;
    private const double MinimumPlotHeight = 88;
    private const double MaximumPlotHeight = 180;
    private const double PlotHeightToWidth = 0.24;
    private const double MinimumXLabelSpacing = 72;
    private const double DarkThemeAreaOpacity = 0.36;
    private const double LightThemeAreaOpacity = 0.24;
    private const double MarkerHaloOpacity = 0.28;

    private static readonly TimeSpan DefaultScrollDuration = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MinimumScrollDuration = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MaximumScrollDuration = TimeSpan.FromMilliseconds(1500);
    private static readonly CompositeFormat SeriesNameFormat = CompositeFormat.Parse(RS_.GetString("AdaptiveChart_SeriesName"));
    private static readonly CompositeFormat SeriesSummaryFormat = CompositeFormat.Parse(RS_.GetString("AdaptiveChart_SeriesSummary"));

    private LineChartModel _model;
    private bool _isCompact;
    private Storyboard? _scrollStoryboard;
    private TimeSpan _scrollDuration = DefaultScrollDuration;
    private long _lastUpdateTimestamp;

    public LineChartControl(LineChartModel model)
    {
        _model = model;
        InitializeComponent();
        ApplyText();
        SizeChanged += OnSizeChanged;
        PlotCanvas.SizeChanged += (_, _) => Redraw(scrolled: false);
        ActualThemeChanged += (_, _) => Redraw(scrolled: false);
        Unloaded += (_, _) => StopScrollAnimation();
    }

    public string IncrementalState => _model.IncrementalState;

    public bool CanApplyIncrementalState(IIncrementalAdaptiveElementControl candidate) =>
        candidate is LineChartControl;

    public void ApplyIncrementalState(IIncrementalAdaptiveElementControl candidate)
    {
        var next = ((LineChartControl)candidate)._model;
        var scrolled = LineChartUpdate.IsScrolledByOne(_model, next);

        // Scroll for as long as the data took to arrive, so the motion looks continuous.
        var now = Stopwatch.GetTimestamp();
        _scrollDuration = _lastUpdateTimestamp == 0
            ? DefaultScrollDuration
            : Clamp(Stopwatch.GetElapsedTime(_lastUpdateTimestamp, now), MinimumScrollDuration, MaximumScrollDuration);
        _lastUpdateTimestamp = now;

        _model = next;
        ApplyText();
        UpdatePlotHeight(ActualWidth);
        Redraw(scrolled);
    }

    private void ApplyText()
    {
        var title = _model.ShowTitle && !_isCompact ? _model.Title : null;
        TitleText.Text = title ?? string.Empty;
        TitleText.Visibility = string.IsNullOrEmpty(title) ? Visibility.Collapsed : Visibility.Visible;
        YAxisTitleText.Text = _model.YAxisTitle ?? string.Empty;
        XAxisTitleText.Text = _model.XAxisTitle ?? string.Empty;
        HeaderRow.Visibility = _isCompact ? Visibility.Collapsed : Visibility.Visible;
        FooterRow.Visibility = _isCompact ? Visibility.Collapsed : Visibility.Visible;

        var name = FirstNonEmpty(
            _model.Title,
            _model.YAxisTitle,
            _model.Series.Count > 0 ? _model.Series[0].Legend : null)
            ?? RS_.GetString("AdaptiveChart_LineChart");
        AutomationProperties.SetName(this, name);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width != e.PreviousSize.Width)
        {
            var isCompact = LineChartLayout.IsCompact(e.NewSize.Width);
            if (isCompact != _isCompact)
            {
                _isCompact = isCompact;
                ApplyText();
            }

            UpdatePlotHeight(e.NewSize.Width);
        }
    }

    private void UpdatePlotHeight(double width)
    {
        if (width <= 0)
        {
            return;
        }

        var height = _isCompact
            ? SparklineHeight
            : Math.Clamp(width * PlotHeightToWidth, MinimumPlotHeight, MaximumPlotHeight);
        if (Math.Abs(PlotRow.Height.Value - height) > 0.5)
        {
            PlotRow.Height = new GridLength(height);
        }
    }

    private void Redraw(bool scrolled)
    {
        SeriesLayer.Children.Clear();
        var width = PlotCanvas.ActualWidth;
        var height = PlotCanvas.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            StopScrollAnimation();
            return;
        }

        PlotCanvas.Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, height) };

        var (dataMin, dataMax) = _model.GetValueExtent();
        var range = ChartScale.Compute(_model.YMin, _model.YMax, dataMin, dataMax);
        MaxLabelText.Text = ChartValueFormatter.FormatCompact(range.Max);
        MinLabelText.Text = ChartValueFormatter.FormatCompact(range.Min);

        var layout = new LineChartLayout(
            width,
            height,
            range,
            _model.SlotCount,
            PlotTopPadding,
            _isCompact ? SparklineRightPadding : PlotRightPadding,
            PlotBottomPadding);
        GridLinesPath.Data = _isCompact ? null : CreateGridLines(layout);

        var isDarkTheme = ActualTheme == ElementTheme.Dark;
        var isHighContrast = ChartTheme.IsHighContrast;
        var fillOpacity = isHighContrast ? 0 : (isDarkTheme ? DarkThemeAreaOpacity : LightThemeAreaOpacity);
        var markerScale = _isCompact ? 0.75 : 1;
        var colors = new ChartColor[_model.Series.Count];
        for (var i = 0; i < _model.Series.Count; i++)
        {
            var series = _model.Series[i];
            colors[i] = ChartTheme.Resolve(series.Color, _model.Color, _model.ColorSet, i, isDarkTheme);
            var dashPattern = isHighContrast ? ChartPalette.GetHighContrastDashPattern(i) : null;
            DrawSeries(series, layout, colors[i], fillOpacity, markerScale, dashPattern);
        }

        UpdateLegend(colors, isHighContrast);
        UpdateXLabels(layout);
        UpdateSummary();

        if (scrolled && layout.SlotWidth > 0 && ChartTheme.AnimationsEnabled)
        {
            StartScrollAnimation(layout.SlotWidth);
        }
        else
        {
            StopScrollAnimation();
        }
    }

    private void DrawSeries(
        LineChartSeries series,
        LineChartLayout layout,
        ChartColor color,
        double fillOpacity,
        double markerScale,
        IReadOnlyList<double>? dashPattern)
    {
        var points = layout.MapPoints(series.Points);
        if (points.Length == 0)
        {
            return;
        }

        var stroke = new SolidColorBrush(ToColor(color));
        if (points.Length == 1)
        {
            AddDot(points[0], IsolatedPointDiameter, stroke);
        }
        else
        {
            if (fillOpacity > 0)
            {
                SeriesLayer.Children.Add(new Path { Data = CreateAreaGeometry(points, layout), Fill = CreateAreaBrush(color, layout, fillOpacity) });
            }

            var line = new Path
            {
                Data = CreateLineGeometry(points),
                Stroke = stroke,
                StrokeThickness = LineThickness,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };
            if (dashPattern is not null)
            {
                ChartShapes.ApplyDashPattern(line, dashPattern);
            }

            SeriesLayer.Children.Add(line);
        }

        // Mark the newest sample: the "now" of a live series.
        AddDot(points[^1], MarkerHaloDiameter * markerScale, new SolidColorBrush(ToColor(color.WithOpacity(MarkerHaloOpacity))));
        AddDot(points[^1], MarkerDiameter * markerScale, stroke);
    }

    private PathGeometry CreateLineGeometry(IReadOnlyList<ChartPoint> points)
    {
        var figure = new PathFigure { StartPoint = ToPoint(points[0]), IsClosed = false, IsFilled = false };
        AppendCurve(figure, points);
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private PathGeometry CreateAreaGeometry(IReadOnlyList<ChartPoint> points, LineChartLayout layout)
    {
        var figure = new PathFigure
        {
            StartPoint = new Point(points[0].X, layout.PlotBottom),
            IsClosed = true,
            IsFilled = true,
        };
        figure.Segments.Add(new LineSegment { Point = ToPoint(points[0]) });
        AppendCurve(figure, points);
        figure.Segments.Add(new LineSegment { Point = new Point(points[^1].X, layout.PlotBottom) });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private void AppendCurve(PathFigure figure, IReadOnlyList<ChartPoint> points)
    {
        if (points.Count > 2)
        {
            foreach (var segment in ChartCurve.CreateMonotoneSegments(points))
            {
                figure.Segments.Add(new BezierSegment
                {
                    Point1 = ToPoint(segment.Control1),
                    Point2 = ToPoint(segment.Control2),
                    Point3 = ToPoint(segment.End),
                });
            }

            return;
        }

        for (var i = 1; i < points.Count; i++)
        {
            figure.Segments.Add(new LineSegment { Point = ToPoint(points[i]) });
        }
    }

    private static LinearGradientBrush CreateAreaBrush(ChartColor color, LineChartLayout layout, double opacity)
    {
        // Absolute mapping ties the gradient to the plot, so equal values always get the same fill intensity.
        var brush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(0, layout.PlotTop),
            EndPoint = new Point(0, layout.PlotBottom),
        };
        brush.GradientStops.Add(new GradientStop { Color = ToColor(color.WithOpacity(opacity)), Offset = 0 });
        brush.GradientStops.Add(new GradientStop { Color = ToColor(color.WithOpacity(0)), Offset = 1 });
        return brush;
    }

    private static GeometryGroup CreateGridLines(LineChartLayout layout)
    {
        var group = new GeometryGroup();
        foreach (var tick in layout.Range.GetTicks())
        {
            // Center each 1px line on a pixel so it stays crisp.
            var y = Math.Round(layout.MapY(tick)) + 0.5;
            group.Children.Add(new LineGeometry
            {
                StartPoint = new Point(0, y),
                EndPoint = new Point(layout.Width, y),
            });
        }

        return group;
    }

    private void AddDot(ChartPoint center, double diameter, Brush fill)
    {
        var dot = new Ellipse { Width = diameter, Height = diameter, Fill = fill };
        Canvas.SetLeft(dot, center.X - (diameter / 2));
        Canvas.SetTop(dot, center.Y - (diameter / 2));
        SeriesLayer.Children.Add(dot);
    }

    private void UpdateLegend(IReadOnlyList<ChartColor> colors, bool isHighContrast)
    {
        LegendPanel.Children.Clear();
        if (!_model.ShowsLegend || _isCompact)
        {
            LegendPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var captionStyle = ChartTheme.GetTextStyle("CaptionTextBlockStyle");
        for (var i = 0; i < _model.Series.Count; i++)
        {
            var entry = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var brush = new SolidColorBrush(ToColor(colors[i]));

            // In high contrast, lines also differ by dash pattern, so the legend shows it.
            entry.Children.Add(isHighContrast
                ? (UIElement)ChartShapes.CreateLineSwatch(brush, LineThickness, ChartPalette.GetHighContrastDashPattern(i))
                : new Ellipse
                {
                    Width = LegendSwatchDiameter,
                    Height = LegendSwatchDiameter,
                    Fill = brush,
                    VerticalAlignment = VerticalAlignment.Center,
                });
            entry.Children.Add(new TextBlock
            {
                Text = GetSeriesName(i),
                Style = captionStyle,
                VerticalAlignment = VerticalAlignment.Center,
            });
            LegendPanel.Children.Add(entry);
        }

        LegendPanel.Visibility = Visibility.Visible;
    }

    private void UpdateXLabels(LineChartLayout layout)
    {
        XLabelsCanvas.Children.Clear();
        if (!_model.HasPointLabels || _isCompact || _model.SlotCount == 0)
        {
            XLabelsCanvas.Visibility = Visibility.Collapsed;
            return;
        }

        XLabelsCanvas.Visibility = Visibility.Visible;
        var points = GetLongestSeries().Points;
        var firstSlot = layout.GetFirstSlot(points.Count);
        var maxLabels = Math.Max(2, (int)(layout.Width / MinimumXLabelSpacing));
        var step = Math.Max(1, (int)Math.Ceiling((points.Count - 1) / (double)Math.Max(1, maxLabels - 1)));
        var captionStyle = ChartTheme.GetTextStyle("CaptionTextBlockStyle");
        var previousRight = double.NegativeInfinity;
        for (var i = 0; i < points.Count; i++)
        {
            var isLast = i == points.Count - 1;
            if ((i % step != 0 && !isLast) || points[i].Label is not string label)
            {
                continue;
            }

            var text = new TextBlock { Text = label, Style = captionStyle, Foreground = MinLabelText.Foreground };
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var labelWidth = text.DesiredSize.Width;
            var left = Math.Clamp(
                layout.MapX(firstSlot + i) - (labelWidth / 2),
                0,
                Math.Max(0, layout.Width - labelWidth));
            if (left < previousRight + 8)
            {
                continue;
            }

            Canvas.SetLeft(text, left);
            XLabelsCanvas.Children.Add(text);
            previousRight = left + labelWidth;
        }
    }

    private void UpdateSummary()
    {
        var summary = new StringBuilder();
        for (var i = 0; i < _model.Series.Count; i++)
        {
            var points = _model.Series[i].Points;
            if (points.Count == 0)
            {
                continue;
            }

            var low = double.MaxValue;
            var high = double.MinValue;
            foreach (var point in points)
            {
                low = Math.Min(low, point.Y);
                high = Math.Max(high, point.Y);
            }

            if (summary.Length > 0)
            {
                summary.Append(' ');
            }

            summary.Append(string.Format(
                CultureInfo.CurrentCulture,
                SeriesSummaryFormat,
                GetSeriesName(i),
                ChartValueFormatter.FormatCompact(points[^1].Y),
                ChartValueFormatter.FormatCompact(low),
                ChartValueFormatter.FormatCompact(high)));
        }

        AutomationProperties.SetHelpText(this, summary.ToString());
    }

    private void StartScrollAnimation(double slotWidth)
    {
        StopScrollAnimation();

        // The new samples are drawn in place, so starting one slot to the right shows exactly
        // the previous frame; sliding back to zero reveals the newest sample.
        var animation = new DoubleAnimation
        {
            From = slotWidth,
            To = 0,
            Duration = new Duration(_scrollDuration),
        };
        Storyboard.SetTarget(animation, ScrollTransform);
        Storyboard.SetTargetProperty(animation, "X");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
        _scrollStoryboard = storyboard;
    }

    private void StopScrollAnimation()
    {
        _scrollStoryboard?.Stop();
        _scrollStoryboard = null;
    }

    private LineChartSeries GetLongestSeries()
    {
        var longest = _model.Series[0];
        foreach (var series in _model.Series)
        {
            if (series.Points.Count > longest.Points.Count)
            {
                longest = series;
            }
        }

        return longest;
    }

    private string GetSeriesName(int index) =>
        _model.Series[index].Legend
            ?? string.Format(CultureInfo.CurrentCulture, SeriesNameFormat, index + 1);

    private static global::Windows.UI.Color ToColor(ChartColor color) => ChartTheme.ToColor(color);

    private static Point ToPoint(ChartPoint point) => new(point.X, point.Y);

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max) =>
        TimeSpan.FromTicks(Math.Clamp(value.Ticks, min.Ticks, max.Ticks));

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
