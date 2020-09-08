// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Interactivity;
using System.Windows.Media.Animation;

namespace ColorPicker.Behaviors
{
    public class MoveWindowBehavior : Behavior<Window>
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "https://docs.microsoft.com/en-us/dotnet/framework/wpf/advanced/dependency-property-security#:~:text=Dependency%20properties%20should%20generally%20be%20considered%20to%20be,make%20security%20guarantees%20about%20a%20dependency%20property%20value.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2211:Non-constant fields should not be visible", Justification = "https://docs.microsoft.com/en-us/dotnet/framework/wpf/advanced/dependency-property-security#:~:text=Dependency%20properties%20should%20generally%20be%20considered%20to%20be,make%20security%20guarantees%20about%20a%20dependency%20property%20value.")]
        public static DependencyProperty LeftProperty = DependencyProperty.Register("Left", typeof(double), typeof(MoveWindowBehavior), new PropertyMetadata(new PropertyChangedCallback(LeftPropertyChanged)));

        private static void LeftPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = ((MoveWindowBehavior)d).AssociatedObject;
            var move = new DoubleAnimation(sender.Left, (double)e.NewValue, new Duration(TimeSpan.FromMilliseconds(150)), FillBehavior.Stop);
            move.EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseOut };
            sender.BeginAnimation(Window.LeftProperty, move, HandoffBehavior.Compose);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "https://docs.microsoft.com/en-us/dotnet/framework/wpf/advanced/dependency-property-security#:~:text=Dependency%20properties%20should%20generally%20be%20considered%20to%20be,make%20security%20guarantees%20about%20a%20dependency%20property%20value.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2211:Non-constant fields should not be visible", Justification = "https://docs.microsoft.com/en-us/dotnet/framework/wpf/advanced/dependency-property-security#:~:text=Dependency%20properties%20should%20generally%20be%20considered%20to%20be,make%20security%20guarantees%20about%20a%20dependency%20property%20value.")]
        public static DependencyProperty TopProperty = DependencyProperty.Register("Top", typeof(double), typeof(MoveWindowBehavior), new PropertyMetadata(new PropertyChangedCallback(TopPropertyChanged)));

        private static void TopPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = ((MoveWindowBehavior)d).AssociatedObject;
            var move = new DoubleAnimation(sender.Top, (double)e.NewValue, new Duration(TimeSpan.FromMilliseconds(150)), FillBehavior.Stop);
            move.EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseOut };
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
