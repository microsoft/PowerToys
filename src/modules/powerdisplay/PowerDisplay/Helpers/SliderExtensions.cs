// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// Provides attached dependency properties and methods for the <see cref="Slider"/> control.
    /// </summary>
    public static class SliderExtensions
    {
        /// <summary>
        /// Attached <see cref="DependencyProperty"/> that, when <c>true</c>, lets the
        /// <see cref="Slider"/> respond to mouse-wheel input by adjusting its
        /// <see cref="RangeBase.Value"/> by <see cref="MouseWheelChangeProperty"/>
        /// per notch. The wheel event is marked handled so an enclosing
        /// <see cref="ScrollViewer"/> does not also scroll.
        /// </summary>
        public static readonly DependencyProperty IsMouseWheelEnabledProperty =
            DependencyProperty.RegisterAttached(
                nameof(GetIsMouseWheelEnabled)[3..],
                typeof(bool),
                typeof(SliderExtensions),
                new PropertyMetadata(false, OnIsMouseWheelEnabledChanged));

        /// <summary>
        /// Attached <see cref="DependencyProperty"/> for the value added to or subtracted
        /// from <see cref="RangeBase.Value"/> per mouse-wheel notch. Defaults to
        /// <see cref="double.NaN"/>, which means "use the slider's own
        /// <see cref="Slider.SmallChange"/>".
        /// </summary>
        public static readonly DependencyProperty MouseWheelChangeProperty =
            DependencyProperty.RegisterAttached(
                nameof(GetMouseWheelChange)[3..],
                typeof(double),
                typeof(SliderExtensions),
                new PropertyMetadata(double.NaN));

        // Per-slider accumulator for raw wheel deltas. Private storage — exposed only
        // via the wheel handler. High-precision mice and touchpads emit deltas well
        // below WHEEL_DELTA (120); without accumulation those sub-notch events would
        // either round away to zero or surface as fractional Slider.Value writes that
        // the int-typed source property silently truncates. Accumulating preserves the
        // notched feel the system volume flyout has.
        private static readonly DependencyProperty WheelAccumulatorProperty =
            DependencyProperty.RegisterAttached(
                "WheelAccumulator",
                typeof(int),
                typeof(SliderExtensions),
                new PropertyMetadata(0));

        /// <summary>
        /// Gets the value of the <see cref="IsMouseWheelEnabledProperty"/> attached property.
        /// </summary>
        /// <param name="obj">The <see cref="Slider"/> to read the value from.</param>
        /// <returns><c>true</c> if mouse-wheel scrolling is enabled on the slider; otherwise <c>false</c>.</returns>
        public static bool GetIsMouseWheelEnabled(Slider obj)
        {
            return (bool)obj.GetValue(IsMouseWheelEnabledProperty);
        }

        /// <summary>
        /// Sets the value of the <see cref="IsMouseWheelEnabledProperty"/> attached property.
        /// </summary>
        /// <param name="obj">The <see cref="Slider"/> to set the value on.</param>
        /// <param name="value"><c>true</c> to enable mouse-wheel scrolling on the slider; otherwise <c>false</c>.</param>
        public static void SetIsMouseWheelEnabled(Slider obj, bool value)
        {
            obj.SetValue(IsMouseWheelEnabledProperty, value);
        }

        /// <summary>
        /// Gets the value of the <see cref="MouseWheelChangeProperty"/> attached property.
        /// </summary>
        /// <param name="obj">The <see cref="Slider"/> to read the value from.</param>
        /// <returns>The per-notch delta, or <see cref="double.NaN"/> to inherit from <see cref="Slider.SmallChange"/>.</returns>
        public static double GetMouseWheelChange(Slider obj)
        {
            return (double)obj.GetValue(MouseWheelChangeProperty);
        }

        /// <summary>
        /// Sets the value of the <see cref="MouseWheelChangeProperty"/> attached property.
        /// </summary>
        /// <param name="obj">The <see cref="Slider"/> to set the value on.</param>
        /// <param name="value">The per-notch delta to apply to <see cref="RangeBase.Value"/>.</param>
        public static void SetMouseWheelChange(Slider obj, double value)
        {
            obj.SetValue(MouseWheelChangeProperty, value);
        }

        private static void OnIsMouseWheelEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Slider slider)
            {
                return;
            }

            slider.PointerWheelChanged -= OnPointerWheelChanged;

            if (e.NewValue is true)
            {
                slider.PointerWheelChanged += OnPointerWheelChanged;
            }
        }

        private static void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not Slider slider)
            {
                return;
            }

            int delta = e.GetCurrentPoint(slider).Properties.MouseWheelDelta;
            if (delta == 0)
            {
                return;
            }

            // Claim the gesture so an enclosing ScrollViewer does not also scroll —
            // even sub-threshold events must be swallowed, otherwise a precision
            // touchpad would scroll the flyout while the pointer is on a slider.
            e.Handled = true;

            double step = GetMouseWheelChange(slider);
            if (double.IsNaN(step) || step <= 0)
            {
                step = slider.SmallChange;
            }

            // Integer truncation toward zero, so the remainder keeps its sign.
            int accumulator = (int)slider.GetValue(WheelAccumulatorProperty) + delta;
            int notches = accumulator / WheelDeltaThreshold;
            slider.SetValue(WheelAccumulatorProperty, accumulator - (notches * WheelDeltaThreshold));

            if (notches == 0)
            {
                return;
            }

            double newValue = Math.Clamp(slider.Value + (notches * step), slider.Minimum, slider.Maximum);
            if (newValue != slider.Value)
            {
                slider.Value = newValue;
            }
        }

        // Standard mouse-wheel notch size; high-precision wheels and touchpads emit
        // smaller deltas that must be summed before they advance one step.
        private const int WheelDeltaThreshold = 120;
    }
}
