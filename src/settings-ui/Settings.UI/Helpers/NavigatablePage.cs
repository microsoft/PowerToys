﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.PowerToys.Settings.UI.Helpers;

public abstract partial class NavigatablePage : Page
{
    private const int ExpandWaitDuration = 500;
    private const int AnimationDuration = 1000;

    private NavigationParams _pendingNavigationParams;

    public NavigatablePage()
    {
        Loaded += OnPageLoaded;
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // Handle both old string parameter and new NavigationParams
        if (e.Parameter is NavigationParams navParams)
        {
            _pendingNavigationParams = navParams;
        }
        else if (e.Parameter is string elementKey)
        {
            _pendingNavigationParams = new NavigationParams(elementKey);
        }
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        if (_pendingNavigationParams != null && !string.IsNullOrEmpty(_pendingNavigationParams.ElementName))
        {
            // First, expand parent if specified
            if (!string.IsNullOrEmpty(_pendingNavigationParams.ParentElementName))
            {
                var parentElement = FindElementByName(_pendingNavigationParams.ParentElementName);
                if (parentElement is SettingsExpander expander)
                {
                    expander.IsExpanded = true;

                    // Give time for the expander to animate
                    await Task.Delay(ExpandWaitDuration);
                }
            }

            // Then find and navigate to the target element
            var target = FindElementByName(_pendingNavigationParams.ElementName);

            target?.StartBringIntoView(new BringIntoViewOptions
            {
                VerticalOffset = -20,
                AnimationDesired = true,
            });

            await OnTargetElementNavigatedAsync(target, _pendingNavigationParams.ElementName);

            _pendingNavigationParams = null;
        }
    }

    protected virtual async Task OnTargetElementNavigatedAsync(FrameworkElement target, string elementKey)
    {
        if (target == null)
        {
            return;
        }

        // Get the visual and compositor
        var visual = ElementCompositionPreview.GetElementVisual(target);
        var compositor = visual.Compositor;

        // Create a subtle glow effect using drop shadow
        var dropShadow = compositor.CreateDropShadow();
        dropShadow.Color = Microsoft.UI.Colors.Gray;
        dropShadow.BlurRadius = 8f;
        dropShadow.Opacity = 0f;
        dropShadow.Offset = new Vector3(0, 0, 0);

        var spriteVisual = compositor.CreateSpriteVisual();
        spriteVisual.Size = new Vector2((float)target.ActualWidth + 16, (float)target.ActualHeight + 16);
        spriteVisual.Shadow = dropShadow;
        spriteVisual.Offset = new Vector3(-8, -8, 0);

        // Insert the shadow visual behind the target element
        ElementCompositionPreview.SetElementChildVisual(target, spriteVisual);

        // Create a simple fade in/out animation
        var fadeAnimation = compositor.CreateScalarKeyFrameAnimation();
        fadeAnimation.InsertKeyFrame(0f, 0f);
        fadeAnimation.InsertKeyFrame(0.5f, 0.3f);
        fadeAnimation.InsertKeyFrame(1f, 0f);
        fadeAnimation.Duration = TimeSpan.FromMilliseconds(AnimationDuration);

        dropShadow.StartAnimation("Opacity", fadeAnimation);

        if (target is Control ctrl)
        {
            // TODO: ability to adjust brush color and animation from settings.
            var originalBackground = ctrl.Background;

            var highlightBrush = new SolidColorBrush();
            var grayColor = Microsoft.UI.Colors.Gray;
            grayColor.A = 50; // Very subtle transparency
            highlightBrush.Color = grayColor;

            // Apply the highlight
            ctrl.Background = highlightBrush;

            // Wait for animation to complete
            await Task.Delay(AnimationDuration);

            // Restore original background
            ctrl.Background = originalBackground;
        }
        else
        {
            // For non-control elements, just wait for the glow animation
            await Task.Delay(AnimationDuration);
        }

        // Clean up the shadow visual
        ElementCompositionPreview.SetElementChildVisual(target, null);
    }

    protected FrameworkElement FindElementByName(string name)
    {
        var element = this.FindName(name) as FrameworkElement;
        return element;
    }
}
