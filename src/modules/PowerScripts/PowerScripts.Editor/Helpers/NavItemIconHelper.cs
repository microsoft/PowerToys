// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;

namespace PowerScripts.Editor.Helpers;

/// <summary>Attached properties used by the rail NavigationView item template (adapted from AI Dev Gallery).</summary>
public static class NavItemIconHelper
{
    public static readonly DependencyProperty SelectedIconProperty =
        DependencyProperty.RegisterAttached("SelectedIcon", typeof(object), typeof(NavItemIconHelper), new PropertyMetadata(null));

    public static readonly DependencyProperty UnselectedIconProperty =
        DependencyProperty.RegisterAttached("UnselectedIcon", typeof(object), typeof(NavItemIconHelper), new PropertyMetadata(null));

    public static readonly DependencyProperty StaticIconVisibilityProperty =
        DependencyProperty.RegisterAttached("StaticIconVisibility", typeof(Visibility), typeof(NavItemIconHelper), new PropertyMetadata(Visibility.Collapsed));

    public static object GetSelectedIcon(DependencyObject obj) => obj.GetValue(SelectedIconProperty);

    public static void SetSelectedIcon(DependencyObject obj, object value) => obj.SetValue(SelectedIconProperty, value);

    public static object GetUnselectedIcon(DependencyObject obj) => obj.GetValue(UnselectedIconProperty);

    public static void SetUnselectedIcon(DependencyObject obj, object value) => obj.SetValue(UnselectedIconProperty, value);

    public static Visibility GetStaticIconVisibility(DependencyObject obj) => (Visibility)obj.GetValue(StaticIconVisibilityProperty);

    public static void SetStaticIconVisibility(DependencyObject obj, Visibility value) => obj.SetValue(StaticIconVisibilityProperty, value);
}
