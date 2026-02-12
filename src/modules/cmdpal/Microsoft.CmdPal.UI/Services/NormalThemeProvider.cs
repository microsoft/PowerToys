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

        var backdropStyle = context.BackdropStyle ?? BackdropStyle.Acrylic;
        var config = BackdropStyles.Get(backdropStyle);

        // Apply light/dark theme adjustment to luminosity
        var baseLuminosityOpacity = isLight
            ? config.BaseLuminosityOpacity
            : Math.Min(config.BaseLuminosityOpacity + 0.06f, 1.0f);

        var effectiveOpacity = config.ComputeEffectiveOpacity(context.BackdropOpacity);
        var effectiveLuminosityOpacity = baseLuminosityOpacity * context.BackdropOpacity;

        return new BackdropParameters(
            TintColor: isLight ? LightBaseColor : DarkBaseColor,
            FallbackColor: isLight ? LightBaseColor : DarkBaseColor,
            EffectiveOpacity: effectiveOpacity,
            EffectiveLuminosityOpacity: effectiveLuminosityOpacity,
            Style: backdropStyle);
    }
}
