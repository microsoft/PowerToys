// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Interactivity;
using System.Windows.Media.Animation;

namespace ColorPicker.Behaviors
{
    public class AppearAnimationBehavior : Behavior<Window>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += AssociatedObject_Loaded;
        }

        private void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
        {
            Appear();
            AssociatedObject.IsVisibleChanged += AssociatedObject_IsVisibleChanged;
        }

        private void AssociatedObject_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (AssociatedObject.IsVisible)
            {
                Appear();
            }
            else
            {
                Hide();
            }
        }

        private void Appear()
        {
            var opacityAppear = new DoubleAnimation(0, 1.0, new Duration(TimeSpan.FromMilliseconds(250)));
            opacityAppear.EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseOut };

            var resize = new DoubleAnimation(0, 180, new Duration(TimeSpan.FromMilliseconds(250)));
            resize.EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseOut };
            AssociatedObject.BeginAnimation(Window.OpacityProperty, opacityAppear);
            AssociatedObject.BeginAnimation(Window.WidthProperty, resize);
        }

        private void Hide()
        {
            var opacityAppear = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(1)));
            var resize = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(1)));
            AssociatedObject.BeginAnimation(Window.OpacityProperty, opacityAppear);
            AssociatedObject.BeginAnimation(Window.WidthProperty, resize);
        }
    }
}
