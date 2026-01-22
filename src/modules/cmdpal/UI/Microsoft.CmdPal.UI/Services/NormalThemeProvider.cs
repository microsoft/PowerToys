// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

    public AcrylicBackdropParameters GetAcrylicBackdrop(ThemeContext context)
    {
        var isLight = context.Theme == ElementTheme.Light ||
                      (context.Theme == ElementTheme.Default &&
                       _uiSettings.GetColorValue(UIColorType.Background).R > 128);

        return new AcrylicBackdropParameters(
            TintColor: isLight ? LightBaseColor : DarkBaseColor,
            FallbackColor: isLight ? LightBaseColor : DarkBaseColor,
            TintOpacity: 0.5f,
            LuminosityOpacity: isLight ? 0.9f : 0.96f);
    }
}
