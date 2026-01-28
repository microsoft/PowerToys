// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.UI.Xaml;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Microsoft.CmdPal.UI.Services;

/// <summary>
/// Provides theme resources and acrylic backdrop parameters matching the default Command Palette theme.
/// </summary>
internal sealed class NormalThemeProvider : IThemeProvider
{
    private static readonly Color DarkBaseColor = Color.FromArgb(255, 32, 32, 32);
    private static readonly Color LightBaseColor = Color.FromArgb(255, 243, 243, 243);
    private readonly UISettings _uiSettings;

    public NormalThemeProvider(UISettings uiSettings)
    {
        ArgumentNullException.ThrowIfNull(uiSettings);
        _uiSettings = uiSettings;
    }

    public string ThemeKey => "normal";

    public string ResourcePath => "ms-appx:///Styles/Theme.Normal.xaml";

    public BackdropParameters GetBackdropParameters(ThemeContext context)
    {
        var isLight = context.Theme == ElementTheme.Light ||
                      (context.Theme == ElementTheme.Default &&
                       _uiSettings.GetColorValue(UIColorType.Background).R > 128);

        var backdropStyle = context.TransparencyMode ?? BackdropStyle.Acrylic;

        // Base opacities before applying user's backdrop opacity
        var (baseTintOpacity, baseLuminosityOpacity) = backdropStyle switch
        {
            BackdropStyle.Acrylic => (0.5f, isLight ? 0.9f : 0.96f),
            BackdropStyle.Mica => (0.8f, 1.0f),
            _ => (1.0f, 1.0f), // Clear
        };

        // Compute effective opacities based on style
        // For Clear: only BackdropOpacity matters (controls alpha of solid color)
        // For Acrylic/Mica: multiply base opacity with BackdropOpacity
        var effectiveOpacity = backdropStyle == BackdropStyle.Clear
            ? context.BackdropOpacity
            : baseTintOpacity * context.BackdropOpacity;

        var effectiveLuminosityOpacity = baseLuminosityOpacity * context.BackdropOpacity;

        return new BackdropParameters(
            TintColor: isLight ? LightBaseColor : DarkBaseColor,
            FallbackColor: isLight ? LightBaseColor : DarkBaseColor,
            EffectiveOpacity: effectiveOpacity,
            EffectiveLuminosityOpacity: effectiveLuminosityOpacity,
            Style: backdropStyle);
    }
}
