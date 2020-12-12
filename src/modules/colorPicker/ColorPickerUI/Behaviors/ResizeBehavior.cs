// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.Xaml.Behaviors;

namespace ColorPicker.Behaviors
{
    public class ResizeBehavior : Behavior<FrameworkElement>
    {
        public static readonly DependencyProperty WidthProperty = DependencyProperty.Register("Width", typeof(double), typeof(ResizeBehavior), new PropertyMetadata(new PropertyChangedCallback(WidthPropertyChanged)));

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

        public static readonly DependencyProperty HeightProperty = DependencyProperty.Register("Height", typeof(double), typeof(ResizeBehavior), new PropertyMetadata(new PropertyChangedCallback(HeightPropertyChanged)));

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
