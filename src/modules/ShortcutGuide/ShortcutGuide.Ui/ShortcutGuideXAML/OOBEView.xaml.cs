// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace ShortcutGuide
{
    public sealed partial class OOBEView : Page
    {
        public OOBEView()
        {
            InitializeComponent();

            /*SizeChanged += (_, _) =>
            {
                HeroImageCompositeTransform.TranslateX = ActualWidth - 1350;
            };*/

            HeroImage.ImageSource = ActualTheme == ElementTheme.Dark
                ? new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/ShortcutGuide/HeroImage-dark.png"))
                : new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/ShortcutGuide/HeroImage.png"));

            ActualThemeChanged += (_, _) =>
            {
                HeroImage.ImageSource = ActualTheme == ElementTheme.Dark
                    ? new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/ShortcutGuide/HeroImage-dark.png"))
                    : new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/ShortcutGuide/HeroImage.png"));
            };

            Loaded += (_, _) => AnimateStackPanelChildren(MainStackPanel);
        }

        /// <summary>
        /// Animates the children of a StackPanel by fading them in and translating them from the left.
        /// </summary>
        /// <param name="panel">The StackPanel to animate.</param>
        private void AnimateStackPanelChildren(StackPanel panel)
        {
            Storyboard storyboard = new();
            double delay = 0.0;
            Duration duration = new(TimeSpan.FromSeconds(0.3));

            foreach (UIElement child in panel.Children)
            {
                if (child is not FrameworkElement childFrameworkElement || string.IsNullOrEmpty(childFrameworkElement.Name))
                {
                    continue;
                }

                if (child.RenderTransform is null or not CompositeTransform)
                {
                    child.RenderTransform = new CompositeTransform { TranslateX = -30 };
                    child.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
                }

                child.Opacity = 0;

                DoubleAnimation opacityAnimation = new()
                {
                    From = 0,
                    To = 1,
                    Duration = duration,
                    BeginTime = TimeSpan.FromSeconds(delay),
                };
                Storyboard.SetTarget(opacityAnimation, childFrameworkElement);
                Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
                storyboard.Children.Add(opacityAnimation);

                DoubleAnimation translateAnimation = new()
                {
                    From = -30,
                    To = 0,
                    Duration = duration,
                    BeginTime = TimeSpan.FromSeconds(delay),
                };
                Storyboard.SetTarget(translateAnimation, childFrameworkElement);
                Storyboard.SetTargetProperty(translateAnimation, "(UIElement.RenderTransform).(CompositeTransform.TranslateX)");
                storyboard.Children.Add(translateAnimation);

                delay += 0.2;
            }

            DoubleAnimation heroImageTranslateAnimation = new()
            {
                From = ActualWidth,
                To = ActualWidth - 1350,
                Duration = duration,
                BeginTime = TimeSpan.FromSeconds(delay),
            };
            Storyboard.SetTarget(heroImageTranslateAnimation, HeroImage);
            Storyboard.SetTargetProperty(heroImageTranslateAnimation, "(ImageBrush.Transform).(CompositeTransform.TranslateX)");
            storyboard.Children.Add(heroImageTranslateAnimation);

            storyboard.Begin();
        }
    }
}
