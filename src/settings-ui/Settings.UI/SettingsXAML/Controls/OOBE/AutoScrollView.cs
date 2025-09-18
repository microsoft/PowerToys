// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public enum ScrollDirection
    {
        Left,
        Right,
    }

    public partial class AutoScrollView : RedirectVisualView
    {
        public AutoScrollView()
        {
            RedirectVisualEnabled = false;

            compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;

            propSet = compositor.CreatePropertySet();
            propSet.InsertScalar(nameof(Spacing), (float)Spacing);

            visual1 = compositor.CreateSpriteVisual();
            visual1.Brush = ChildVisualBrush;

            visual2 = compositor.CreateSpriteVisual();
            visual2.Brush = ChildVisualBrush;

            offsetBind1 = compositor.CreateExpressionAnimation("Vector3(visual.Offset.X, visual.Offset.Y, 0)");

            sizeBind = compositor.CreateExpressionAnimation("visual.Size");

            RootVisual.Brush = null;
            RootVisual.Children.InsertAtTop(visual2);
            RootVisual.Children.InsertAtTop(visual1);
            RootVisual.IsPixelSnappingEnabled = true;

            linearEasingFunc = compositor.CreateLinearEasingFunction();

            MeasureChildInBoundingBox = IsPlaying;

            this.Loaded += AutoScrollView_Loaded;
        }

        private Compositor compositor;
        private CompositionPropertySet propSet;
        private SpriteVisual visual1;
        private SpriteVisual visual2;

        private ExpressionAnimation offsetBind1;
        private ExpressionAnimation offsetBind2;
        private ExpressionAnimation sizeBind;

        private LinearEasingFunction linearEasingFunc;
        private ScalarKeyFrameAnimation animation;

        protected override bool ChildVisualBrushOffsetEnabled => false;

        private void AutoScrollView_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateAnimationState();
            UpdateAnimationSpeed();
        }

        protected override void OnAttachVisuals()
        {
            base.OnAttachVisuals();

            if (ChildPresenter != null && LayoutRoot != null)
            {
                var childVisual = ElementCompositionPreview.GetElementVisual(ChildPresenter);
                var rootVisual = ElementCompositionPreview.GetElementVisual(LayoutRoot);

                rootVisual.Clip = compositor.CreateInsetClip();

                offsetBind1.SetReferenceParameter("visual", childVisual);
                sizeBind.SetReferenceParameter("visual", childVisual);

                offsetBind2 = GetOffsetBind2ForDirection();
                offsetBind2.SetReferenceParameter("visual", childVisual);
                offsetBind2.SetReferenceParameter("propSet", propSet);

                visual1.StartAnimation("Size", sizeBind);
                visual2.StartAnimation("Size", sizeBind);

                visual1.StartAnimation("Offset", offsetBind1);
                visual2.StartAnimation("Offset", offsetBind2);

                UpdateAnimationSpeed();
            }
        }

        protected override void OnDetachVisuals()
        {
            base.OnDetachVisuals();

            visual1.StopAnimation("Offset");
            visual1.StopAnimation("Size");
            visual2.StopAnimation("Offset");
            visual2.StopAnimation("Size");

            RootVisual.StopAnimation("Offset.X");

            offsetBind1.ClearAllParameters();
            offsetBind2?.ClearAllParameters();
            sizeBind.ClearAllParameters();
            animation?.ClearAllParameters();
        }

        protected override void OnUpdateSize()
        {
            base.OnUpdateSize();
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateAnimationState();
                UpdateAnimationSpeed();
            });
        }

        private void UpdateAnimationState()
        {
            MeasureChildInBoundingBox = !IsPlaying;

            if (IsLoaded && IsPlaying && ChildPresenter != null && LayoutRoot != null)
            {
                var childWidth = ChildPresenter.ActualWidth;
                var rootWidth = LayoutRoot.ActualWidth - Padding.Left - Padding.Right;

                RedirectVisualEnabled = childWidth > rootWidth;
            }
            else
            {
                RedirectVisualEnabled = false;
            }
        }

        private void UpdateAnimationSpeed()
        {
            if (ChildPresenter == null || LayoutRoot == null)
            {
                return;
            }

            UpdateAnimationExpression();

            var childVisual = ElementCompositionPreview.GetElementVisual(ChildPresenter);
            var rootVisual = ElementCompositionPreview.GetElementVisual(LayoutRoot);

            var progress = 0f;

            if (RedirectVisualAttached)
            {
                var controller = RootVisual.TryGetAnimationController("Offset.X");
                if (controller != null)
                {
                    controller.Pause();
                    progress = controller.Progress;
                }

                RootVisual.StopAnimation("Offset.X");
            }

            animation.SetReferenceParameter("visual", childVisual);
            animation.SetReferenceParameter("visual2", rootVisual);
            animation.SetReferenceParameter("propSet", propSet);
            animation.Duration = TimeSpan.FromSeconds(ChildPresenter.ActualWidth / ScrollingPixelsPerSecond);

            RootVisual.StartAnimation("Offset.X", animation);

            if (progress > 0)
            {
                var controller = RootVisual.TryGetAnimationController("Offset.X");
                if (controller != null)
                {
                    controller.Pause();
                    controller.Progress = progress;
                    controller.Resume();
                }
            }
        }

        private void UpdateAnimationExpression()
        {
            string expression = Direction == ScrollDirection.Left
                ? "-visual.Size.X - propSet.Spacing"
                : "visual.Size.X + propSet.Spacing";

            var newAnimation = compositor.CreateScalarKeyFrameAnimation();
            newAnimation.InsertKeyFrame(0, 0);
            newAnimation.InsertExpressionKeyFrame(1, expression, linearEasingFunc);
            newAnimation.IterationBehavior = AnimationIterationBehavior.Forever;
            newAnimation.SetReferenceParameter("propSet", propSet);

            animation = newAnimation;
        }

        private ExpressionAnimation GetOffsetBind2ForDirection()
        {
            string expression = Direction == ScrollDirection.Left
                ? "Vector3(visual.Offset.X + visual.Size.X + propSet.Spacing, visual.Offset.Y, 0)"
                : "Vector3(visual.Offset.X - visual.Size.X - propSet.Spacing, visual.Offset.Y, 0)";

            var anim = compositor.CreateExpressionAnimation(expression);
            anim.SetReferenceParameter("propSet", propSet);
            return anim;
        }

        public static readonly DependencyProperty SpacingProperty =
            DependencyProperty.Register(nameof(Spacing), typeof(double), typeof(AutoScrollView), new PropertyMetadata(0d, (s, a) =>
            {
                if (s is AutoScrollView sender && !Equals(a.NewValue, a.OldValue))
                {
#pragma warning disable CA1305 // Specify IFormatProvider
                    var value = Convert.ToSingle(a.NewValue);
#pragma warning restore CA1305 // Specify IFormatProvider
                    if (value < 0)
                    {
                        throw new ArgumentException(nameof(Spacing));
                    }

                    sender.propSet.InsertScalar(nameof(Spacing), value);
                }
            }));

        public static readonly DependencyProperty IsPlayingProperty =
            DependencyProperty.Register(nameof(IsPlaying), typeof(bool), typeof(AutoScrollView), new PropertyMetadata(true, (s, a) =>
            {
                if (s is AutoScrollView sender && !Equals(a.NewValue, a.OldValue))
                {
                    sender.UpdateAnimationState();
                    sender.UpdateAnimationSpeed();
                }
            }));

        public static readonly DependencyProperty ScrollingPixelsPerSecondProperty =
            DependencyProperty.Register(nameof(ScrollingPixelsPerSecond), typeof(double), typeof(AutoScrollView), new PropertyMetadata(30d, (s, a) =>
            {
                if (s is AutoScrollView sender && !Equals(a.NewValue, a.OldValue))
                {
#pragma warning disable CA1305 // Specify IFormatProvider
                    var value = Convert.ToSingle(a.NewValue);
#pragma warning restore CA1305 // Specify IFormatProvider
                    if (value <= 0)
                    {
                        throw new ArgumentException(nameof(ScrollingPixelsPerSecond));
                    }

                    sender.UpdateAnimationSpeed();
                }
            }));

        public static readonly DependencyProperty DirectionProperty =
            DependencyProperty.Register(nameof(Direction), typeof(ScrollDirection), typeof(AutoScrollView), new PropertyMetadata(ScrollDirection.Left, (s, a) =>
            {
                if (s is AutoScrollView sender && !Equals(a.NewValue, a.OldValue))
                {
                    sender.UpdateAnimationSpeed();
                }
            }));

        public double Spacing
        {
            get => (double)GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }

        public bool IsPlaying
        {
            get => (bool)GetValue(IsPlayingProperty);
            set => SetValue(IsPlayingProperty, value);
        }

        public double ScrollingPixelsPerSecond
        {
            get => (double)GetValue(ScrollingPixelsPerSecondProperty);
            set => SetValue(ScrollingPixelsPerSecondProperty, value);
        }

        public ScrollDirection Direction
        {
            get => (ScrollDirection)GetValue(DirectionProperty);
            set => SetValue(DirectionProperty, value);
        }
    }
}
