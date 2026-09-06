// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls.Graphs;

public sealed partial class VerticalUsageBar : GraphControl
{
    private readonly double _minimum;
    private readonly double _maximum;
    private readonly Color? _indicatorColor;
    private double[] _start;
    private double[] _target;
    private double _value;

    public VerticalUsageBar(GraphSeries[] series, double minimum, double maximum, Color? indicatorColor)
    {
        ConfigureSeries(series);
        _minimum = minimum;
        _maximum = maximum;
        _indicatorColor = indicatorColor;
        _start = _target = new double[Math.Max(series.Length, 1)];
        Width = 160;
        Height = 260;
        HorizontalAlignment = HorizontalAlignment.Left;
    }

    public void SetSnapshot(double value, string valueText, double[] contributions)
    {
        _value = value;
        var target = new double[_start.Length];
        var range = _maximum - _minimum;
        if (Series.Length == 0)
        {
            target[0] = (Math.Clamp(value, _minimum, _maximum) - _minimum) / range;
        }
        else
        {
            var remaining = range;
            for (var index = 0; index < contributions.Length; index++)
            {
                var visible = Math.Min(contributions[index], remaining);
                target[index] = visible / range;
                remaining -= visible;
            }
        }

        var caption = valueText;
        if (Series.Length > 0)
        {
            caption += Environment.NewLine + string.Join("   ", Series.Select((series, index) =>
                $"{series.Name}: {contributions[index].ToString("G4", CultureInfo.CurrentCulture)}"));
        }

        SetCaption(caption);
        if (!_target.AsSpan().SequenceEqual(target))
        {
            var progress = Ease(AnimationProgress);
            _start = _target.Select((value, index) => _start[index] + ((value - _start[index]) * progress)).ToArray();
            _target = target;
            BeginAnimation();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new UsageBarAutomationPeer(this);

    protected override void Draw(CanvasDrawingSession session, CanvasControl canvas, float width, float height)
    {
        var top = height;
        var progress = Ease(AnimationProgress);
        for (var index = 0; index < _target.Length; index++)
        {
            var fraction = _start[index] + ((_target[index] - _start[index]) * progress);
            var segmentHeight = (float)fraction * height;
            top -= segmentHeight;
            var color = Series.Length == 0 ? IndicatorColor(_indicatorColor) : SeriesColor(index);
            session.FillRectangle(0, top, width, segmentHeight, WithOpacity(color, HighContrast ? 0.8 : 0.3));
            if (segmentHeight > 0)
            {
                session.DrawLine(0, top, width, top, color, 2);
            }
        }

        for (var y = height; y > 0; y -= 12)
        {
            session.DrawLine(0, y, width, y, GridColor);
        }
    }

    private sealed partial class UsageBarAutomationPeer(VerticalUsageBar owner) : FrameworkElementAutomationPeer(owner), IRangeValueProvider
    {
        public bool IsReadOnly => true;

        public double LargeChange => double.NaN;

        public double SmallChange => double.NaN;

        public double Minimum => owner._minimum;

        public double Maximum => owner._maximum;

        public double Value => Math.Clamp(owner._value, Minimum, Maximum);

        public void SetValue(double value) => throw new InvalidOperationException("This meter is read-only.");

        protected override string GetClassNameCore() => nameof(VerticalUsageBar);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

        protected override object GetPatternCore(PatternInterface patternInterface)
            => patternInterface == PatternInterface.RangeValue ? this : base.GetPatternCore(patternInterface);
    }
}
