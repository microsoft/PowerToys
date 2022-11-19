// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using ColorPicker.Helpers;
using ColorPicker.Mouse;
using ManagedCommon;
using Microsoft.Xaml.Behaviors;

namespace ColorPicker.Behaviors
{
    public class ChangeWindowPositionBehavior : Behavior<Window>
    {
        // color window should not get into these zones, only mouse to avoid window getting outsize of monitor
        private const int MonitorRightSideDeadZone = 285;
        private const int MonitorBottomSideDeadZone = 90;
        private const int WindowOffsetWhenInDeadZone = 10;

        private const int YOffset = 10;
        private const int XOffset = 5;

        private Point _lastMousePosition;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += AssociatedObject_Loaded;
        }

        private void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
        {
            var mouseInfoProvider = Bootstrapper.Container.GetExportedValue<IMouseInfoProvider>();

            SetWindowPosition(mouseInfoProvider.CurrentPosition);
            mouseInfoProvider.MousePositionChanged += (s, mousePosition) =>
            {
                SetWindowPosition(mousePosition);
            };

            AssociatedObject.IsVisibleChanged += AssociatedObject_IsVisibleChanged;
        }

        private void AssociatedObject_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                SetWindowPosition(_lastMousePosition);
            }
        }

        private void SetWindowPosition(Point mousePosition)
        {
            _lastMousePosition = mousePosition;
            var dpi = MonitorResolutionHelper.GetCurrentMonitorDpi();
            var mousePositionScaled = new Point(mousePosition.X / dpi.DpiScaleX, mousePosition.Y / dpi.DpiScaleX);

            var monitorBounds = GetBoundsOfMonitorWithMouseIn(mousePosition);

            var windowLeft = mousePositionScaled.X + XOffset;
            var windowTop = mousePositionScaled.Y + YOffset;

            if ((windowLeft + MonitorRightSideDeadZone) > monitorBounds.Right / dpi.DpiScaleX)
            {
                windowLeft -= AssociatedObject.Width + WindowOffsetWhenInDeadZone;
            }

            if ((windowTop + MonitorBottomSideDeadZone) > monitorBounds.Bottom / dpi.DpiScaleX)
            {
                windowTop -= AssociatedObject.Height + WindowOffsetWhenInDeadZone;
            }

            AssociatedObject.Left = windowLeft;
            AssociatedObject.Top = windowTop;
        }

        private static Rect GetBoundsOfMonitorWithMouseIn(Point mousePosition)
        {
            foreach (var monitor in MonitorResolutionHelper.AllMonitors)
            {
                if (monitor.Bounds.Contains(new Point(mousePosition.X, mousePosition.Y)))
                {
                    return monitor.Bounds;
                }
            }

            Logger.LogWarning("Failed to get monitor bounds for mouse position" + mousePosition.X + "," + mousePosition.Y);
            return new Rect(0, 0, 0, 0);
        }
    }
}
