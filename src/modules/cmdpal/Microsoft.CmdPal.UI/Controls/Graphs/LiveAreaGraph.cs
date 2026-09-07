// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;

namespace Microsoft.CmdPal.UI.Controls.Graphs;

public sealed partial class LiveAreaGraph : GraphControl
{
    private const float GridCrossRadius = 2;

    private static readonly CompositeFormat _historyFormat = CompositeFormat.Parse(ResourceLoaderInstance.GetString("GraphAxis_History"));
    private static readonly CompositeFormat[] _durationFormats =
    [
        CompositeFormat.Parse(ResourceLoaderInstance.GetString("GraphAxis_Seconds")),
        CompositeFormat.Parse(ResourceLoaderInstance.GetString("GraphAxis_Minutes")),
        CompositeFormat.Parse(ResourceLoaderInstance.GetString("GraphAxis_Hours")),
        CompositeFormat.Parse(ResourceLoaderInstance.GetString("GraphAxis_Days")),
    ];

    private readonly DateTimeOffset _clockOrigin = DateTimeOffset.UtcNow;
    private readonly long _clockTimestamp = Stopwatch.GetTimestamp();
    private readonly GraphTooltip _tooltip = new();
    private readonly GraphTooltipItem[] _tooltipItems;
    private readonly TimeSpan _history;
    private readonly double _minimum;
    private readonly double _maximum;
    private readonly bool _autoScaleMaximum;
    private readonly bool[] _readoutOnly;
    private readonly GraphValueScale[] _valueScales;
    private readonly double[] _valueDivisors;
    private readonly double _smoothing;
    private readonly string _valueFormat;
    private readonly string _valueSuffix;
    private readonly string _nowText;
    private readonly string _secondsAgoFormat;
    private readonly string _historyText;
    private LineGraphPoint[][] _samples = [];
    private LineGraphPresentation[] _presentations = [];
    private SeriesGeometry[] _geometry = [];
    private CanvasStrokeStyle? _dashedStrokeStyle;
    private CanvasStrokeStyle? _dottedStrokeStyle;
    private double _visibleMaximum;
    private double[] _valueTicks = [];
    private TimeSpan _timeStep;
    private double _valueStep;
    private double _gridDivisor = 1;
    private double _gridMaximum = double.NaN;
    private float _gridWidth;
    private float _gridHeight;
    private float _geometryWidth;
    private float _geometryHeight;
    private DateTimeOffset _geometryTime;
    private double? _inspectionPosition;
    private double _inspectionY = 0.5;
    private bool _keyboardInspection;
    private long _lastInspectionUpdate;
    private string _latestCaption = string.Empty;

    public LiveAreaGraph(GraphSeries[] series, double minimum, double maximum, TimeSpan history, string valueFormat, string valueSuffix, string nowText = "Now", string secondsAgoFormat = "{0:0.0} s ago", double smoothing = 0, TimeSpan? presentationDelay = null, bool autoScaleMaximum = false, GraphValueScale[]? valueScales = null)
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

        ConfigureSeries(series, useLineSwatches: true);
        _minimum = minimum;
        _maximum = _visibleMaximum = maximum;
        _autoScaleMaximum = autoScaleMaximum;
        _readoutOnly = series.Select(series => series.IsReadoutOnly).ToArray();
        _valueScales = valueScales is null ? [] : [.. valueScales];
        _valueDivisors = _valueScales.Select(scale => scale.Divisor).ToArray();
        _smoothing = smoothing;
        _history = history;
        _valueFormat = valueFormat;
        _valueSuffix = valueSuffix;
        _nowText = nowText;
        _secondsAgoFormat = secondsAgoFormat;
        _historyText = FormatHistory(history);
        if (_autoScaleMaximum)
        {
            SetScaleMaximumLabel(FormatValue(_visibleMaximum));
        }

