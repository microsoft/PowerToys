// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls.Graphs;

// Tick values are independent of samples and drawing resources. Only their
// density depends on the measured plot size, in device-independent pixels.
internal static class LineGraphGrid
{
    private const int MaximumIntervals = 64;
    private const double RefinementMargin = 1.2;

    public static TimeSpan ChooseTimeStep(TimeSpan history, double width, double valueSpacing, TimeSpan previous = default)
    {
        if (!IsPositiveFinite(width) || !IsPositiveFinite(valueSpacing) || history <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var requested = history.TotalSeconds / Math.Clamp(width / valueSpacing, 1, MaximumIntervals);
        var seconds = RoundTimeStep(requested, history.TotalSeconds / MaximumIntervals);
        if (previous > TimeSpan.Zero && history.TotalSeconds / previous.TotalSeconds <= MaximumIntervals)
        {
            var previousError = Math.Abs(Math.Log(previous.TotalSeconds / requested));
            var candidateError = Math.Abs(Math.Log(seconds / requested));
            if (previousError <= candidateError + Math.Log(1.1))
            {
                seconds = previous.TotalSeconds;
            }
        }

        var ticks = Math.Ceiling(seconds * TimeSpan.TicksPerSecond);
        return ticks >= long.MaxValue ? TimeSpan.MaxValue : TimeSpan.FromTicks(Math.Max(1, (long)ticks));
    }

    public static double ChooseValueStep(double minimum, double maximum, double height, double divisor = 1, double previous = 0)
    {
        var span = maximum - minimum;
        if (!IsPositiveFinite(height) || !IsPositiveFinite(span) || !IsPositiveFinite(divisor))
        {
            return 0;
        }

        // Keep at least four divisions, even in a short plot. Include 2.5 in
        // the round steps so a 0-100 range can use 25 rather than jumping to 50.
        var maximumStep = Math.Max(double.Epsilon, span / 4);
        var step = ChooseValueStep(span, height, previous, value =>
        {
            var scaled = value / divisor;
            var rounded = RoundValueStep(scaled) * divisor;
            return IsPositiveFinite(rounded) ? rounded : RoundValueStep(value);
        });

        if (step > maximumStep)
        {
            var rounded = RoundValueStep(maximumStep / divisor, roundUp: false) * divisor;
            step = IsPositiveFinite(rounded) ? rounded : RoundValueStep(maximumStep, roundUp: false);
        }

        return step;
    }

    public static double[] GetValueTicks(double minimum, double maximum, double step)
    {
        if (!IsPositiveFinite(step) || !double.IsFinite(minimum) || !double.IsFinite(maximum) || maximum <= minimum)
        {
            return [];
        }

        var first = Math.Ceiling(minimum / step) * step;
        if (!double.IsFinite(first))
        {
            return [];
        }

        var values = new List<double>();
        for (var index = 0; index <= MaximumIntervals; index++)
        {
            var value = first + (index * step);
            if (!double.IsFinite(value) || value > maximum)
            {
                break;
            }

            if (value >= minimum && (values.Count == 0 || value > values[^1]))
            {
                values.Add(value == 0 ? 0 : value);
            }
        }

        return [.. values];
    }

    // Include adjacent ticks so crosses enter and leave smoothly through the
    // plot clip. Integer UTC ticks avoid rounding large absolute timestamps.
    public static TimeTicks GetTimeTicks(DateTimeOffset windowEnd, TimeSpan history, TimeSpan step)
    {
        if (history <= TimeSpan.Zero || step <= TimeSpan.Zero)
        {
            return default;
        }

        var end = windowEnd.UtcTicks;
        var start = end - Math.Min(end, history.Ticks);
        var first = start - (start % step.Ticks);
        var count = Math.Min(MaximumIntervals + 2L, ((end - first) / step.Ticks) + 2);
        count = Math.Min(count, ((DateTimeOffset.MaxValue.Ticks - first) / step.Ticks) + 1);
        return new(first, step.Ticks, (int)count);
    }

    private static double ChooseValueStep(double span, double length, double previous, Func<double, double> round)
    {
        const double spacing = 32;
        var requested = Math.Max(double.Epsilon, span / Math.Clamp(length / spacing, 4, MaximumIntervals));
        var candidate = round(requested);
        if (IsPositiveFinite(previous) && previous <= span && span / previous <= MaximumIntervals)
        {
            var previousSpacing = previous / span * length;
            if (candidate >= previous && previousSpacing >= spacing * 0.9)
            {
                return previous;
            }

            // A finer grid needs extra room before we switch, so resizing near
            // a boundary does not alternate between two intervals every frame.
            if (candidate < previous && candidate / span * length < spacing * RefinementMargin)
            {
                return previous;
            }
        }

        return candidate;
    }

    private static double RoundTimeStep(double seconds, double minimum)
    {
        double unit;
        ReadOnlySpan<double> steps;
        if (seconds < 1)
        {
            unit = Math.Pow(10, Math.Floor(Math.Log10(seconds)));
            steps = [1, 1.5, 2, 2.5, 3, 4, 5, 6, 7.5, 10];
        }
        else if (seconds <= 3600)
        {
            unit = seconds <= 60 ? 1 : 60;
            steps = [1, 1.5, 2, 2.5, 3, 4, 5, 6, 7.5, 10, 12, 15, 20, 30, 45, 60];
        }
        else if (seconds <= 86400)
        {
            unit = 3600;
            steps = [1, 1.5, 2, 2.5, 3, 4, 6, 8, 12, 18, 24];
        }
        else
        {
            unit = Math.Pow(10, Math.Floor(Math.Log10(seconds / 86400))) * 86400;
            steps = [1, 1.5, 2, 2.5, 3, 4, 5, 6, 7.5, 10];
        }

        // Match the horizontal and vertical cell sizes proportionally. The
        // time interval remains round and anchored to fixed timestamps.
        var best = seconds;
        var bestError = double.PositiveInfinity;
        foreach (var step in steps)
        {
            var candidate = step * unit;
            var error = Math.Abs(Math.Log(candidate / seconds));
            if (candidate >= minimum && error < bestError)
            {
                best = candidate;
                bestError = error;
            }
        }

        return best;
    }

    private static double RoundValueStep(double value, bool roundUp = true)
    {
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        if (!IsPositiveFinite(magnitude))
        {
            return value;
        }

        var leading = value / magnitude;
        var factor = roundUp
            ? leading <= 1 ? 1 : leading <= 2 ? 2 : leading <= 2.5 ? 2.5 : leading <= 5 ? 5 : 10
            : leading >= 5 ? 5 : leading >= 2.5 ? 2.5 : leading >= 2 ? 2 : 1;
        var rounded = factor * magnitude;
        return double.IsFinite(rounded) ? rounded : value;
    }

    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0;

    internal readonly record struct TimeTicks(long First, long Step, int Count);
}
