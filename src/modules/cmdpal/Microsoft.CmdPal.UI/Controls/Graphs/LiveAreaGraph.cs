// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.System;

namespace Microsoft.CmdPal.UI.Controls.Graphs;

public sealed partial class LiveAreaGraph : GraphControl
{
    private readonly DateTimeOffset _clockOrigin = DateTimeOffset.UtcNow;
    private readonly long _clockTimestamp = Stopwatch.GetTimestamp();
    private readonly TextBlock _tooltipTime = new() { FontSize = 12, FontWeight = FontWeights.SemiBold };
    private readonly SolidColorBrush _tooltipForeground = new();
    private readonly SolidColorBrush _tooltipStroke = new();
    private readonly Border _tooltip;
    private readonly TooltipRow[] _tooltipRows;
    private readonly TimeSpan _history;
    private readonly double _minimum;
    private readonly double _maximum;
    private readonly double _smoothing;
    private readonly string _valueFormat;
    private readonly string _valueSuffix;
    private readonly string _nowText;
    private readonly string _secondsAgoFormat;
    private LineGraphPoint[][] _samples = [];
    private LineGraphPresentation[] _presentations = [];
    private SeriesGeometry[] _geometry = [];
    private float _geometryWidth;
    private float _geometryHeight;
    private DateTimeOffset _geometryTime;
    private double? _inspectionPosition;
    private double _inspectionY = 0.5;
    private bool _keyboardInspection;
    private long _lastInspectionUpdate;
    private string _latestCaption = string.Empty;

    public LiveAreaGraph(GraphSeries[] series, double minimum, double maximum, TimeSpan history, string valueFormat, string valueSuffix, string nowText = "Now", string secondsAgoFormat = "{0:0.0} s ago", double smoothing = 0, TimeSpan? presentationDelay = null)
    {
        if (!double.IsFinite(smoothing) || smoothing < 0 || smoothing > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(smoothing));
        }

