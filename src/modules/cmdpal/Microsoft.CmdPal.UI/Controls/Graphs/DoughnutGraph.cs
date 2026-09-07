// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls.Graphs;

public sealed partial class DoughnutGraph : GraphControl
{
    private readonly TextBlock _centerValue;
    private readonly TextBlock _centerLabel;
    private double[] _start;
    private double[] _target;

    public DoughnutGraph(GraphSeries[] series)
    {
        ConfigureSeries(series);
        _start = _target = new double[series.Length];
        Width = 240;
        Height = 280;
        HorizontalAlignment = HorizontalAlignment.Left;
        var center = CreateGraphElement<StackPanel>("GraphDoughnutCenterTemplate");
        _centerValue = (TextBlock)center.FindName("CenterValue");
        _centerLabel = (TextBlock)center.FindName("CenterLabel");
        Plot.Children.Add(center);
    }

    public void SetSnapshot(double[] values, string centerValue, string centerLabel)
    {
        var target = GraphValueProportions.Normalize(values);
        _centerValue.Text = centerValue;
        _centerLabel.Text = centerLabel;
        SetLegend(Series.Select((series, index) => $"{series.Name}: {values[index].ToString("G4", CultureInfo.CurrentCulture)}").ToArray());
        if (!_target.AsSpan().SequenceEqual(target))
        {
            var progress = Ease(AnimationProgress);
            _start = _target.Select((value, index) => _start[index] + ((value - _start[index]) * progress)).ToArray();
            _target = target;
            BeginAnimation();
        }
    }

    protected override void Draw(CanvasDrawingSession session, CanvasControl canvas, float width, float height)
    {
        const float strokeWidth = 14;
        var radius = (Math.Min(width, height) - strokeWidth - 4) / 2;
        if (radius <= 0)
        {
            return;
        }

        var center = new Vector2(width / 2, height / 2);
        session.DrawCircle(center, radius, GridColor, strokeWidth);
        var angle = -MathF.PI / 2;
        var progress = Ease(AnimationProgress);
        for (var index = 0; index < _target.Length; index++)
        {
            var value = _start[index] + ((_target[index] - _start[index]) * progress);
            var sweep = (float)(value * Math.PI * 2);
            if (sweep <= 0)
            {
                continue;
            }

            if (sweep >= (MathF.PI * 2) - 0.0001f)
            {
                session.DrawCircle(center, radius, SeriesColor(index), strokeWidth);
            }
            else
            {
                // Keep even small nonzero slices visible, as in WinUI3Graphs.
                var gap = Math.Min(MathF.PI / 60, sweep / 2);
                var start = angle + (gap / 2);
                using var path = new CanvasPathBuilder(canvas);
                path.BeginFigure(center + (new Vector2(MathF.Cos(start), MathF.Sin(start)) * radius));
                path.AddArc(center, radius, radius, start, sweep - gap);
                path.EndFigure(CanvasFigureLoop.Open);
                using var geometry = CanvasGeometry.CreatePath(path);
                session.DrawGeometry(geometry, SeriesColor(index), strokeWidth);
            }

            angle += sweep;
        }
    }
}
