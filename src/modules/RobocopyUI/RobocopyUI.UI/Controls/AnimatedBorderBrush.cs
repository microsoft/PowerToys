// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace RobocopyUI.Controls
{
    /// <summary>
    /// Brush that slowly rotates a gradient image, used as the animated border of
    /// <see cref="AnimatedContentControl"/> while a request is in flight. Ported from Advanced
    /// Paste's control of the same name so both modules share one prompt visual language.
    /// </summary>
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
            => ((AnimatedBorderBrush)d).IsLoadingChanged();

        private static void OnDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs newValue)
            => ((AnimatedBorderBrush)d).DurationChanged();

        private readonly Compositor _compositor;
        private bool _isConnected;
        private double _centerWidth;
        private double _centerHeight;
        private CompositionAnimationGroup? _animationGroup;
        private CompositionSurfaceBrush? _gradientBrush;

        public AnimatedBorderBrush()
        {
            _compositor = CompositionTarget.GetCompositorForCurrentThread();
        }

        protected override void OnConnected()
        {
            _isConnected = true;
            IsLoadingChanged();
        }

        protected override void OnDisconnected()
        {
            _isConnected = false;
            CompositionBrush = null;
            _gradientBrush = null;
        }

        public void UpdateSize(double width, double height)
        {
            _centerWidth = width;
            _centerHeight = height;

            if (_gradientBrush is not null)
            {
                _gradientBrush.CenterPoint = new Vector2((float)width / 2, (float)height / 2);
            }
        }

        private void DurationChanged()
        {
            if (!_isConnected)
            {
                return;
            }

            if (IsLoading)
            {
                PlayAnimation(reset: true);
            }
            else
            {
                _animationGroup = null;
            }
        }

        private void IsLoadingChanged()
        {
            if (!_isConnected)
            {
                return;
            }

            if (!IsLoading)
            {
                if (_animationGroup is not null && _gradientBrush is not null)
                {
                    _gradientBrush.StopAnimationGroup(_animationGroup);
                }

                return;
            }

            if (CompositionBrush is null)
            {
                var brush = _compositor.CreateSurfaceBrush();
                brush.Surface = LoadedImageSurface.StartLoadFromUri(new Uri("ms-appx:///Assets/RobocopyUI/Gradient.png"));
                brush.HorizontalAlignmentRatio = 0.5f;
                brush.VerticalAlignmentRatio = 0.5f;
                brush.Stretch = CompositionStretch.UniformToFill;
                brush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.MagLinearMinLinearMipLinear;

                // Oversize the image slightly so no transparent corner rotates into view.
                brush.Scale = new Vector2(1.4f, 1.4f);

                CompositionBrush = brush;
                _gradientBrush = brush;
                _gradientBrush.CenterPoint = new Vector2((float)_centerWidth / 2, (float)_centerHeight / 2);
            }

            PlayAnimation(reset: false);
        }

        private void PlayAnimation(bool reset)
        {
            if (_gradientBrush is null)
            {
                return;
            }

            if (reset || _animationGroup is null)
            {
                InitializeAnimation();
            }

            _gradientBrush.StopAnimationGroup(_animationGroup);
            _gradientBrush.StartAnimationGroup(_animationGroup);
        }

        private void InitializeAnimation()
        {
            _animationGroup = _compositor.CreateAnimationGroup();

            var animation = _compositor.CreateScalarKeyFrameAnimation();
            animation.Duration = TimeSpan.FromMilliseconds(Duration);
            animation.IterationBehavior = AnimationIterationBehavior.Forever;

            var easing = _compositor.CreateLinearEasingFunction();
            animation.InsertKeyFrame(0, 0, easing);
            animation.InsertKeyFrame(1, 360, easing);
            animation.Target = "RotationAngleInDegrees";

            _animationGroup.Add(animation);
        }
    }
}
