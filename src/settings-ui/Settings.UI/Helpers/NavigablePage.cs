// Copyright (c) Microsoft Corporation
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
using Windows.UI;

namespace Microsoft.PowerToys.Settings.UI.Helpers;

public abstract partial class NavigablePage : Page
{
    private const int ExpandWaitDuration = 500;
    private const int AnimationDuration = 1850;

    private NavigationParams _pendingNavigationParams;

    public NavigablePage()
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

        // Attempt to set keyboard focus so that screen readers announce the element and keyboard users land directly on it.
        TrySetFocus(target);

        // Get the visual and compositor
        var visual = ElementCompositionPreview.GetElementVisual(target);
        var compositor = visual.Compositor;

        // Create a subtle glow effect using drop shadow
        var dropShadow = compositor.CreateDropShadow();
        dropShadow.Color = (Color)Application.Current.Resources["SystemAccentColorLight2"];
        dropShadow.BlurRadius = 16f;
        dropShadow.Opacity = 0f;
        dropShadow.Offset = new Vector3(0, 0, 0);

        var spriteVisual = compositor.CreateSpriteVisual();
        spriteVisual.Size = new Vector2((float)target.ActualWidth + 8, (float)target.ActualHeight + 8);
        spriteVisual.Shadow = dropShadow;
        spriteVisual.Offset = new Vector3(-4, -4, 0);

        // Insert the shadow visual behind the target element
        ElementCompositionPreview.SetElementChildVisual(target, spriteVisual);

        // Create a simple fade in/out animation
        var fadeAnimation = compositor.CreateScalarKeyFrameAnimation();
        fadeAnimation.InsertKeyFrame(0f, 0f);
        fadeAnimation.InsertKeyFrame(0.5f, 0.3f);
        fadeAnimation.InsertKeyFrame(1f, 0f);
        fadeAnimation.Duration = TimeSpan.FromMilliseconds(AnimationDuration);

        dropShadow.StartAnimation("Opacity", fadeAnimation);
        await Task.Delay(AnimationDuration);

        // Clean up the shadow visual
        ElementCompositionPreview.SetElementChildVisual(target, null);
    }

    private static void TrySetFocus(FrameworkElement target)
    {
        try
        {
            // Prefer Control.Focus when available.
            if (target is Control ctrl)
            {
                // Ensure it can receive focus.
                if (!ctrl.IsTabStop)
                {
                    ctrl.IsTabStop = true;
                }

                ctrl.Focus(FocusState.Programmatic);
            }

            // Target is not a Control. Find first focusable descendant Control.
            var focusCandidate = FindFirstFocusableDescendant(target);
            if (focusCandidate != null)
            {
                if (!focusCandidate.IsTabStop)
                {
                    focusCandidate.IsTabStop = true;
                }

                focusCandidate.Focus(FocusState.Programmatic);
                return;
            }

            // Fallback: attempt to focus parent control if no descendant found.
            if (target.Parent is Control parent)
            {
                if (!parent.IsTabStop)
                {
                    parent.IsTabStop = true;
                }

                parent.Focus(FocusState.Programmatic);
            }
        }
        catch
        {
            // Swallow focus exceptions; not critical. Could log if logging enabled.
            // Leave the default focus as it is.
        }
    }

    private static Control FindFirstFocusableDescendant(FrameworkElement root)
    {
        if (root == null)
        {
            return null;
        }

        var queue = new System.Collections.Generic.Queue<DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is Control c && c.IsEnabled && c.Visibility == Visibility.Visible)
            {
                return c;
            }

            int count = VisualTreeHelper.GetChildrenCount(current);
            for (int i = 0; i < count; i++)
            {
                queue.Enqueue(VisualTreeHelper.GetChild(current, i));
            }
        }

        return null;
    }

    protected FrameworkElement FindElementByName(string name)
    {
        var element = this.FindName(name) as FrameworkElement;
        if (element != null)
        {
            return element;
        }

        if (this.Content is DependencyObject root)
        {
            var found = FindInDescendants(root, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static FrameworkElement FindInDescendants(DependencyObject root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
        {
            return null;
        }

        var queue = new System.Collections.Generic.Queue<DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is FrameworkElement fe)
            {
                var local = fe.FindName(name) as FrameworkElement;
                if (local != null)
                {
                    return local;
                }
            }

            int count = VisualTreeHelper.GetChildrenCount(current);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(current, i);
                queue.Enqueue(child);
            }
        }

        return null;
    }
}
