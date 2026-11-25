// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace PowerScripts.Editor.Helpers;

/// <summary>Tints a tile by the kind of value it stands for, matching the light or dark theme of the tile.</summary>
public static class ScriptTileTint
{
    /// <summary>Identifies the attached Kind property: a value kind ("text", "image", "files", "audio", "operation", "none")
    /// or a hue ("blue", "teal", "purple", "orange", "green", "slate").</summary>
    public static readonly DependencyProperty KindProperty =
        DependencyProperty.RegisterAttached("Kind", typeof(string), typeof(ScriptTileTint), new PropertyMetadata(null, OnKindChanged));

    /// <summary>Gets the tile kind.</summary>
    /// <param name="obj">The tile.</param>
    /// <returns>The tile kind.</returns>
    public static string GetKind(DependencyObject obj) => (string)obj.GetValue(KindProperty);

    /// <summary>Sets the tile kind.</summary>
    /// <param name="obj">The tile.</param>
    /// <param name="value">The tile kind.</param>
    public static void SetKind(DependencyObject obj, string value) => obj.SetValue(KindProperty, value);

    private static void OnKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement tile)
        {
            return;
        }

        tile.ActualThemeChanged -= Tile_ActualThemeChanged;
        tile.ActualThemeChanged += Tile_ActualThemeChanged;
        tile.Loaded -= Tile_Loaded;
        tile.Loaded += Tile_Loaded;
        Apply(tile);
    }

    private static void Tile_ActualThemeChanged(FrameworkElement sender, object args) => Apply(sender);

    private static void Tile_Loaded(object sender, RoutedEventArgs e) => Apply((FrameworkElement)sender);

    private static void Apply(FrameworkElement tile)
    {
        var dark = tile.ActualTheme == ElementTheme.Dark;
        var hue = GetKind(tile) switch
        {
            "text" or "blue" => dark ? "#60CDFF" : "#0067C0",
            "image" or "teal" => dark ? "#5FD8D8" : "#00787A",
            "files" or "purple" => dark ? "#C7A4FF" : "#7A4FBF",
            "audio" or "pink" => dark ? "#F19CE6" : "#B4009E",
            "operation" or "orange" => dark ? "#FFB36B" : "#C25400",
            "green" => dark ? "#6CCB5F" : "#0F7B0F",
            _ => dark ? "#A9B8C8" : "#4A5868",
        };

        var color = (Color)XamlBindingHelper.ConvertValue(typeof(Color), hue);
        var backgroundColor = color;
        backgroundColor.A = 0x21;
        var borderColor = color;
        borderColor.A = 0x2E;
        var background = new SolidColorBrush(backgroundColor);
        var border = new SolidColorBrush(borderColor);
        var foreground = new SolidColorBrush(color);
        switch (tile)
        {
            case ContentPresenter presenter:
                presenter.Background = background;
                presenter.BorderBrush = border;
                presenter.Foreground = foreground;
                break;
            case Control control:
                control.Background = background;
                control.BorderBrush = border;
                control.Foreground = foreground;
                break;
            case Border box:
                box.Background = background;
                box.BorderBrush = border;
                break;
            case IconElement icon:
                icon.Foreground = foreground;
                break;
        }
    }
}
