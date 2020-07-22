using ColorPicker.Helpers;
using System.Windows;
using System.Windows.Interactivity;

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
