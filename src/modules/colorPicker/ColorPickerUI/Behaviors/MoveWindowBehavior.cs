// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.Xaml.Behaviors;

namespace ColorPicker.Behaviors
{
    public class MoveWindowBehavior : Behavior<Window>
    {
        public static readonly DependencyProperty LeftProperty = DependencyProperty.Register("Left", typeof(double), typeof(MoveWindowBehavior), new PropertyMetadata(new PropertyChangedCallback(LeftPropertyChanged)));

        private static void LeftPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = ((MoveWindowBehavior)d).AssociatedObject;
            var move = new DoubleAnimation(sender.Left, (double)e.NewValue, new Duration(TimeSpan.FromMilliseconds(150)), FillBehavior.Stop);
            move.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseInOut };
            sender.BeginAnimation(Window.LeftProperty, move, HandoffBehavior.Compose);
        }

        public static readonly DependencyProperty TopProperty = DependencyProperty.Register("Top", typeof(double), typeof(MoveWindowBehavior), new PropertyMetadata(new PropertyChangedCallback(TopPropertyChanged)));

        private static void TopPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = ((MoveWindowBehavior)d).AssociatedObject;
            var move = new DoubleAnimation(sender.Top, (double)e.NewValue, new Duration(TimeSpan.FromMilliseconds(150)), FillBehavior.Stop);
            move.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseInOut };
            sender.BeginAnimation(Window.TopProperty, move, HandoffBehavior.Compose);
        }

        public double Left
        {
            get
            {
                return (double)GetValue(LeftProperty);
            }

            set
            {
                SetValue(LeftProperty, value);
            }
        }

        public double Top
        {
            get
            {
                return (double)GetValue(TopProperty);
            }

            set
            {
                SetValue(TopProperty, value);
            }
        }
    }
}