        _tooltipItems = new GraphTooltipItem[series.Length];
        Plot.Children.Add(_tooltip);
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
        _latestCaption = SetLegend(
            Series.Select((series, index) =>
                _samples[index].Length == 0 ? series.Name : $"{series.Name}: {FormatValue(_samples[index][^1].Value, index)}").ToArray(),
            _historyText);
        UpdateInspection(now);
        BeginAnimation();
    }

    protected override void Draw(CanvasDrawingSession session, CanvasControl canvas, float width, float height)
    {
        var now = Now;
        var displayTime = new DateTimeOffset(Math.Max(0, now.UtcTicks - PresentationDelay.Ticks), TimeSpan.Zero);
        UpdateScale(displayTime);
        UpdateGrid(width, height);
        if (_geometry.Length != _samples.Length || width != _geometryWidth || height != _geometryHeight)
        {
            BuildGeometry(canvas, width, height, displayTime);
        }

        using var clip = session.CreateLayer(1, new Rect(0, 0, width, height));

        var timeTicks = LineGraphGrid.GetTimeTicks(displayTime, _history, _timeStep);
        for (var index = 0; index < timeTicks.Count; index++)
        {
            var time = new DateTimeOffset(timeTicks.First + (index * timeTicks.Step), TimeSpan.Zero);
            var x = ToX(time, displayTime, width);
            foreach (var value in _valueTicks)
            {
                var y = height * (float)(1 - ((value - _minimum) / (_visibleMaximum - _minimum)));
                session.DrawLine(x - GridCrossRadius, y, x + GridCrossRadius, y, GridColor);
                session.DrawLine(x, y - GridCrossRadius, x, y + GridCrossRadius, GridColor);
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
                var strokeStyle = Series[index].LineStyle switch
                {
                    GraphStrokeStyle.Dashed => _dashedStrokeStyle ??= new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash },
                    GraphStrokeStyle.Dotted => _dottedStrokeStyle ??= new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dot, DashCap = CanvasCapStyle.Round },
                    _ => null,
                };
                session.DrawGeometry(geometry.Stroke, color, LineStrokeWidth, strokeStyle);
                var last = geometry.Last;
                if (last.X < width + offset)
                {
                    session.DrawLine(Math.Max(last.X, offset), last.Y, width + offset, last.Y, color, LineStrokeWidth, strokeStyle);
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

    private void UpdateGrid(float width, float height)
    {
        if (_gridWidth == width && _gridHeight == height && _gridMaximum == _visibleMaximum)
        {
            return;
        }

        // Round value intervals in the same display unit as the visible range.
        var divisor = 1d;
        if (_valueScales.Length > 0)
        {
            var magnitude = Math.Max(Math.Abs(_minimum), Math.Abs(_visibleMaximum));
            var scaleIndex = 0;
            while (scaleIndex < _valueScales.Length - 1 && magnitude >= _valueScales[scaleIndex + 1].Divisor)
            {
                scaleIndex++;
            }

            divisor = _valueScales[scaleIndex].Divisor;
        }

        var valueStep = LineGraphGrid.ChooseValueStep(_minimum, _visibleMaximum, height, divisor, _gridDivisor == divisor ? _valueStep : 0);
        if (valueStep != _valueStep || _gridMaximum != _visibleMaximum || _gridDivisor != divisor)
        {
            _valueStep = valueStep;
            _gridDivisor = divisor;
            _valueTicks = LineGraphGrid.GetValueTicks(_minimum, _visibleMaximum, valueStep);
        }

        var valueSpacing = valueStep / (_visibleMaximum - _minimum) * height;
        _timeStep = LineGraphGrid.ChooseTimeStep(_history, width, valueSpacing, _timeStep);
        _gridWidth = width;
        _gridHeight = height;
        _gridMaximum = _visibleMaximum;
    }

    private void DrawInspection(CanvasDrawingSession session, DateTimeOffset now, double position, float width, float height)
    {
        var time = InspectionTime(now, position);
        var x = (float)(position * width);
        var outline = _tooltip.FallbackColor ?? ForegroundColor;
        var guideDrawn = false;
        for (var series = 0; series < _samples.Length; series++)
        {
            if (!_readoutOnly[series] && _presentations[series].TryGetValue(time, out var value, out var normalized))
            {
                if (!guideDrawn)
                {
                    session.DrawLine(x, 0, x, height, ForegroundColor, 1.25f);
                    guideDrawn = true;
                }

                var y = ToY(value, normalized, height);
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
        _dashedStrokeStyle?.Dispose();
        _dashedStrokeStyle = null;
        _dottedStrokeStyle?.Dispose();
        _dottedStrokeStyle = null;
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
            if (_readoutOnly[index] || samples.Length == 0)
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
        var end = new Vector2(ToX(segment.EndTime, geometryTime, width), ToY(segment.End, segment.NormalizedEnd, height));
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
            var control1 = new Vector2(start.X + step, ToY(segment.Control1, segment.NormalizedControl1, height));
            var control2 = new Vector2(end.X - step, ToY(segment.Control2, segment.NormalizedControl2, height));
            fill.AddCubicBezier(control1, control2, end);
            stroke.AddCubicBezier(control1, control2, end);
        }

        return end;
    }

    private Vector2 ToPoint(LineGraphPoint sample, DateTimeOffset now, float width, float height)
    {
        var x = ToX(sample.Timestamp, now, width);
        var value = Math.Clamp(sample.Value, _minimum, _maximum);
        var y = ToY(sample.Value, (value - _minimum) / (_maximum - _minimum), height);
        return new(x, y);
    }

    private void UpdateScale(DateTimeOffset displayTime)
    {
        if (!_autoScaleMaximum)
        {
            return;
        }

        var maximum = LineGraphAutoScale.GetMaximum(_presentations, displayTime, _history, _minimum, _maximum, _valueDivisors, _readoutOnly);
        if (maximum != _visibleMaximum)
        {
            _visibleMaximum = maximum;
            SetScaleMaximumLabel(FormatValue(maximum));
            ReleaseDrawingResources();
        }
    }

    private float ToY(double value, double normalized, float height)
    {
        if (_autoScaleMaximum)
        {
            // Keep offscreen endpoints unclamped to the visible range so a
            // boundary-crossing curve keeps its true shape after rescaling.
            // Bound extreme offscreen coordinates before conversion to float.
            normalized = Math.Clamp((value - _minimum) / (_visibleMaximum - _minimum), -1_000_000, 1_000_000);
        }

        return height * (float)(1 - normalized);
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
        _tooltip.Hide();
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
        var heading = age.TotalMilliseconds < 50 ? _nowText : string.Format(CultureInfo.CurrentCulture, _secondsAgoFormat, age.TotalSeconds);
        for (var series = 0; series < _tooltipItems.Length; series++)
        {
            var text = series < _presentations.Length && _presentations[series].TryGetValue(time, out var value, out _)
                ? FormatValue(value, series)
                : null;
            _tooltipItems[series] = new(Series[series].Name, text, _readoutOnly[series] ? null : SeriesColor(series));
        }

        var description = _tooltip.Show(new Point(position, _inspectionY), heading, _tooltipItems);
        AutomationProperties.SetHelpText(this, description ?? _latestCaption);
    }

    private DateTimeOffset InspectionTime(DateTimeOffset now, double position)
    {
        var displayTicks = Math.Max(0, now.UtcTicks - PresentationDelay.Ticks);
        var offsetTicks = (long)Math.Min((1 - position) * _history.Ticks, displayTicks);
        return new DateTimeOffset(Math.Max(0, displayTicks - offsetTicks), TimeSpan.Zero);
    }

    private string FormatValue(double value) => GraphValueFormatter.Format(value, _valueFormat, _valueSuffix, _valueScales);

    private static string FormatHistory(TimeSpan duration)
    {
        var (value, format) = duration.TotalSeconds < 60 ? (duration.TotalSeconds, 0)
            : duration.TotalMinutes < 60 ? (duration.TotalMinutes, 1)
            : duration.TotalHours < 24 ? (duration.TotalHours, 2)
            : (duration.TotalDays, 3);
        var formatted = string.Format(CultureInfo.CurrentCulture, _durationFormats[format], value);
        return string.Format(CultureInfo.CurrentCulture, _historyFormat, formatted);
    }

    private string FormatValue(double value, int seriesIndex)
        => _readoutOnly[seriesIndex]
            ? GraphValueFormatter.Format(value, _valueFormat, Series[seriesIndex].ReadoutValueSuffix)
            : FormatValue(value);

    private sealed record SeriesGeometry(CanvasGeometry? Fill, CanvasGeometry? Stroke, Vector2 Last)
    {
        public void Dispose()
        {
            Fill?.Dispose();
            Stroke?.Dispose();
        }
    }
}
