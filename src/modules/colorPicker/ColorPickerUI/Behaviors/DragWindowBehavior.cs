// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using Microsoft.Xaml.Behaviors;

namespace ColorPicker.Behaviors
{
    public class DragWindowBehavior : Behavior<FrameworkElement>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += AssociatedObject_Loaded;
        }

        private void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
        {
            AssociatedObject.MousePrimaryButtonDown += AssociatedObject_MousePrimaryButtonDown;
        }

        private void AssociatedObject_MousePrimaryButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var parentWindow = Window.GetWindow(AssociatedObject);
            if (parentWindow != null)
            {
                parentWindow.DragMove();
            }
        }
    }
}
