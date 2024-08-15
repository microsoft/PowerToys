// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AdvancedPaste.Controls
{
    public partial class AnimatedBorderBrush : XamlCompositionBrushBase
    {
        public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
        nameof(IsLoading),
        typeof(bool),
        typeof(AnimatedBorderBrush),
        new PropertyMetadata(defaultValue: false, OnIsLoadingChanged));

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(
        nameof(Duration),
        typeof(int),
        typeof(AnimatedBorderBrush),
        new PropertyMetadata(defaultValue: 400, OnDurationChanged));

        public int Duration
        {
            get => (int)GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }

        private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs newValue)
        {
            var selectionRectangle = (AnimatedBorderBrush)d;
            selectionRectangle.IsLoadingChanged();
        }

        private static void OnDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs newValue)
        {
            var selectionRectangle = (AnimatedBorderBrush)d;
            selectionRectangle.DurationChanged();
        }

        private Compositor compositor;
        private bool isConnected;
        private double centerWidth;
        private double centerHeight;
        private CompositionAnimationGroup animationGroup;
        private CompositionSurfaceBrush gradientBrush;

        public AnimatedBorderBrush()
        {
            compositor = CompositionTarget.GetCompositorForCurrentThread();
        }

        private void DurationChanged()
        {
            if (!isConnected)
            {
                return;
            }

            if (IsLoading)
            {
                PlayAnimation(true);
            }
            else
            {
                animationGroup = null;
            }
        }

        protected override void OnConnected()
        {
            isConnected = true;
            IsLoadingChanged();
        }

        protected override void OnDisconnected()
        {
            isConnected = false;
            CompositionBrush = null;
            gradientBrush = null;
        }

        private void IsLoadingChanged()
        {
            if (!isConnected)
            {
                return;
            }

            if (IsLoading)
            {
                if (CompositionBrush == null)
                {
                    var brush = compositor.CreateSurfaceBrush();
                    var loadedSurface = LoadedImageSurface.StartLoadFromUri(new Uri("ms-appx:///Assets/AdvancedPaste/Gradient.png"));
                    brush.Surface = loadedSurface;
                    brush.HorizontalAlignmentRatio = 0.5f;
                    brush.VerticalAlignmentRatio = 0.5f;
                    brush.Stretch = CompositionStretch.UniformToFill;
                    brush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.MagLinearMinLinearMipLinear;
                    brush.Scale = new Vector2(1.4f, 1.4f);
                    CompositionBrush = brush;
                    gradientBrush = brush;
                    gradientBrush.CenterPoint = new Vector2((float)centerWidth / 2, (float)centerHeight / 2);
                }

                PlayAnimation(false);
            }
            else
            {
                if (animationGroup != null && gradientBrush != null)
                {
                    gradientBrush.StopAnimationGroup(animationGroup);
                }
            }
        }

        public void UpdateSize(double width, double height)
        {
            centerWidth = width;
            centerHeight = height;

            if (gradientBrush != null)
            {
                gradientBrush.CenterPoint = new Vector2((float)width / 2, (float)height / 2);
            }
        }

        private void PlayAnimation(bool reset)
        {
            if (reset || animationGroup == null)
            {
                InitializeAnimation();
            }

            gradientBrush.StopAnimationGroup(animationGroup);
            gradientBrush.StartAnimationGroup(animationGroup);
        }

        private void InitializeAnimation()
        {
            animationGroup = compositor.CreateAnimationGroup();
            var animation = compositor.CreateScalarKeyFrameAnimation();
            animation.Duration = TimeSpan.FromMilliseconds(Duration);
            animation.IterationBehavior = AnimationIterationBehavior.Forever;
            var easing = compositor.CreateLinearEasingFunction();
            animation.InsertKeyFrame(0, 0, easing);
            animation.InsertKeyFrame(1, 360, easing);
            animation.Target = "RotationAngleInDegrees";
            animationGroup.Add(animation);
        }
    }
}
