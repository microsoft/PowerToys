// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using ColorPicker.Helpers;
using Microsoft.Xaml.Behaviors;

namespace ColorPicker.Behaviors
{
    public class CloseZoomWindowBehavior : Behavior<Window>
    {
        private ZoomWindowHelper _zoomWindowHelper;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += AssociatedObject_Loaded;
        }

        private void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
        {
            AssociatedObject.PreviewKeyDown += AssociatedObject_PreviewKeyDown;
            AssociatedObject.MouseLeftButtonDown += AssociatedObject_MouseLeftButtonDown;
            _zoomWindowHelper = Bootstrapper.Container.GetExportedValue<ZoomWindowHelper>();
        }

        private void AssociatedObject_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _zoomWindowHelper.CloseZoomWindow();
        }

        private void AssociatedObject_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                _zoomWindowHelper.CloseZoomWindow();
            }
        }
    }
}
