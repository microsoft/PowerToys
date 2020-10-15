// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Interactivity;
using ColorPicker.Helpers;

namespace ColorPicker.Behaviors
{
    public class CopyToClipboardBehavior : Behavior<TextBlock>
    {
        public static readonly DependencyProperty CopiedIndicationBorderProperty = DependencyProperty.Register("CopiedIndicationBorder", typeof(Border), typeof(CopyToClipboardBehavior));

        public Border CopiedIndicationBorder
        {
            get { return (Border)GetValue(CopiedIndicationBorderProperty); }
            set { SetValue(CopiedIndicationBorderProperty, value); }
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += AssociatedObject_Loaded;
        }

        private void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
        {
            AssociatedObject.PreviewMouseLeftButtonDown += AssociatedObject_PreviewMouseLeftButtonDown;
        }

        private void AssociatedObject_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ClipboardHelper.CopyToClipboard(AssociatedObject.Text);

            // TODO start appear animation and hide after some time
            CopiedIndicationBorder.Visibility = Visibility.Visible;
        }
    }
}
