// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Interactivity;
using System.Windows.Media.Animation;

namespace ColorPicker.Behaviors
{
    public class ResizeBehavior : Behavior<FrameworkElement>
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "https://docs.microsoft.com/en-us/dotnet/framework/wpf/advanced/dependency-property-security#:~:text=Dependency%20properties%20should%20generally%20be%20considered%20to%20be,make%20security%20guarantees%20about%20a%20dependency%20property%20value.")]
        public static DependencyProperty WidthProperty = DependencyProperty.Register("Width", typeof(double), typeof(ResizeBehavior), new PropertyMetadata(new PropertyChangedCallback(WidthPropertyChanged)));

        private static void WidthPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = ((ResizeBehavior)d).AssociatedObject;
            var move = new DoubleAnimation(sender.Width, (double)e.NewValue, new Duration(TimeSpan.FromMilliseconds(150)), FillBehavior.Stop);
            move.Completed += (s, e1) =>
            {
                sender.BeginAnimation(FrameworkElement.WidthProperty, null);
                sender.Width = (double)e.NewValue;
            };

            move.EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseOut };
            sender.BeginAnimation(FrameworkElement.WidthProperty, move, HandoffBehavior.Compose);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "https://docs.microsoft.com/en-us/dotnet/framework/wpf/advanced/dependency-property-security#:~:text=Dependency%20properties%20should%20generally%20be%20considered%20to%20be,make%20security%20guarantees%20about%20a%20dependency%20property%20value.")]
        public static DependencyProperty HeightProperty = DependencyProperty.Register("Height", typeof(double), typeof(ResizeBehavior), new PropertyMetadata(new PropertyChangedCallback(HeightPropertyChanged)));

        private static void HeightPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = ((ResizeBehavior)d).AssociatedObject;
            var move = new DoubleAnimation(sender.Height, (double)e.NewValue, new Duration(TimeSpan.FromMilliseconds(150)), FillBehavior.Stop);
            move.Completed += (s, e1) =>
            {
                sender.BeginAnimation(FrameworkElement.HeightProperty, null);
                sender.Height = (double)e.NewValue;
            };

            move.EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseOut };
            sender.BeginAnimation(FrameworkElement.HeightProperty, move, HandoffBehavior.Compose);
        }

        public double Width
        {
            get
            {
                return (double)GetValue(WidthProperty);
            }

            set
            {
                SetValue(WidthProperty, value);
            }
        }

        public double Height
        {
            get
            {
                return (double)GetValue(HeightProperty);
            }

            set
            {
                SetValue(HeightProperty, value);
            }
        }
    }
}
