// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.CmdPal.UI.Settings;

/// <summary>Declares a settings-page navigation anchor.</summary>
public static class SettingsPageTarget
{
    /// <summary>Identifies the current UI anchor attached to a settings element.</summary>
    public static readonly DependencyProperty IdProperty = DependencyProperty.RegisterAttached(
        "Id",
        typeof(string),
        typeof(SettingsPageTarget),
        new PropertyMetadata(string.Empty));

    /// <summary>Gets the current UI anchor identifier.</summary>
    public static string GetId(DependencyObject element) => (string)element.GetValue(IdProperty);

    /// <summary>Sets the current UI anchor identifier.</summary>
    public static void SetId(DependencyObject element, string value) => element.SetValue(IdProperty, value);

    internal static async Task<(FrameworkElement? Target, bool IsHidden)> NavigateAsync(
        FrameworkElement root,
        string targetId,
        CancellationToken cancellationToken)
    {
        await WaitForLoadedAsync(root, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        root.UpdateLayout();
        if (FindTarget(root, targetId) is not { } target)
        {
            return (null, false);
        }

        ExpandAncestors(target);
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        root.UpdateLayout();
        if (!IsVisible(target))
        {
            return (null, true);
        }

        _ = FindFocusableControl(target)?.Focus(FocusState.Programmatic);
        target.StartBringIntoView(new BringIntoViewOptions
        {
            AnimationDesired = false,
            VerticalAlignmentRatio = 0.1,
        });
        return (target, false);
    }

    private static bool IsVisible(FrameworkElement target)
    {
        for (DependencyObject? current = target; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is UIElement element && element.Visibility != Visibility.Visible)
            {
                return false;
            }
        }

        return true;
    }

    private static FrameworkElement? FindTarget(DependencyObject root, string targetId)
    {
        if (root is FrameworkElement element &&
            string.Equals(GetId(element), targetId, StringComparison.OrdinalIgnoreCase))
        {
            return element;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            if (FindTarget(VisualTreeHelper.GetChild(root, index), targetId) is { } target)
            {
                return target;
            }
        }

        return null;
    }

    private static void ExpandAncestors(FrameworkElement target)
    {
        if (target is SettingsExpander targetExpander)
        {
            targetExpander.IsExpanded = true;
        }

        for (var parent = VisualTreeHelper.GetParent(target); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is SettingsExpander expander)
            {
                expander.IsExpanded = true;
            }
        }
    }

    private static Control? FindFocusableControl(DependencyObject root)
    {
        if (root is Control { IsEnabled: true, IsTabStop: true, Visibility: Visibility.Visible } control)
        {
            return control;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            if (FindFocusableControl(VisualTreeHelper.GetChild(root, index)) is { } focusable)
            {
                return focusable;
            }
        }

        return null;
    }

    internal static async Task WaitForLoadedAsync(FrameworkElement root, CancellationToken cancellationToken)
    {
        if (root.IsLoaded)
        {
            return;
        }

        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnLoaded(object sender, RoutedEventArgs args) => loaded.TrySetResult();

        root.Loaded += OnLoaded;
        try
        {
            if (!root.IsLoaded)
            {
                await loaded.Task.WaitAsync(cancellationToken);
            }
        }
        finally
        {
            root.Loaded -= OnLoaded;
        }
    }
}
