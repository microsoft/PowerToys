// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using ColorPicker.Shaders;
using Microsoft.Xaml.Behaviors;

namespace ColorPicker.Behaviors
{
    public class GridEffectBehavior : Behavior<FrameworkElement>
    {
        private static double _baseZoomImageSizeInPx = 50;

        public static readonly DependencyProperty EffectProperty = DependencyProperty.Register("Effect", typeof(GridShaderEffect), typeof(GridEffectBehavior));

        public static readonly DependencyProperty ZoomFactorProperty = DependencyProperty.Register("ZoomFactor", typeof(double), typeof(GridEffectBehavior));

        public GridShaderEffect Effect
        {
            get { return (GridShaderEffect)GetValue(EffectProperty); }
            set { SetValue(EffectProperty, value); }
        }

        public double ZoomFactor
        {
            get { return (double)GetValue(ZoomFactorProperty); }
            set { SetValue(ZoomFactorProperty, value); }
        }

        protected override void OnAttached()
        {
            AssociatedObject.Loaded += AssociatedObject_Loaded;
        }

        private void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
        {
            AssociatedObject.MouseMove += AssociatedObject_MouseMove;
        }

        private void AssociatedObject_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var position = e.GetPosition(AssociatedObject);

            var relativeX = position.X / AssociatedObject.ActualWidth;
            var relativeY = position.Y / AssociatedObject.Height;
            Effect.MousePosition = new Point(relativeX, relativeY);

            if (ZoomFactor >= 4)
            {
                Effect.Radius = 0.04;
                Effect.SquareSize = ZoomFactor;
                Effect.TextureSize = _baseZoomImageSizeInPx * ZoomFactor;
            }
            else
            {
                // don't show grid, too small pixels
                Effect.Radius = 0.0;
                Effect.SquareSize = 0;
                Effect.TextureSize = 0;
            }
        }
    }
}
