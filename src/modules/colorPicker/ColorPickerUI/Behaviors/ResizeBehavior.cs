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
        // animation behavior variables
        // used when size is getting bigger
        private static readonly TimeSpan _animationTime = TimeSpan.FromMilliseconds(200);
        private static readonly IEasingFunction _easeFunction = new SineEase() { EasingMode = EasingMode.EaseOut };

        // used when size is getting smaller
        private static readonly TimeSpan _animationTimeSmaller = _animationTime;
        private static readonly IEasingFunction _easeFunctionSmaller = new QuadraticEase() { EasingMode = EasingMode.EaseIn };

        private static void CustomAnimation(DependencyProperty prop, FrameworkElement sender, double fromValue, double toValue)
        {
            // if the animation is to/from a value of 0, it will cancel the current animation
            DoubleAnimation move = null;
            if (toValue > 0 && fromValue > 0)
            {
                // if getting bigger
                if (fromValue < toValue)
                {
                    move = new DoubleAnimation(fromValue, toValue, new Duration(_animationTime), FillBehavior.Stop)
                    {
                        EasingFunction = _easeFunction,
                    };
                }
                else
                {
                    move = new DoubleAnimation(fromValue, toValue, new Duration(_animationTimeSmaller), FillBehavior.Stop)
                    {
                        EasingFunction = _easeFunctionSmaller,
                    };
                }
            }

            // HandoffBehavior must be SnapshotAndReplace
            // Compose does not allow cancellation
            sender.BeginAnimation(prop, move, HandoffBehavior.SnapshotAndReplace);
        }

        public static readonly DependencyProperty WidthProperty = DependencyProperty.Register("Width", typeof(double), typeof(ResizeBehavior), new PropertyMetadata(new PropertyChangedCallback(WidthPropertyChanged)));

        private static void WidthPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = ((ResizeBehavior)d).AssociatedObject;

            var fromValue = sender.Width;
            var toValue = (double)e.NewValue;

            // setting Width before animation prevents jumping
            sender.Width = toValue;
            CustomAnimation(FrameworkElement.WidthProperty, sender, fromValue, toValue);
        }

        public static readonly DependencyProperty HeightProperty = DependencyProperty.Register("Height", typeof(double), typeof(ResizeBehavior), new PropertyMetadata(new PropertyChangedCallback(HeightPropertyChanged)));

        private static void HeightPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = ((ResizeBehavior)d).AssociatedObject;

            var fromValue = sender.Height;
            var toValue = (double)e.NewValue;

            // setting Height before animation prevents jumping
            sender.Height = toValue;
            CustomAnimation(FrameworkElement.HeightProperty, sender, fromValue, toValue);
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
