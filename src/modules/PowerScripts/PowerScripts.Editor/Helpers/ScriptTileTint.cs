// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
            "text" or "blue" => dark ? 0x60CDFFu : 0x0067C0u,
            "image" or "teal" => dark ? 0x5FD8D8u : 0x00787Au,
            "files" or "purple" => dark ? 0xC7A4FFu : 0x7A4FBFu,
            "audio" or "pink" => dark ? 0xF19CE6u : 0xB4009Eu,
            "operation" or "orange" => dark ? 0xFFB36Bu : 0xC25400u,
            "green" => dark ? 0x6CCB5Fu : 0x0F7B0Fu,
            _ => dark ? 0xA9B8C8u : 0x4A5868u,
        };

        var background = new SolidColorBrush(FromRgb(hue, 0x21));
        var border = new SolidColorBrush(FromRgb(hue, 0x2E));
        var foreground = new SolidColorBrush(FromRgb(hue, 0xFF));
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

    private static Color FromRgb(uint rgb, byte alpha) =>
        Color.FromArgb(alpha, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
}
