// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>Theme services shared by the native chart controls.</summary>
internal static class ChartTheme
{
    private static readonly Lazy<UISettings> UserInterfaceSettings = new(() => new UISettings());
    private static readonly Lazy<AccessibilitySettings> AccessibilitySettings = new(() => new AccessibilitySettings());

    // Contrast themes define these as distinct, readable colors on the Window background. The count
    // matches ChartPalette.HighContrastColorCount.
    private static readonly UIElementType[] HighContrastDataElements =
    [
        UIElementType.Highlight,
        UIElementType.Hotlight,
        UIElementType.WindowText,
    ];

    public static bool IsHighContrast => AccessibilitySettings.Value.HighContrast;

    public static bool AnimationsEnabled => UserInterfaceSettings.Value.AnimationsEnabled;

    /// <summary>Gets the accent color that reads well on the current theme's surfaces.</summary>
    public static ChartColor GetAccent(bool isDarkTheme)
    {
        var color = UserInterfaceSettings.Value.GetColorValue(isDarkTheme ? UIColorType.AccentLight2 : UIColorType.AccentDark1);
        return new ChartColor(color.A, color.R, color.G, color.B);
    }

    /// <summary>
    /// Resolves a data color. In high contrast, items cycle through distinct system colors by
    /// index, so series, slices, and segments stay distinguishable.
    /// </summary>
    public static ChartColor Resolve(string? itemColor, string? chartColor, string? colorSet, int index, bool isDarkTheme) =>
        IsHighContrast
            ? GetSystemColor(HighContrastDataElements[ChartPalette.GetHighContrastSlot(index)])
            : ChartPalette.ResolveSeriesColor(itemColor, chartColor, colorSet, index, isDarkTheme, GetAccent(isDarkTheme));

    /// <summary>
    /// Resolves a semantic color name from the schema: <c>accent</c> (and a badge's
    /// <c>informative</c>), <c>good</c>, <c>warning</c>, or <c>attention</c>. Other names, such as
    /// <c>default</c> or <c>subtle</c>, return null, so callers fall back to their default.
    /// </summary>
    public static ChartColor? ResolveSemantic(string? style, bool isDarkTheme)
    {
        var name = style?.ToLowerInvariant() switch
        {
            "accent" or "informative" => ChartPalette.AccentColorName,
            "good" => "good",
            "warning" => "warning",
            "attention" => "attention",
            _ => null,
        };

        if (name is null)
        {
            return null;
        }

        if (IsHighContrast)
        {
            return GetHighContrastColor();
        }

        return ChartPalette.TryResolve(name, isDarkTheme, GetAccent(isDarkTheme), out var color)
            ? color
            : GetAccent(isDarkTheme);
    }

    /// <summary>Gets the color of an empty track, such as the unfilled part of a gauge.</summary>
    public static ChartColor GetTrackColor(bool isDarkTheme) =>
        isDarkTheme ? new ChartColor(0x29, 0xFF, 0xFF, 0xFF) : new ChartColor(0x1F, 0x00, 0x00, 0x00);

    /// <summary>
    /// Gets the Fluent text color for the element's theme. Lookups through application resources
    /// follow the app theme instead, which can differ from the window's theme.
    /// </summary>
    public static ChartColor GetTextColor(bool isDarkTheme, bool secondary)
    {
        if (IsHighContrast)
        {
            return GetSystemColor(UIElementType.WindowText);
        }

        return (isDarkTheme, secondary) switch
        {
            (true, false) => new ChartColor(0xFF, 0xFF, 0xFF, 0xFF),
            (true, true) => new ChartColor(0xC5, 0xFF, 0xFF, 0xFF),
            (false, false) => new ChartColor(0xE4, 0x00, 0x00, 0x00),
            (false, true) => new ChartColor(0x9E, 0x00, 0x00, 0x00),
        };
    }

    public static ChartColor GetHighContrastColor() => GetSystemColor(UIElementType.Highlight);

    /// <summary>
    /// Gets a contrast theme color: the same system color the SystemColor*Color resources use.
    /// It's read with a typed call because a color boxed in a resource dictionary can't be
    /// unboxed under native AOT.
    /// </summary>
    private static ChartColor GetSystemColor(UIElementType type)
    {
        var color = UserInterfaceSettings.Value.UIElementColor(type);
        return new ChartColor(color.A, color.R, color.G, color.B);
    }

    /// <summary>Picks black or white text for a filled background.</summary>
    public static ChartColor GetContrastingText(ChartColor background)
    {
        var luminance = ((0.2126 * background.R) + (0.7152 * background.G) + (0.0722 * background.B)) / 255;
        return luminance > 0.55 ? new ChartColor(0xE4, 0, 0, 0) : new ChartColor(0xFF, 0xFF, 0xFF, 0xFF);
    }

    public static global::Windows.UI.Color ToColor(ChartColor color) =>
        ColorHelper.FromArgb(color.A, color.R, color.G, color.B);

    public static SolidColorBrush ToBrush(ChartColor color) => new(ToColor(color));

    /// <summary>
    /// Gets a text style from the application resources. Resource values reach managed code as
    /// plain objects, and under native AOT CsWinRT finds their type by name, which needs
    /// <see cref="Style"/>'s metadata.
    /// </summary>
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Style))]
    public static Style GetTextStyle(string key) => (Style)Application.Current.Resources[key];
}
