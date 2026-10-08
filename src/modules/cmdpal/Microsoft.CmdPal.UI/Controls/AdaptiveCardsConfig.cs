// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdaptiveCards.Rendering.WinUI3;
using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// Creates Adaptive Card host configs from Fluent theme tokens, the system accent color, and the
/// high contrast colors, so cards match the rest of Command Palette in every theme.
/// </summary>
public sealed class AdaptiveCardsConfig
{
    // Accent and contrast changes create new configs, so keep only the latest few.
    private const int MaximumCachedConfigs = 4;

    private static readonly UISettings UserInterfaceSettings = new();
    private static readonly AccessibilitySettings AccessibilitySettings = new();
    private static readonly Lock CacheLock = new();
    private static readonly Dictionary<string, AdaptiveHostConfig> CachedConfigs = new(StringComparer.Ordinal);

    public static AdaptiveHostConfig Light => Create(ElementTheme.Light);

    public static AdaptiveHostConfig Dark => Create(ElementTheme.Dark);

    /// <summary>
    /// Returns the host config for content shown in <paramref name="theme"/>. Calls that resolve
    /// to the same colors return the same instance, so callers can compare by reference, even
    /// when light and dark content are on screen at the same time.
    /// </summary>
    public static AdaptiveHostConfig Create(ElementTheme theme)
    {
        var json = AdaptiveHostConfigJson.Create(GetTokens(theme));
        lock (CacheLock)
        {
            if (CachedConfigs.TryGetValue(json, out var cached))
            {
                return cached;
            }

            if (CachedConfigs.Count >= MaximumCachedConfigs)
            {
                CachedConfigs.Clear();
            }

            var config = AdaptiveHostConfig.FromJsonString(json).HostConfig;
            CachedConfigs[json] = config;
            return config;
        }
    }

    private static AdaptiveCardThemeTokens GetTokens(ElementTheme theme)
    {
        if (AccessibilitySettings.HighContrast)
        {
            return AdaptiveCardThemeTokens.HighContrast(
                GetSystemColor(UIElementType.WindowText),
                GetSystemColor(UIElementType.GrayText),
                GetSystemColor(UIElementType.Highlight),
                GetSystemColor(UIElementType.Window));
        }

        // Match AccentTextFillColorPrimaryBrush: a darker accent on light surfaces and a
        // lighter accent on dark surfaces.
        return theme == ElementTheme.Light
            ? AdaptiveCardThemeTokens.Light(GetAccent(UIColorType.AccentDark2))
            : AdaptiveCardThemeTokens.Dark(GetAccent(UIColorType.AccentLight3));
    }

    private static string GetAccent(UIColorType type)
    {
        var color = UserInterfaceSettings.GetColorValue(type);
        return AdaptiveCardThemeTokens.ToHex(color.A, color.R, color.G, color.B);
    }

    /// <summary>
    /// Gets a contrast theme color: the same system color the SystemColor*Color resources use.
    /// It's read with a typed call because a color boxed in a resource dictionary can't be
    /// unboxed under native AOT.
    /// </summary>
    private static string GetSystemColor(UIElementType type)
    {
        var color = UserInterfaceSettings.UIElementColor(type);
        return AdaptiveCardThemeTokens.ToHex(color.A, color.R, color.G, color.B);
    }
}
