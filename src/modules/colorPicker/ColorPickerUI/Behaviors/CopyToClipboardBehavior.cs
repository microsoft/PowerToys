// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interactivity;
using System.Windows.Media.Animation;
using ColorPicker.Helpers;

namespace ColorPicker.Behaviors
{
    public class CopyToClipboardBehavior : Behavior<TextBlock>
    {
        public static readonly DependencyProperty CopiedIndicationBorderProperty = DependencyProperty.Register("CopiedIndicationBorder", typeof(Border), typeof(CopyToClipboardBehavior));
        private const int CopyIndicatorStayTimeInMs = 3000;
        private IThrottledActionInvoker _actionInvoker;
        private bool _copyIndicatorVisible;

        public Border CopiedIndicationBorder
        {
            get { return (Border)GetValue(CopiedIndicationBorderProperty); }
            set { SetValue(CopiedIndicationBorderProperty, value); }
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += AssociatedObject_Loaded;
            _actionInvoker = Bootstrapper.Container.GetExportedValue<IThrottledActionInvoker>();
        }

        private void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
        {
            AssociatedObject.PreviewMouseLeftButtonDown += AssociatedObject_PreviewMouseLeftButtonDown;
        }

        private void AssociatedObject_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ClipboardHelper.CopyToClipboard(AssociatedObject.Text);
            if (!_copyIndicatorVisible)
            {
                AppearCopiedIndicator();
            }

            _actionInvoker.ScheduleAction(() => HideCopiedIndicator(), CopyIndicatorStayTimeInMs);
        }

        private void AppearCopiedIndicator()
        {
            _copyIndicatorVisible = true;
            var opacityAppear = new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(300)));
            opacityAppear.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseInOut };
            var resize = new DoubleAnimation(56, new Duration(TimeSpan.FromMilliseconds(300)));

            resize.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseInOut };
            CopiedIndicationBorder.BeginAnimation(Border.OpacityProperty, opacityAppear);
            CopiedIndicationBorder.BeginAnimation(Border.HeightProperty, resize);
        }

        private void HideCopiedIndicator()
        {
            _copyIndicatorVisible = false;
            var opacityDisappear = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(300)));
            opacityDisappear.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseInOut };
            var resize = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(300)));

            resize.EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseInOut };
            CopiedIndicationBorder.BeginAnimation(Border.OpacityProperty, opacityDisappear);
            CopiedIndicationBorder.BeginAnimation(Border.HeightProperty, resize);
        }
    }
}