        PresentationDelay = presentationDelay ?? LineGraphPresentation.DefaultPresentationDelay;
        if (PresentationDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(presentationDelay));
        }

        ConfigureSeries(series);
        _minimum = minimum;
        _maximum = maximum;
        _smoothing = smoothing;
        _history = history;
        _valueFormat = valueFormat;
        _valueSuffix = valueSuffix;
        _nowText = nowText;
        _secondsAgoFormat = secondsAgoFormat;
        var tooltipContent = new StackPanel { Spacing = 6 };
        _tooltipTime.Foreground = _tooltipForeground;
        tooltipContent.Children.Add(_tooltipTime);
        var values = new StackPanel { Spacing = 4 };
        _tooltipRows = new TooltipRow[series.Length];
        for (var index = 0; index < series.Length; index++)
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var swatch = new Ellipse { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center, Fill = new SolidColorBrush() };
            var name = new TextBlock { Text = series[index].Name, FontSize = 12, Foreground = _tooltipForeground, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            var value = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = _tooltipForeground, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right, MaxWidth = 144, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(name, 1);
            Grid.SetColumn(value, 2);
            row.Children.Add(swatch);
            row.Children.Add(name);
            row.Children.Add(value);
            values.Children.Add(row);
            _tooltipRows[index] = new(row, swatch, name, value);
        }

        tooltipContent.Children.Add(values);
        _tooltip = new Border
        {
            Child = tooltipContent,
            Padding = new Thickness(10, 8, 10, 8),
            Width = 220,
            BorderBrush = _tooltipStroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };
        AutomationProperties.SetAccessibilityView(_tooltip, AccessibilityView.Raw);
        var tooltipLayer = new Canvas { IsHitTestVisible = false };
        tooltipLayer.Children.Add(_tooltip);
        Plot.Children.Add(tooltipLayer);
        _tooltip.SizeChanged += (_, _) => PositionTooltip();
        Plot.SizeChanged += (_, _) => PositionTooltip();
        PointerMoved += InspectPointer;
        PointerExited += (_, _) =>
        {
            if (!_keyboardInspection)
            {
                EndInspection();
            }
        };
        GotFocus += (_, _) =>
        {
            if (FocusState == FocusState.Keyboard)
            {
                _keyboardInspection = true;
                Inspect(1);
            }
        };
        LostFocus += (_, _) =>
        {
            _keyboardInspection = false;
            EndInspection();
        };
        KeyDown += InspectKeyboard;
    }

    /// <summary>
    /// Gets the local interpolation budget. Zero disables buffering.
    /// Intervals that exceed the budget are displayed as steps.
    /// </summary>
    public TimeSpan PresentationDelay { get; }

    protected override bool NeedsContinuousDrawing => _samples.Any(samples => samples.Length > 0);

    private DateTimeOffset Now => _clockOrigin + Stopwatch.GetElapsedTime(_clockTimestamp);

    public void SetSnapshot(LineGraphPoint[][] samples)
    {
        var now = Now;
        var previous = _presentations;
        _samples = new LineGraphPoint[samples.Length][];
        _presentations = new LineGraphPresentation[samples.Length];
        for (var index = 0; index < samples.Length; index++)
        {
            _presentations[index] = new(index < previous.Length ? previous[index] : null, (LineGraphPoint[])samples[index].Clone(), _minimum, _maximum, _smoothing, now, _history, PresentationDelay);
            _samples[index] = _presentations[index].Samples;
        }

        ReleaseDrawingResources();
        _latestCaption = string.Join("   ", Series.Select((series, index) =>
            _samples[index].Length == 0 ? series.Name : $"{series.Name}: {FormatValue(_samples[index][^1].Value)}"));
        SetCaption(_latestCaption);
        UpdateInspection(now);
        BeginAnimation();
    }

    protected override void Draw(CanvasDrawingSession session, CanvasControl canvas, float width, float height)
    {
        var now = Now;
        var displayTime = new DateTimeOffset(Math.Max(0, now.UtcTicks - PresentationDelay.Ticks), TimeSpan.Zero);
        if (_geometry.Length != _samples.Length || width != _geometryWidth || height != _geometryHeight)
        {
            BuildGeometry(canvas, width, height, displayTime);
        }

        using var clip = session.CreateLayer(1, new Rect(0, 0, width, height));
        for (var y = 0f; y < height; y += 32)
        {
            for (var x = 0f; x < width; x += 32)
            {
                session.DrawLine(x - 2, y, x + 2, y, GridColor);
                session.DrawLine(x, y - 2, x, y + 2, GridColor);
            }
        }

        var offset = (float)((displayTime - _geometryTime).TotalSeconds / _history.TotalSeconds * width);
        var transform = session.Transform;
        session.Transform = Matrix3x2.CreateTranslation(-offset, 0) * transform;
        for (var index = 0; index < _geometry.Length; index++)
        {
            var geometry = _geometry[index];
            if (geometry.Fill is null)
            {
                continue;
            }

            var color = SeriesColor(index);
            var fillColor = WithOpacity(color, HighContrast ? 0.12 : 0.2);
            session.FillGeometry(geometry.Fill, fillColor);
            var last = geometry.Last;
            var right = width + offset;
            if (last.X < right)
            {
                var left = Math.Max(last.X, offset);
                session.FillRectangle(left, last.Y, right - left, height - last.Y, fillColor);
            }
        }

        // Draw all strokes after all fills so overlapping series remain legible.
        for (var index = 0; index < _geometry.Length; index++)
        {
            var geometry = _geometry[index];
            if (geometry.Stroke is not null)
            {
                var color = SeriesColor(index);
                session.DrawGeometry(geometry.Stroke, color, 2);
                var last = geometry.Last;
                if (last.X < width + offset)
                {
                    session.DrawLine(Math.Max(last.X, offset), last.Y, width + offset, last.Y, color, 2);
                }
            }
        }

        session.Transform = transform;
        if (_inspectionPosition is { } position)
        {
            DrawInspection(session, now, position, width, height);
            if (Stopwatch.GetElapsedTime(_lastInspectionUpdate).TotalMilliseconds >= 100)
            {
                UpdateInspection(now);
            }
        }
    }

    private void DrawInspection(CanvasDrawingSession session, DateTimeOffset now, double position, float width, float height)
    {
        var time = InspectionTime(now, position);
        var x = (float)(position * width);
        var outline = (ResourceBrush("SolidBackgroundFillColorBaseBrush") as SolidColorBrush)?.Color ?? ForegroundColor;
        var guideDrawn = false;
        for (var series = 0; series < _samples.Length; series++)
        {
            if (_presentations[series].TryGetValue(time, out _, out var normalized))
            {
                if (!guideDrawn)
                {
                    session.DrawLine(x, 0, x, height, ForegroundColor, 1.25f);
                    guideDrawn = true;
                }

                var y = height * (float)(1 - normalized);
                session.FillCircle(x, y, 4.25f, outline);
                session.FillCircle(x, y, 2.75f, SeriesColor(series));
            }
        }
    }

    protected override void ReleaseDrawingResources()
    {
        foreach (var geometry in _geometry)
        {
            geometry?.Dispose();
        }

        _geometry = [];
        _geometryWidth = _geometryHeight = 0;
    }

    private void BuildGeometry(CanvasControl canvas, float width, float height, DateTimeOffset now)
    {
        ReleaseDrawingResources();
        _geometryTime = now;
        _geometryWidth = width;
        _geometryHeight = height;
        _geometry = new SeriesGeometry[_samples.Length];
        for (var index = 0; index < _samples.Length; index++)
        {
            var samples = _samples[index];
            var presentation = _presentations[index];
            if (samples.Length == 0)
            {
                _geometry[index] = new(null, null, default);
                continue;
            }

            using var fill = new CanvasPathBuilder(canvas);
            using var stroke = new CanvasPathBuilder(canvas);
            var first = ToPoint(samples[0], now, width, height);
            fill.BeginFigure(first.X, height);
            fill.AddLine(first);
            stroke.BeginFigure(first);
            var last = first;
            for (var segmentIndex = 0; segmentIndex < presentation.Curve.Segments.Length; segmentIndex++)
            {
                last = AddSegment(fill, stroke, presentation.Curve.Segments[segmentIndex], presentation.IsStep(segmentIndex), last, now, width, height);
            }

            fill.AddLine(last.X, height);
            fill.EndFigure(CanvasFigureLoop.Closed);
            stroke.EndFigure(CanvasFigureLoop.Open);
            _geometry[index] = new(CanvasGeometry.CreatePath(fill), CanvasGeometry.CreatePath(stroke), last);
        }
    }

    private Vector2 AddSegment(CanvasPathBuilder fill, CanvasPathBuilder stroke, LineGraphSegment segment, bool isStep, Vector2 start, DateTimeOffset geometryTime, float width, float height)
    {
        var end = new Vector2(ToX(segment.EndTime, geometryTime, width), height * (float)(1 - segment.NormalizedEnd));
        if (isStep)
        {
            var corner = new Vector2(end.X, start.Y);
            fill.AddLine(corner);
            stroke.AddLine(corner);
            fill.AddLine(end);
            stroke.AddLine(end);
        }
        else if (_smoothing == 0)
        {
            fill.AddLine(end);
            stroke.AddLine(end);
        }
        else
        {
            var step = (end.X - start.X) / 3;
            var control1 = new Vector2(start.X + step, height * (float)(1 - segment.NormalizedControl1));
            var control2 = new Vector2(end.X - step, height * (float)(1 - segment.NormalizedControl2));
            fill.AddCubicBezier(control1, control2, end);
            stroke.AddCubicBezier(control1, control2, end);
        }

        return end;
    }

    private Vector2 ToPoint(LineGraphPoint sample, DateTimeOffset now, float width, float height)
    {
        var x = ToX(sample.Timestamp, now, width);
        var value = Math.Clamp(sample.Value, _minimum, _maximum);
        var y = height * (float)(1 - ((value - _minimum) / (_maximum - _minimum)));
        return new(x, y);
    }

    private float ToX(DateTimeOffset time, DateTimeOffset now, float width)
    {
        return (float)(1 + ((time - now).TotalSeconds / _history.TotalSeconds)) * width;
    }

    private void InspectPointer(object sender, PointerRoutedEventArgs args)
    {
        if (Plot.ActualWidth > 0 && Plot.ActualHeight > 0)
        {
            _keyboardInspection = false;
            var point = args.GetCurrentPoint(Plot).Position;
            if (point.X < 0 || point.X > Plot.ActualWidth || point.Y < 0 || point.Y > Plot.ActualHeight)
            {
                EndInspection();
                return;
            }

            _inspectionY = point.Y / Plot.ActualHeight;
            Inspect(point.X / Plot.ActualWidth);
        }
    }

    private void InspectKeyboard(object sender, KeyRoutedEventArgs args)
    {
        var position = _inspectionPosition ?? 1;
        switch (args.Key)
        {
            case VirtualKey.Left:
                position -= 0.02;
                break;
            case VirtualKey.Right:
                position += 0.02;
                break;
            case VirtualKey.Home:
                position = 0;
                break;
            case VirtualKey.End:
                position = 1;
                break;
            case VirtualKey.Escape:
                EndInspection();
                args.Handled = true;
                return;
            default:
                return;
        }

        _keyboardInspection = true;
        Inspect(position);
        args.Handled = true;
    }

    private void Inspect(double position)
    {
        _inspectionPosition = Math.Clamp(position, 0, 1);
        UpdateInspection(Now);
        Invalidate();
    }

    private void EndInspection()
    {
        _inspectionPosition = null;
        _tooltip.Visibility = Visibility.Collapsed;
        AutomationProperties.SetHelpText(this, _latestCaption);
        Invalidate();
    }

    private void UpdateInspection(DateTimeOffset now)
    {
        if (_inspectionPosition is not { } position)
        {
            return;
        }

        _lastInspectionUpdate = Stopwatch.GetTimestamp();
        var time = InspectionTime(now, position);
        var age = now - time;
        _tooltipTime.Text = age.TotalMilliseconds < 50 ? _nowText : string.Format(CultureInfo.CurrentCulture, _secondsAgoFormat, age.TotalSeconds);
        var description = new StringBuilder(_tooltipTime.Text);
        var hasValues = false;
        for (var series = 0; series < _samples.Length; series++)
        {
            var row = _tooltipRows[series];
            if (!_presentations[series].TryGetValue(time, out var value, out _))
            {
                row.Root.Visibility = Visibility.Collapsed;
                continue;
            }

            hasValues = true;
            row.Root.Visibility = Visibility.Visible;
            row.Value.Text = FormatValue(value);
            row.Name.Opacity = HighContrast ? 1 : 0.78;
            ((SolidColorBrush)row.Swatch.Fill).Color = SeriesColor(series);
            description.AppendLine().Append(Series[series].Name).Append(": ").Append(row.Value.Text);
        }

        _tooltip.Visibility = hasValues ? Visibility.Visible : Visibility.Collapsed;
        _tooltipForeground.Color = ForegroundColor;
        _tooltipStroke.Color = GridColor;
        _tooltip.Background = ResourceBrush("SolidBackgroundFillColorBaseBrush");
        PositionTooltip();
        AutomationProperties.SetHelpText(this, hasValues ? description.ToString() : _latestCaption);
    }

    private DateTimeOffset InspectionTime(DateTimeOffset now, double position)
    {
        var displayTicks = Math.Max(0, now.UtcTicks - PresentationDelay.Ticks);
        var offsetTicks = (long)Math.Min((1 - position) * _history.Ticks, displayTicks);
        return new DateTimeOffset(Math.Max(0, displayTicks - offsetTicks), TimeSpan.Zero);
    }

    private void PositionTooltip()
    {
        if (_inspectionPosition is not { } position || _tooltip.Visibility != Visibility.Visible)
        {
            return;
        }

        var width = Plot.ActualWidth;
        var height = Plot.ActualHeight;
        var tooltipWidth = Math.Min(220, Math.Max(0, width - 16));
        _tooltip.Width = tooltipWidth;
        var x = position * width;
        var left = x + 12 + tooltipWidth <= width - 8 ? x + 12 : x - tooltipWidth - 12;
        var top = (_inspectionY * height) - (_tooltip.ActualHeight / 2);
        Canvas.SetLeft(_tooltip, Math.Clamp(left, 8, Math.Max(8, width - tooltipWidth - 8)));
        Canvas.SetTop(_tooltip, Math.Clamp(top, 8, Math.Max(8, height - _tooltip.ActualHeight - 8)));
    }

    private string FormatValue(double value)
    {
        Span<char> buffer = stackalloc char[128];
        try
        {
            if (value.TryFormat(buffer, out var written, _valueFormat, CultureInfo.CurrentCulture))
            {
                return string.Concat(buffer[..written], _valueSuffix);
            }
        }
        catch (FormatException)
        {
        }

        return value.ToString("0.0", CultureInfo.CurrentCulture) + _valueSuffix;
    }

    private sealed record SeriesGeometry(CanvasGeometry? Fill, CanvasGeometry? Stroke, Vector2 Last)
    {
        public void Dispose()
        {
            Fill?.Dispose();
            Stroke?.Dispose();
        }
    }

    private sealed record TooltipRow(Grid Root, Ellipse Swatch, TextBlock Name, TextBlock Value);
}
