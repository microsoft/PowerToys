// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Microsoft.PowerToys.Settings.UI.Helpers;

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name
public class NavigationParams
#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
{
    public string ElementName { get; set; }

    public string ParentElementName { get; set; }

    public NavigationParams(string elementName, string parentElementName = null)
    {
        ElementName = elementName;
        ParentElementName = parentElementName;
    }
}

public abstract partial class NavigatablePage : Page
{
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
                var parentElement = FindElementByName(this, _pendingNavigationParams.ParentElementName);
                if (parentElement is SettingsExpander expander)
                {
                    expander.IsExpanded = true;

                    // Give time for the expander to animate
                    await Task.Delay(200);
                }
            }

            // Then find and navigate to the target element
            var target = FindElementByName(this, _pendingNavigationParams.ElementName);

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
        fadeAnimation.Duration = TimeSpan.FromMilliseconds(400);

        // Apply animation
        dropShadow.StartAnimation("Opacity", fadeAnimation);

        // If it's a control, add a subtle background highlight
        if (target is Control ctrl)
        {
            var originalBackground = ctrl.Background;

            // Create a simple semi-transparent gray brush
            var highlightBrush = new SolidColorBrush();
            var grayColor = Microsoft.UI.Colors.Gray;
            grayColor.A = 25; // Very subtle transparency
            highlightBrush.Color = grayColor;

            // Apply the highlight
            ctrl.Background = highlightBrush;

            // Wait for animation to complete
            await Task.Delay(400);

            // Restore original background
            ctrl.Background = originalBackground;
        }
        else
        {
            // For non-control elements, just wait for the glow animation
            await Task.Delay(400);
        }

        // Clean up the shadow visual
        ElementCompositionPreview.SetElementChildVisual(target, null);
    }

    protected static FrameworkElement FindElementByName(DependencyObject root, string name)
    {
        if (root is FrameworkElement fe)
        {
            if (!string.IsNullOrEmpty(fe.Name) && fe.Name == name)
            {
                return fe;
            }
        }

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var result = FindElementByName(child, name);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
