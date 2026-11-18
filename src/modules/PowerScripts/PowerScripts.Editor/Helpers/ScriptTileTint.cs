// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace PowerScripts.Editor.Helpers;

/// <summary>Tints a script tile by the kind of value the script works with, matching the light or dark theme of the tile.</summary>
public static class ScriptTileTint
{
    /// <summary>Identifies the attached Kind property ("text", "image", "files", "audio", "operation" or "none").</summary>
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
        if (d is not ContentPresenter tile)
        {
            return;
        }

        tile.ActualThemeChanged -= Tile_ActualThemeChanged;
        tile.ActualThemeChanged += Tile_ActualThemeChanged;
        tile.Loaded -= Tile_Loaded;
        tile.Loaded += Tile_Loaded;
        Apply(tile);
    }

    private static void Tile_ActualThemeChanged(FrameworkElement sender, object args) => Apply((ContentPresenter)sender);

    private static void Tile_Loaded(object sender, RoutedEventArgs e) => Apply((ContentPresenter)sender);

    private static void Apply(ContentPresenter tile)
    {
        var dark = tile.ActualTheme == ElementTheme.Dark;
        var hue = GetKind(tile) switch
        {
            "text" => dark ? 0x60CDFFu : 0x0067C0u,
            "image" => dark ? 0x5FD8D8u : 0x00787Au,
            "files" => dark ? 0xC7A4FFu : 0x7A4FBFu,
            "audio" => dark ? 0xF19CE6u : 0xB4009Eu,
            "operation" => dark ? 0xFFB36Bu : 0xC25400u,
            _ => dark ? 0xA9B8C8u : 0x4A5868u,
        };

        tile.Background = new SolidColorBrush(FromRgb(hue, 0x21));
        tile.BorderBrush = new SolidColorBrush(FromRgb(hue, 0x2E));
        tile.Foreground = new SolidColorBrush(FromRgb(hue, 0xFF));
    }

    private static Color FromRgb(uint rgb, byte alpha) =>
        Color.FromArgb(alpha, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
}
