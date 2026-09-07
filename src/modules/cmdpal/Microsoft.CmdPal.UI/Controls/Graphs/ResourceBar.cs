// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace Microsoft.CmdPal.UI.Controls.Graphs;

public sealed partial class ResourceBar : GraphControl
{
    private readonly string _valueFormat;
    private readonly string _valueSuffix;
    private readonly GraphValueScale[] _valueScales;
    private double[] _start;
    private double[] _target;

    public ResourceBar(GraphSeries[] series, string valueFormat, string valueSuffix, GraphValueScale[]? valueScales)
    {
        ConfigureSeries(series);
        _valueFormat = valueFormat;
        _valueSuffix = valueSuffix;
        _valueScales = valueScales is null ? [] : [.. valueScales];
        _start = _target = new double[series.Length];
        Height = double.NaN;
        Plot.MinHeight = Plot.Height = 28;
    }

    public void SetSnapshot(double[] values)
    {
        var target = GraphValueProportions.Normalize(values);
        SetLegend(Series.Select((series, index) =>
            $"{series.Name}: {GraphValueFormatter.Format(values[index], _valueFormat, _valueSuffix, _valueScales)}").ToArray());

        if (!_target.AsSpan().SequenceEqual(target))
        {
            var progress = Ease(AnimationProgress);
            for (var index = 0; index < _start.Length; index++)
            {
                _start[index] += (_target[index] - _start[index]) * progress;
            }

            _target = target;
            BeginAnimation();
        }
    }

    protected override void Draw(CanvasDrawingSession session, CanvasControl canvas, float width, float height)
    {
        var left = 0d;
        var progress = Ease(AnimationProgress);
        for (var index = 0; index < _target.Length; index++)
        {
            var fraction = _start[index] + ((_target[index] - _start[index]) * progress);
            var right = Math.Min(width, left + (fraction * width));
            if (right > left)
            {
                session.FillRectangle((float)left, 0, (float)(right - left), height, WithOpacity(SeriesColor(index), HighContrast ? 1 : 0.7));
                if (left > 0)
                {
                    session.DrawLine((float)left, 0, (float)left, height, GridColor, LineStrokeWidth);
                }
            }

            left = right;
        }
    }
}
